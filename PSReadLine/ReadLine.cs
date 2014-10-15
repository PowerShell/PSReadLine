using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using PSConsoleUtilities.Internal;

namespace PSConsoleUtilities
{
    class ExitException : Exception { }

    public partial class PSConsoleReadLine : IPSConsoleReadLineMockableMethods
    {
        private static readonly PSConsoleReadLine _singleton;
        private bool _delayedOneTimeInitCompleted;

        private IPSConsoleReadLineMockableMethods _mockableMethods;

        private EngineIntrinsics _engineIntrinsics;
        private static readonly GCHandle _breakHandlerGcHandle;
        private Thread _readKeyThread;
        private AutoResetEvent _readKeyWaitHandle;
        private AutoResetEvent _keyReadWaitHandle;
        private AutoResetEvent _closingWaitHandle;
        private WaitHandle[] _waitHandles;
        private bool _captureKeys;
        private readonly Queue<ConsoleKeyInfo> _savedKeys;
        private uint _prePSReadlineConsoleMode;

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

        // Save a fixed # of keys so we can reconstruct a repro after a crash
        private readonly static HistoryQueue<ConsoleKeyInfo> _lastNKeys;

        // Tokens etc.
        private Token[] _tokens;
        private Ast _ast;
        private ParseError[] _parseErrors;

        [ExcludeFromCodeCoverage]
        ConsoleKeyInfo IPSConsoleReadLineMockableMethods.ReadKey()
        {
            var key = Console.ReadKey(true);
            _lastNKeys.Enqueue(key);
            return key;
        }

        [ExcludeFromCodeCoverage]
        bool IPSConsoleReadLineMockableMethods.KeyAvailable()
        {
            return Console.KeyAvailable;
        }

        private void ReadKeyThreadProc()
        {
            var stopwatch = new Stopwatch();
            while (true)
            {
                // Wait until ReadKey tells us to read a key.
                _readKeyWaitHandle.WaitOne();

                stopwatch.Restart();
                while (_mockableMethods.KeyAvailable())
                {
                    _queuedKeys.Enqueue(_mockableMethods.ReadKey());
                    if (stopwatch.ElapsedMilliseconds > 2)
                    {
                        // Don't spend too long in this loop if there are lots of queued keys
                        break;
                    }
                }

                if (_queuedKeys.Count == 0)
                {
                    var key = _mockableMethods.ReadKey();
                    _queuedKeys.Enqueue(key);
                }

                // One or more keys were read - let ReadKey know we're done.
                _keyReadWaitHandle.Set();
            }
        }

        private static ConsoleKeyInfo ReadKey()
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
            PowerShell ps = null;

