/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.PowerShell.Commands;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;

[module: SuppressMessage("Microsoft.Design", "CA1014:MarkAssembliesWithClsCompliant")]
[module: SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]

namespace Microsoft.PowerShell
{
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors")]
    [SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    class ExitException : Exception { }

    public partial class PSConsoleReadLine : IPSConsoleReadLineMockableMethods
    {
        private static readonly PSConsoleReadLine _singleton = new PSConsoleReadLine();

        private bool _delayedOneTimeInitCompleted;

        private IPSConsoleReadLineMockableMethods _mockableMethods;
        private IConsole _console;
        private Encoding _initialOutputEncoding;

        private EngineIntrinsics _engineIntrinsics;
        private Thread _readKeyThread;
        private AutoResetEvent _readKeyWaitHandle;
        private AutoResetEvent _keyReadWaitHandle;
        internal ManualResetEvent _closingWaitHandle;
        private WaitHandle[] _threadProcWaitHandles;
        private WaitHandle[] _requestKeyWaitHandles;

        private readonly StringBuilder _buffer;
        private readonly StringBuilder _statusBuffer;
        private bool _statusIsErrorMessage;
        private string _statusLinePrompt;
        private List<EditItem> _edits;
        private int _editGroupStart;
        private int _undoEditIndex;
        private int _mark;
        private bool _inputAccepted;
        private readonly Queue<ConsoleKeyInfo> _queuedKeys;
        private Stopwatch _lastRenderTime;
        private static readonly Stopwatch _readkeyStopwatch = new Stopwatch();

        // Save a fixed # of keys so we can reconstruct a repro after a crash
        private static readonly HistoryQueue<ConsoleKeyInfo> _lastNKeys = new HistoryQueue<ConsoleKeyInfo>(200);

        // Tokens etc.
        private Token[] _tokens;
        private Ast _ast;
        private ParseError[] _parseErrors;

        bool IPSConsoleReadLineMockableMethods.RunspaceIsRemote(Runspace runspace)
        {
            return runspace?.ConnectionInfo != null;
        }

        private void ReadOneOrMoreKeys()
        {
            _readkeyStopwatch.Restart();
            while (_console.KeyAvailable)
            {
                var key = _console.ReadKey();
                _lastNKeys.Enqueue(key);
                _queuedKeys.Enqueue(key);
                if (_readkeyStopwatch.ElapsedMilliseconds > 2)
                {
                    // Don't spend too long in this loop if there are lots of queued keys
                    break;
                }
            }

            if (_queuedKeys.Count == 0)
            {
                var key = _console.ReadKey();
                _lastNKeys.Enqueue(key);
                _queuedKeys.Enqueue(key);
            }
        }

        private void ReadKeyThreadProc()
        {
            while (true)
            {
                // Wait until ReadKey tells us to read a key (or it's time to exit).
                int handleId = WaitHandle.WaitAny(_singleton._threadProcWaitHandles);
                if (handleId == 1) // It was the _closingWaitHandle that was signaled.
                    break;

                ReadOneOrMoreKeys();

                // One or more keys were read - let ReadKey know we're done.
                _keyReadWaitHandle.Set();
            }
        }

        internal static ConsoleKeyInfo ReadKey()
        {
            // Reading a key is handled on a different thread.  During process shutdown,
            // PowerShell will wait in it's ConsoleCtrlHandler until the pipeline has completed.
            // If we're running, we're most likely blocked waiting for user input.
            // This is a problem for two reasons.  First, exiting takes a long time (5 seconds
            // on Win8) because PowerShell is waiting forever, but the OS will forcibly terminate
            // the console.  Also - if there are any event handlers for the engine event
            // PowerShell.Exiting, those handlers won't get a chance to run.
            //
            // By waiting for a key on a different thread, our pipeline execution thread
            // (the thread Readline is called from) avoid being blocked in code that can't
            // be unblocked and instead blocks on events we control.

            // First, set an event so the thread to read a key actually attempts to read a key.
            _singleton._readKeyWaitHandle.Set();

            int handleId;
            System.Management.Automation.PowerShell ps = null;

            try
            {
                while (true)
                {
                    // Next, wait for one of three things:
                    //   - a key is pressed
                    //   - the console is exiting
                    //   - 300ms - to process events if we're idle

                    handleId = WaitHandle.WaitAny(_singleton._requestKeyWaitHandles, 300);
                    if (handleId != WaitHandle.WaitTimeout)
                        break;

                    // If we timed out, check for event subscribers (which is just
                    // a hint that there might be an event waiting to be processed.)
                    var eventSubscribers = _singleton._engineIntrinsics?.Events.Subscribers;
                    if (eventSubscribers?.Count > 0)
                    {
                        bool runPipelineForEventProcessing = false;
                        foreach (var sub in eventSubscribers)
                        {
                            if (sub.SourceIdentifier.Equals("PowerShell.OnIdle", StringComparison.OrdinalIgnoreCase))
                            {
                                // There is an OnIdle event.  We're idle because we timed out.  Normally
                                // PowerShell generates this event, but PowerShell assumes the engine is not
                                // idle because it called PSConsoleHostReadline which isn't returning.
                                // So we generate the event instead.
                                _singleton._engineIntrinsics.Events.GenerateEvent("PowerShell.OnIdle", null, null, null);
                                runPipelineForEventProcessing = true;
                                break;
                            }

                            // If there are any event subscribers that have an action (which might
                            // write to the console) and have a source object (i.e. aren't engine
                            // events), run a tiny useless bit of PowerShell so that the events
                            // can be processed.
                            if (sub.Action != null && sub.SourceObject != null)
                            {
                                runPipelineForEventProcessing = true;
                                break;
                            }
                        }

                        if (runPipelineForEventProcessing)
                        {
                            if (ps == null)
                            {
                                ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                                ps.AddScript("0");
                            }

                            // To detect output during possible event processing, see if the cursor moved
                            // and rerender if so.
                            var console = _singleton._console;
                            var y = console.CursorTop;
                            ps.Invoke();
                            if (y != console.CursorTop)
                            {
                                _singleton._initialY = console.CursorTop;
                                _singleton.Render();
                            }
                        }
                    }
                }
            }
            finally
            {
                ps?.Dispose();
            }

            if (handleId == 1)
            {
                // The console is exiting - throw an exception to unwind the stack to the point
                // where we can return from ReadLine.
                if (_singleton.Options.HistorySaveStyle == HistorySaveStyle.SaveAtExit)
                {
                    _singleton.SaveHistoryAtExit();
                }
                _singleton._historyFileMutex.Dispose();

                throw new OperationCanceledException();
            }

            var key = _singleton._queuedKeys.Dequeue();
            return key;
        }

        private void PrependQueuedKeys(ConsoleKeyInfo key)
        {
            if (_queuedKeys.Count > 0)
            {
                // This should almost never happen so being inefficient is fine.
                var list = new List<ConsoleKeyInfo>(_queuedKeys);
                _queuedKeys.Clear();
                _queuedKeys.Enqueue(key);
                list.ForEach(k => _queuedKeys.Enqueue(k));
            }
            else
            {
                _queuedKeys.Enqueue(key);
            }
        }

        /// <summary>
        /// Entry point - called from the PowerShell function PSConsoleHostReadline
        /// after the prompt has been displayed.
        /// </summary>
        /// <returns>The complete command line.</returns>
        public static string ReadLine(Runspace runspace, EngineIntrinsics engineIntrinsics)
        {
            var console = _singleton._console;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PlatformWindows.Init();
            }

            bool firstTime = true;
            while (true)
            {
                try
                {
                    if (firstTime)
                    {
                        firstTime = false;
                        _singleton.Initialize(runspace, engineIntrinsics);
                    }

                    return _singleton.InputLoop();
                }
                catch (OperationCanceledException)
                {
                    // Console is exiting - return value isn't too critical - null or 'exit' could work equally well.
                    return "";
                }
                catch (ExitException)
                {
                    return "exit";
                }
                catch (CustomHandlerException e)
                {
                    var oldColor = console.ForegroundColor;
                    console.ForegroundColor = ConsoleColor.Red;
                    console.WriteLine(
                        string.Format(CultureInfo.CurrentUICulture, PSReadLineResources.OopsCustomHandlerException, e.InnerException.Message));
                    console.ForegroundColor = oldColor;

                    var lineBeforeCrash = _singleton._buffer.ToString();
                    _singleton.Initialize(runspace, _singleton._engineIntrinsics);
                    InvokePrompt();
                    Insert(lineBeforeCrash);
                }
                catch (Exception e)
                {
                    // If we're running tests, just throw.
                    if (_singleton._mockableMethods != _singleton)
                    {
                        throw;
                    }

                    while (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    var oldColor = console.ForegroundColor;
                    console.ForegroundColor = ConsoleColor.Red;
                    console.WriteLine(PSReadLineResources.OopsAnErrorMessage1);
                    console.ForegroundColor = oldColor;
                    var sb = new StringBuilder();
                    for (int i = 0; i < _lastNKeys.Count; i++)
                    {
                        sb.Append(' ');
                        sb.Append(_lastNKeys[i].ToGestureString());

                        if (_singleton._dispatchTable.TryGetValue(_lastNKeys[i], out var handler) &&
                            "AcceptLine".Equals(handler.BriefDescription, StringComparison.OrdinalIgnoreCase))
                        {
                            // Make it a little easier to see the keys
                            sb.Append('\n');
                        }
                    }

                    console.WriteLine(string.Format(CultureInfo.CurrentUICulture, PSReadLineResources.OopsAnErrorMessage2, _lastNKeys.Count, sb, e));
                    var lineBeforeCrash = _singleton._buffer.ToString();
                    _singleton.Initialize(runspace, _singleton._engineIntrinsics);
                    InvokePrompt();
                    Insert(lineBeforeCrash);
                }
                finally
                {
                    Console.OutputEncoding = _singleton._initialOutputEncoding;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        PlatformWindows.Complete();
                    }
                }
            }
        }

        private string InputLoop()
        {
            while (true)
            {
                var killCommandCount = _killCommandCount;
                var yankCommandCount = _yankCommandCount;
                var tabCommandCount = _tabCommandCount;
                var searchHistoryCommandCount = _searchHistoryCommandCount;
                var recallHistoryCommandCount = _recallHistoryCommandCount;
                var yankLastArgCommandCount = _yankLastArgCommandCount;
                var visualSelectionCommandCount = _visualSelectionCommandCount;
                var movingAtEndOfLineCount = _moveToLineCommandCount;

                var key = ReadKey();
                ProcessOneKey(key, _dispatchTable, ignoreIfNoAction: false, arg: null);
                if (_inputAccepted)
                {
                    return MaybeAddToHistory(_buffer.ToString(), _edits, _undoEditIndex, readingHistoryFile: false, fromDifferentSession: false);
                }

                if (killCommandCount == _killCommandCount)
                {
                    // Reset kill command count if it didn't change
                    _killCommandCount = 0;
                }
                if (yankCommandCount == _yankCommandCount)
                {
                    // Reset yank command count if it didn't change
                    _yankCommandCount = 0;
                }
                if (yankLastArgCommandCount == _yankLastArgCommandCount)
                {
                    // Reset yank last arg command count if it didn't change
                    _yankLastArgCommandCount = 0;
                    _yankLastArgState = null;
                }
                if (tabCommandCount == _tabCommandCount)
                {
                    // Reset tab command count if it didn't change
                    _tabCommandCount = 0;
                    _tabCompletions = null;
                }
                if (searchHistoryCommandCount == _searchHistoryCommandCount)
                {
                    if (_searchHistoryCommandCount > 0)
                    {
                        _emphasisStart = -1;
                        _emphasisLength = 0;
                        Render();
                        _currentHistoryIndex = _history.Count;
                    }
                    _searchHistoryCommandCount = 0;
                    _searchHistoryPrefix = null;
                }
                if (recallHistoryCommandCount == _recallHistoryCommandCount)
                {
                    if (_recallHistoryCommandCount > 0)
                    {
                        _currentHistoryIndex = _history.Count;
                    }
                    _recallHistoryCommandCount = 0;
                }
                if (searchHistoryCommandCount == _searchHistoryCommandCount &&
                    recallHistoryCommandCount == _recallHistoryCommandCount)
                {
                    _hashedHistory = null;
                }
                if (visualSelectionCommandCount == _visualSelectionCommandCount && _visualSelectionCommandCount > 0)
                {
                    _visualSelectionCommandCount = 0;
                    Render();  // Clears the visual selection
                }
                if (movingAtEndOfLineCount == _moveToLineCommandCount)
                {
                    _moveToLineCommandCount = 0;
                }
            }
        }

        T CallPossibleExternalApplication<T>(Func<T> func)
        {
            try
            {
                Console.OutputEncoding = _initialOutputEncoding;
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? PlatformWindows.CallPossibleExternalApplication(func)
                    : func();
            }
            finally
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
        }

        void CallPossibleExternalApplication(Action action)
        {
            CallPossibleExternalApplication<object>(() => { action(); return null; });
        }

        void ProcessOneKey(ConsoleKeyInfo key, Dictionary<ConsoleKeyInfo, KeyHandler> dispatchTable, bool ignoreIfNoAction, object arg)
        {
            // Our dispatch tables are built as much as possible in a portable way, so for example,
            // we avoid depending on scan codes like ConsoleKey.Oem6 and instead look at the
            // ConsoleKeyInfo.Key. We also want to ignore the shift state as that may differ on
            // different keyboard layouts.
            //
            // That said, we first look up exactly what we get from Console.ReadKey - that will fail
            // most of the time, and when it does, we normalize the key.
            if (!dispatchTable.TryGetValue(key, out var handler))
            {
                // If we see a control character where Ctrl wasn't used but shift was, treat that like
                // shift hadn't be pressed.  This cleanly allows Shift+Backspace without adding a key binding.
                if (key.KeyChar > 0 && char.IsControl(key.KeyChar) && key.Modifiers == ConsoleModifiers.Shift)
                {
                    key = new ConsoleKeyInfo(key.KeyChar, key.Key, false, false, false);
                    dispatchTable.TryGetValue(key, out handler);
                }
            }
            if (handler != null)
            {
                if (handler.ScriptBlock != null)
                {
                    CallPossibleExternalApplication(() => handler.Action(key, arg));
                }
                else
                {
                    handler.Action(key, arg);
                }
            }
            else if (!ignoreIfNoAction && key.ShouldInsert())
            {
                SelfInsert(key, arg);
            }
        }

        private PSConsoleReadLine()
        {
            _mockableMethods = this;
            _console = new ConhostConsole();

            _buffer = new StringBuilder(8 * 1024);
            _statusBuffer = new StringBuilder(256);
            _savedCurrentLine = new HistoryItem();
            _queuedKeys = new Queue<ConsoleKeyInfo>();

            string hostName = null;
            // This works mostly by luck - we're not doing anything to guarantee the constructor for our
            // singleton is called on a thread with a runspace, but it is happening by coincidence.
            using (var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                try
                {
                    ps.AddCommand("Get-Variable").AddParameter("Name", "host").AddParameter("ValueOnly");
                    var results = ps.Invoke();
                    dynamic host = results.Count == 1 ? results[0] : null;
                    if (host != null)
                    {
                        hostName = host.Name as string;
                    }
                }
                catch
                {
                }
            }
            if (hostName == null)
            {
                hostName = "PSReadline";
            }
            _options = new PSConsoleReadlineOptions(hostName);
            SetDefaultBindings(_options.EditMode);
        }

        private void Initialize(Runspace runspace, EngineIntrinsics engineIntrinsics)
        {
            _engineIntrinsics = engineIntrinsics;
            _runspace = runspace;

            if (!_delayedOneTimeInitCompleted)
            {
                DelayedOneTimeInitialize();
                _delayedOneTimeInitCompleted = true;
            }

            _previousRender = _initialPrevRender;
            _previousRender.bufferWidth = _console.BufferWidth;
            _previousRender.errorPrompt = false;
            _buffer.Clear();
            _edits = new List<EditItem>();
            _undoEditIndex = 0;
            _editGroupStart = -1;
            _current = 0;
            _mark = 0;
            _emphasisStart = -1;
            _emphasisLength = 0;
            _tokens = null;
            _parseErrors = null;
            _inputAccepted = false;
            _initialX = _console.CursorLeft;
            _initialY = _console.CursorTop;
            _killCommandCount = 0;
            _yankCommandCount = 0;
            _yankLastArgCommandCount = 0;
            _tabCommandCount = 0;
            _visualSelectionCommandCount = 0;
            _statusIsErrorMessage = false;

            _initialOutputEncoding = Console.OutputEncoding;
            Console.OutputEncoding = Encoding.UTF8;
            _lastRenderTime = Stopwatch.StartNew();

            _killCommandCount = 0;
            _yankCommandCount = 0;
            _yankLastArgCommandCount = 0;
            _tabCommandCount = 0;
            _recallHistoryCommandCount = 0;
            _visualSelectionCommandCount = 0;
            _hashedHistory = null;

            if (_getNextHistoryIndex > 0)
            {
                _currentHistoryIndex = _getNextHistoryIndex;
                UpdateFromHistory(moveCursor: true);
                _getNextHistoryIndex = 0;
                if (_searchHistoryCommandCount > 0)
                {
                    _searchHistoryPrefix = "";
                    if (Options.HistoryNoDuplicates)
                    {
                        _hashedHistory = new Dictionary<string, int>();
                    }
                }
            }
            else
            {
                _searchHistoryCommandCount = 0;
            }
        }

        private void DelayedOneTimeInitialize()
        {
            // Delayed initialization is needed so that options can be set
            // after the constuctor but have an affect before the user starts
            // editing their first command line.  For example, if the user
            // specifies a custom history save file, we don't want to try reading
            // from the default one.

            var historyCountVar = _engineIntrinsics?.SessionState.PSVariable.Get("MaximumHistoryCount");
            if (historyCountVar?.Value is int historyCountValue)
            {
                _options.MaximumHistoryCount = historyCountValue;
            }

            if (_options.PromptText == null &&
                _engineIntrinsics?.InvokeCommand.GetCommand("prompt", CommandTypes.Function) is FunctionInfo promptCommand)
            {
                var promptIsPure = null ==
                    promptCommand.ScriptBlock.Ast.Find(ast => ast is CommandAst ||
                                                      ast is InvokeMemberExpressionAst,
                                               searchNestedScriptBlocks: true);
                if (promptIsPure)
                {
                    var res = promptCommand.ScriptBlock.InvokeReturnAsIs(Array.Empty<object>());
                    string evaluatedPrompt = res as string;
                    if (evaluatedPrompt == null && res is PSObject psobject)
                    {
                        evaluatedPrompt = psobject.BaseObject as string;
                    }
                    if (evaluatedPrompt != null)
                    {
                        int i;
                        for (i = evaluatedPrompt.Length - 1; i >= 0; i--)
                        {
                            if (!char.IsWhiteSpace(evaluatedPrompt[i])) break;
                        }

                        if (i >= 0)
                        {
                            _options.PromptText = evaluatedPrompt.Substring(i);
                        }
                    }
                }
            }

            _historyFileMutex = new Mutex(false, GetHistorySaveFileMutexName());

            _history = new HistoryQueue<HistoryItem>(Options.MaximumHistoryCount);
            _currentHistoryIndex = 0;

            bool readHistoryFile = true;
            try
            {
                if (_options.HistorySaveStyle == HistorySaveStyle.SaveNothing && Runspace.DefaultRunspace != null)
                {
                    using (var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
                    {
                        ps.AddCommand("Microsoft.PowerShell.Core\\Get-History");
                        foreach (var historyInfo in ps.Invoke<HistoryInfo>())
                        {
                            AddToHistory(historyInfo.CommandLine);
                        }
                        readHistoryFile = false;
                    }
                }
            }
            catch
            {
            }

            if (readHistoryFile)
            {
                ReadHistoryFile();
            }

            _killIndex = -1; // So first add indexes 0.
            _killRing = new List<string>(Options.MaximumKillRingCount);

            _singleton._readKeyWaitHandle = new AutoResetEvent(false);
            _singleton._keyReadWaitHandle = new AutoResetEvent(false);
            _singleton._closingWaitHandle = new ManualResetEvent(false);
            _singleton._requestKeyWaitHandles = new WaitHandle[] {_singleton._keyReadWaitHandle, _singleton._closingWaitHandle};
            _singleton._threadProcWaitHandles = new WaitHandle[] {_singleton._readKeyWaitHandle, _singleton._closingWaitHandle};

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PlatformWindows.OneTimeInit(_singleton);
            }

            // This is for a "being hosted in an alternate appdomain scenario" (the
            // DomainUnload event is not raised for the default appdomain). It allows us
            // to exit cleanly when the appdomain is unloaded but the process is not going
            // away.
            if (!AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                AppDomain.CurrentDomain.DomainUnload += (x, y) =>
                {
                    _singleton._closingWaitHandle.Set();
                    _singleton._readKeyThread.Join(); // may need to wait for history to be written
                };
            }

            _singleton._readKeyThread = new Thread(_singleton.ReadKeyThreadProc) {IsBackground = true};
            _singleton._readKeyThread.Start();
        }

        private static void Chord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (_singleton._chordDispatchTable.TryGetValue(key.Value, out var secondKeyDispatchTable))
            {
                var secondKey = ReadKey();
                _singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, ignoreIfNoAction: true, arg: arg);
            }
        }

        /// <summary>
        /// Abort current action, e.g. incremental history search
        /// </summary>
        public static void Abort(ConsoleKeyInfo? key = null, object arg = null)
        {
        }

        /// <summary>
        /// Start a new digit argument to pass to other functions
        /// </summary>
        public static void DigitArgument(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue || char.IsControl(key.Value.KeyChar))
            {
                Ding();
                return;
            }

            if (_singleton._options.EditMode == EditMode.Vi && key.Value.KeyChar == '0')
            {
                BeginningOfLine();
                return;
            }

            bool sawDigit = false;
            _singleton._statusLinePrompt = "digit-argument: ";
            var argBuffer = _singleton._statusBuffer;
            argBuffer.Append(key.Value.KeyChar);
            if (key.Value.KeyChar == '-')
            {
                argBuffer.Append('1');
            }
            else
            {
                sawDigit = true;
            }

            _singleton.Render(); // Render prompt
            while (true)
            {
                var nextKey = ReadKey();
                if (_singleton._dispatchTable.TryGetValue(nextKey, out var handler))
                {
                    if (handler.Action == DigitArgument)
                    {
                        if (nextKey.KeyChar == '-')
                        {
                            if (argBuffer[0] == '-')
                            {
                                argBuffer.Remove(0, 1);
                            }
                            else
                            {
                                argBuffer.Insert(0, '-');
                            }
                            _singleton.Render(); // Render prompt
                            continue;
                        }

                        if (nextKey.KeyChar >= '0' && nextKey.KeyChar <= '9')
                        {
                            if (!sawDigit && argBuffer.Length > 0)
                            {
                                // Buffer is either '-1' or '1' from one or more Alt+- keys
                                // but no digits yet.  Remove the '1'.
                                argBuffer.Length -= 1;
                            }
                            sawDigit = true;
                            argBuffer.Append(nextKey.KeyChar);
                            _singleton.Render(); // Render prompt
                            continue;
                        }
                    }
                    else if (handler.Action == Abort ||
                             handler.Action == CancelLine ||
                             handler.Action == CopyOrCancelLine)
                    {
                        break;
                    }
                }

                if (int.TryParse(argBuffer.ToString(), out var intArg))
                {
                    _singleton.ProcessOneKey(nextKey, _singleton._dispatchTable, ignoreIfNoAction: false, arg: intArg);
                }
                else
                {
                    Ding();
                }
                break;
            }

            // Remove our status line
            argBuffer.Clear();
            _singleton.ClearStatusMessage(render: true);
        }


        /// <summary>
        /// Erases the current prompt and calls the prompt function to redisplay
        /// the prompt.  Useful for custom key handlers that change state, e.g.
        /// change the current directory.
        /// </summary>
        public static void InvokePrompt(ConsoleKeyInfo? key = null, object arg = null)
        {
            var console = _singleton._console;
            console.CursorVisible = false;

            if (arg is int newY)
            {
                if (newY >= console.BufferHeight)
                {
                    var toScroll = newY - console.BufferHeight + 1;
                    console.ScrollBuffer(toScroll);
                    newY -= toScroll;
                }
                console.SetCursorPosition(0, newY);
            }
            else
            {
                newY = _singleton._initialY - _singleton._options.ExtraPromptLineCount;

                console.SetCursorPosition(0, newY);

                // We need to rewrite the prompt, so blank out everything from a previous prompt invocation
                // in case the next one is shorter.
                var spaces = Spaces(console.BufferWidth);
                for (int i = 0; i < _singleton._options.ExtraPromptLineCount + 1; i++)
                {
                    console.Write(spaces);
                }

                console.SetCursorPosition(0, newY);
            }

            string newPrompt = GetPrompt();

            console.Write(newPrompt);
            _singleton._initialX = console.CursorLeft;
            _singleton._initialY = console.CursorTop;
            _singleton._previousRender = _initialPrevRender;

            _singleton.Render();
            console.CursorVisible = true;
        }

        private static string GetPrompt()
        {
            string newPrompt = null;
            if (_singleton._runspace?.Debugger != null && _singleton._runspace.Debugger.InBreakpoint)
            {
                // Run prompt command in debugger API to ensure it is run correctly on the runspace.
                // This handles remote runspace debugging and nested debugger scenarios.
                PSDataCollection<PSObject> results = new PSDataCollection<PSObject>();
                var command = new PSCommand();
                command.AddCommand("prompt");
                _singleton._runspace.Debugger.ProcessCommand(
                    command,
                    results);

                if (results.Count == 1)
                    newPrompt = results[0].BaseObject as string;
            }
            else
            {
                var runspaceIsRemote = _singleton._mockableMethods.RunspaceIsRemote(_singleton._runspace);

                System.Management.Automation.PowerShell ps;
                if (!runspaceIsRemote)
                {
                    ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                }
                else
                {
                    ps = System.Management.Automation.PowerShell.Create();
                    ps.Runspace = _singleton._runspace;
                }

                using (ps)
                {
                    ps.AddCommand("prompt");
                    var result = ps.Invoke<string>();
                    if (result.Count == 1)
                    {
                        newPrompt = result[0];

                        if (runspaceIsRemote)
                        {
                            if (!string.IsNullOrEmpty(_singleton._runspace?.ConnectionInfo?.ComputerName))
                            {
                                newPrompt = "[" + (_singleton._runspace?.ConnectionInfo).ComputerName + "]: " + newPrompt;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(newPrompt))
                newPrompt = "PS>";

            return newPrompt;
        }
    }
}