            try
            {
                while (true)
                {
                    // Next, wait for one of three things:
                    //   - a key is pressed
                    //   - the console is exiting
                    //   - 300ms - to process events if we're idle

                    handleId = WaitHandle.WaitAny(_singleton._waitHandles, 300);
                    if (handleId != WaitHandle.WaitTimeout)
                        break;

                    // If we timed out, check for event subscribers (which is just
                    // a hint that there might be an event waiting to be processed.)
                    var eventSubscribers = _singleton._engineIntrinsics.Events.Subscribers;
                    if (eventSubscribers.Count > 0)
                    {
                        bool runPipelineForEventProcessing = false;
                        foreach (var sub in eventSubscribers)
                        {
                            if (sub.SourceIdentifier.Equals("PowerShell.OnIdle", StringComparison.OrdinalIgnoreCase))
                            {
                                // There is an OnIdle event.  We're idle because we timed out.  Normally
                                // PowerShell generates this event, but PowerShell assumes the engine is not
                                // idle because it called PSConsoleHostReadline which isn't returning.
                                // So we generate the event intstead.
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
                                ps = PowerShell.Create(RunspaceMode.CurrentRunspace);
                                ps.AddScript("0");
                            }

                            // To detect output during possible event processing, see if the cursor moved
                            // and rerender if so.
                            var y = Console.CursorTop;
                            ps.Invoke();
                            if (y != Console.CursorTop)
                            {
                                _singleton._initialY = Console.CursorTop;
                                _singleton.Render();
                            }
                        }
                    }
                }
            }
            finally
            {
                if (ps != null) { ps.Dispose(); }
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
            if (_singleton._captureKeys)
            {
                _singleton._savedKeys.Enqueue(key);
            }
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

        private bool BreakHandler(ConsoleBreakSignal signal)
        {
            if (signal == ConsoleBreakSignal.Close || signal == ConsoleBreakSignal.Shutdown)
            {
                // Set the event so ReadKey throws an exception to unwind.
                _closingWaitHandle.Set();
            }

            return false;
        }

        /// <summary>
        /// Entry point - called from the PowerShell function PSConsoleHostReadline
        /// after the prompt has been displayed.
        /// </summary>
        /// <returns>The complete command line.</returns>
        public static string ReadLine(Runspace remoteRunspace = null, EngineIntrinsics engineIntrinsics = null)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Input);
            NativeMethods.GetConsoleMode(handle, out _singleton._prePSReadlineConsoleMode);
            try
            {
                // Clear a couple flags so we can actually receive certain keys:
                //     ENABLE_PROCESSED_INPUT - enables Ctrl+C
                //     ENABLE_LINE_INPUT - enables Ctrl+S
                NativeMethods.SetConsoleMode(handle,
                    _singleton._prePSReadlineConsoleMode & ~(NativeMethods.ENABLE_PROCESSED_INPUT | NativeMethods.ENABLE_LINE_INPUT));

                _singleton.Initialize(remoteRunspace, engineIntrinsics);
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
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(PSReadLineResources.OopsAnErrorMessage1);
                Console.ForegroundColor = oldColor;
                var sb = new StringBuilder();
                for (int i = 0; i < _lastNKeys.Count; i++)
                {
                    sb.Append(' ');
                    sb.Append(_lastNKeys[i].ToGestureString());

                    KeyHandler handler;
                    if (_singleton._dispatchTable.TryGetValue(_lastNKeys[i], out handler) &&
                        "AcceptLine".Equals(handler.BriefDescription, StringComparison.OrdinalIgnoreCase))
                    {
                        // Make it a little easier to see the keys
                        sb.Append('\n');
                    }
                    // TODO: print non-default function bindings and script blocks
                }

                Console.WriteLine(PSReadLineResources.OopsAnErrorMessage2, _lastNKeys.Count, sb, e);
                var lineBeforeCrash = _singleton._buffer.ToString();
                _singleton.Initialize(remoteRunspace, _singleton._engineIntrinsics);
                InvokePrompt();
                Insert(lineBeforeCrash);
                return _singleton.InputLoop();
            }
            finally
            {
                NativeMethods.SetConsoleMode(handle, _singleton._prePSReadlineConsoleMode);
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
                    return MaybeAddToHistory(_buffer.ToString(), _edits, _undoEditIndex, readingHistoryFile: false);
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

        T CalloutUsingDefaultConsoleMode<T>(Func<T> func)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Input);
            uint psReadlineConsoleMode;
            NativeMethods.GetConsoleMode(handle, out psReadlineConsoleMode);
            try
            {
                NativeMethods.SetConsoleMode(handle, _prePSReadlineConsoleMode);
                return func();
            }
            finally
            {
                NativeMethods.SetConsoleMode(handle, psReadlineConsoleMode);
            }
        }

        void CalloutUsingDefaultConsoleMode(Action action)
        {
            CalloutUsingDefaultConsoleMode<object>(() => { action(); return null; });
        }

        void ProcessOneKey(ConsoleKeyInfo key, Dictionary<ConsoleKeyInfo, KeyHandler> dispatchTable, bool ignoreIfNoAction, object arg)
        {
            KeyHandler handler;
            if (!dispatchTable.TryGetValue(key, out handler))
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
                _renderForDemoNeeded = _demoMode;

                if (handler.ScriptBlock != null)
                {
                    CalloutUsingDefaultConsoleMode(() => handler.Action(key, arg));
                }
                else
                {
                    handler.Action(key, arg);
                }

                if (_renderForDemoNeeded)
                {
                    Render();
                }
            }
            else if (!ignoreIfNoAction && key.KeyChar != 0)
            {
                SelfInsert(key, arg);
            }
        }

        static PSConsoleReadLine()
        {
            _singleton = new PSConsoleReadLine();

            _breakHandlerGcHandle = GCHandle.Alloc(new BreakHandler(_singleton.BreakHandler));
            NativeMethods.SetConsoleCtrlHandler((BreakHandler) _breakHandlerGcHandle.Target, true);
            _singleton._readKeyThread = new Thread(_singleton.ReadKeyThreadProc) {IsBackground = true};
            _singleton._readKeyThread.Start();
            _singleton._readKeyWaitHandle = new AutoResetEvent(false);
            _singleton._keyReadWaitHandle = new AutoResetEvent(false);
            _singleton._closingWaitHandle = new AutoResetEvent(false);
            _singleton._waitHandles = new WaitHandle[] { _singleton._keyReadWaitHandle, _singleton._closingWaitHandle };

            // This is only used for post-mortem debugging - 200 keys should be enough to reconstruct most command lines.
            _lastNKeys = new HistoryQueue<ConsoleKeyInfo>(200);
        }

        private PSConsoleReadLine()
        {
            _mockableMethods = this;

            _captureKeys = false;
            _savedKeys = new Queue<ConsoleKeyInfo>();
            _demoStrings = new HistoryQueue<string>(100);
            _demoMode = false;

            SetDefaultWindowsBindings();

            _buffer = new StringBuilder(8 * 1024);
            _statusBuffer = new StringBuilder(256);
            _savedCurrentLine = new HistoryItem();
            _queuedKeys = new Queue<ConsoleKeyInfo>();

            string hostName = null;
            try
            {
                var ps = PowerShell.Create(RunspaceMode.CurrentRunspace)
                    .AddCommand("Get-Variable").AddParameter("Name", "host").AddParameter("ValueOnly");
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
            if (hostName == null)
            {
                hostName = "PSReadline";
            }
            _options = new PSConsoleReadlineOptions(hostName);
        }

        private void Initialize(Runspace remoteRunspace, EngineIntrinsics engineIntrinsics)
        {
            if (!_delayedOneTimeInitCompleted)
            {
                DelayedOneTimeInitialize();
                _delayedOneTimeInitCompleted = true;
            }

            _engineIntrinsics = engineIntrinsics;
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
            _initialX = Console.CursorLeft;
            _initialY = Console.CursorTop - Options.ExtraPromptLineCount;
            _initialBackgroundColor = Console.BackgroundColor;
            _initialForegroundColor = Console.ForegroundColor;
            _space = new CHAR_INFO(' ', _initialForegroundColor, _initialBackgroundColor);
            _bufferWidth = Console.BufferWidth;
            _killCommandCount = 0;
            _yankCommandCount = 0;
            _yankLastArgCommandCount = 0;
            _tabCommandCount = 0;
            _visualSelectionCommandCount = 0;
            _remoteRunspace = remoteRunspace;
            _statusIsErrorMessage = false;

            _consoleBuffer = ReadBufferLines(_initialY, 1 + Options.ExtraPromptLineCount);
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

            _historyFileMutex = new Mutex(false, GetHistorySaveFileMutexName());

            _history = new HistoryQueue<HistoryItem>(Options.MaximumHistoryCount);
            _currentHistoryIndex = 0;

            ReadHistoryFile();

            _killIndex = -1; // So first add indexes 0.
            _killRing = new List<string>(Options.MaximumKillRingCount);
        }

        private static void Chord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                throw new ArgumentNullException("key");
            }

            Dictionary<ConsoleKeyInfo, KeyHandler> secondKeyDispatchTable;
            if (_singleton._chordDispatchTable.TryGetValue(key.Value, out secondKeyDispatchTable))
            {
                if (_singleton._demoMode)
                {
                    // Render so the first key of the chord appears in the demo window
                    _singleton.Render();
                }
                var secondKey = ReadKey();
                _singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, ignoreIfNoAction: true, arg: arg);
            }
        }

        private static void Ignore(ConsoleKeyInfo? key = null, object arg = null)
        {
        }

        private static void ExecuteOnSTAThread(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                throw exception;
            }
        }

        #region Miscellaneous bindable functions

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
                KeyHandler handler;
                if (_singleton._dispatchTable.TryGetValue(nextKey, out handler) && handler.Action == DigitArgument)
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

                int intArg;
                if (int.TryParse(argBuffer.ToString(), out intArg))
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
            var currentBuffer = _singleton._buffer.ToString();
            var currentPos = _singleton._current;
            _singleton._buffer.Clear();
            _singleton._current = 0;
            for (int i = 0; i < _singleton._consoleBuffer.Length; i++)
            {
                _singleton._consoleBuffer[i].UnicodeChar = ' ';
                _singleton._consoleBuffer[i].ForegroundColor = Console.ForegroundColor;
                _singleton._consoleBuffer[i].BackgroundColor = Console.BackgroundColor;
            }
            _singleton.Render();
            Console.CursorLeft = 0;
            Console.CursorTop = _singleton._initialY;

            string newPrompt;
            using (var ps = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                ps.AddCommand("prompt");
                var result = ps.Invoke<string>();
                newPrompt = result.Count == 1 ? result[0] : "PS>";
            }
            Console.Write(newPrompt);

            _singleton._initialX = Console.CursorLeft;
            _singleton._consoleBuffer = ReadBufferLines(_singleton._initialY, 1 + _singleton.Options.ExtraPromptLineCount);
            _singleton._buffer.Append(currentBuffer);
            _singleton._current = currentPos;
            _singleton.Render();
        }

        #endregion Miscellaneous bindable functions

    }
}
