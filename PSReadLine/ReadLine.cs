using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        private static readonly PSConsoleReadLine _singleton;

        private static readonly GCHandle _breakHandlerGcHandle;
        private Thread _readKeyThread;
        private AutoResetEvent _readKeyWaitHandle;
        private AutoResetEvent _keyReadWaitHandle;
        private AutoResetEvent _closingWaitHandle;
        private WaitHandle[] _waitHandles;
        private bool _captureKeys;
        private readonly Queue<ConsoleKeyInfo> _savedKeys; 

        private readonly StringBuilder _buffer;
        private readonly StringBuilder _statusBuffer;
        private string _statusLinePrompt;
        private List<EditItem> _edits;
        private int _editGroupCount;
        private readonly Stack<int> _pushedEditGroupCount;
        private int _undoEditIndex;
        private int _mark;
        private bool _inputAccepted;
        private readonly Queue<ConsoleKeyInfo> _queuedKeys;
        private DateTime _lastRenderTime;

        // Tokens etc.
        private Token[] _tokens;
        private Ast _ast;
        private ParseError[] _parseErrors;

        #region Unit test only properties

        // These properties exist solely so the Fakes assembly has something
        // that can be used to access the private bits here.  It's annoying
        // to be so close to 100% coverage and not have 100% coverage!
        private CHAR_INFO[] ConsoleBuffer { get { return _consoleBuffer; } }

        #endregion Unit test only properties

        // For some reason nothing from System.Console is available via the Fakes assembly.
        // If we define a trivial wrapper, it can be faked, so we do.
        private static ConsoleKeyInfo ConsoleReadKey()
        {
            return Console.ReadKey(true);
        }

        private void ReadKeyThreadProc()
        {
            while (true)
            {
                // Wait until ReadKey tells us to read a key.
                _readKeyWaitHandle.WaitOne();

                var start = DateTime.Now;
                while (Console.KeyAvailable)
                {
                    _queuedKeys.Enqueue(ConsoleReadKey());
                    if ((DateTime.Now - start).Milliseconds > 2)
                    {
                        // Don't spend too long in this loop if there are lots of queued keys
                        break;
                    }
                }

                if (_queuedKeys.Count == 0)
                {
                    var key = ConsoleReadKey();
                    _queuedKeys.Enqueue(key);
                }

                // One or more keys were read - let ReadKey know we're done.
                _keyReadWaitHandle.Set();
            }
        }

        [ExcludeFromCodeCoverage]
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

            // Next, wait for one of two things - either a key is pressed on the console is exiting.
            int handleId = WaitHandle.WaitAny(_singleton._waitHandles);
            if (handleId == 1)
            {
                // The console is exiting - throw an exception to unwind the stack to the point
                // where we can return from ReadLine.
                throw new OperationCanceledException();
            }
            var key = _singleton._queuedKeys.Dequeue();
            if (_singleton._captureKeys)
            {
                _singleton._savedKeys.Enqueue(key);
            }
            return key;
        }

        private bool BreakHandler(ConsoleBreakSignal signal)
        {
            if (signal == ConsoleBreakSignal.Close || signal == ConsoleBreakSignal.Shutdown)
            {
                // Set the event so ReadKey throws an exception to unwind.
                _singleton._closingWaitHandle.Set();
            }

            return false;
        }

        /// <summary>
        /// Entry point - called from the PowerShell function PSConsoleHostReadline
        /// after the prompt has been displayed.
        /// </summary>
        /// <returns>The complete command line.</returns>
        public static string ReadLine()
        {
            uint dwConsoleMode;
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Input);
            NativeMethods.GetConsoleMode(handle, out dwConsoleMode);
            try
            {
                // Clear a couple flags so we can actually receive certain keys:
                //     ENABLE_PROCESSED_INPUT - enables Ctrl+C
                //     ENABLE_LINE_INPUT - enables Ctrl+S
                NativeMethods.SetConsoleMode(handle,
                    dwConsoleMode & ~(NativeMethods.ENABLE_PROCESSED_INPUT | NativeMethods.ENABLE_LINE_INPUT));

                _singleton.Initialize();
                return _singleton.InputLoop();
            }
            catch (OperationCanceledException)
            {
                // Console is exiting - return value isn't too critical - null or 'exit' could work equally well.
                return "";
            }
            finally
            {
                NativeMethods.SetConsoleMode(handle, dwConsoleMode);
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
                var yankLastArgCommandCount = _yankLastArgCommandCount;
                var visualSelectionCommandCount = _visualSelectionCommandCount;

                var key = ReadKey();
                ProcessOneKey(key, _dispatchTable, ignoreIfNoAction: false, arg: null);
                if (_inputAccepted)
                {
                    return MaybeAddToHistory(_buffer.ToString(), _edits, _undoEditIndex);
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
                    _searchHistoryCommandCount = 0;
                    _searchHistoryPrefix = null;
                }
                if (visualSelectionCommandCount == _visualSelectionCommandCount && _visualSelectionCommandCount > 0)
                {
                    _visualSelectionCommandCount = 0;
                    Render();  // Clears the visual selection
                }
            }
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

                handler.Action(key, arg);

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
            _singleton._readKeyThread = new Thread(_singleton.ReadKeyThreadProc);
            _singleton._readKeyThread.Start();
            _singleton._readKeyWaitHandle = new AutoResetEvent(false);
            _singleton._keyReadWaitHandle = new AutoResetEvent(false);
            _singleton._closingWaitHandle = new AutoResetEvent(false);
            _singleton._waitHandles = new WaitHandle[] { _singleton._keyReadWaitHandle, _singleton._closingWaitHandle };
        }

        private PSConsoleReadLine()
        {
            _captureKeys = false;
            _savedKeys = new Queue<ConsoleKeyInfo>();
            _demoStrings = new HistoryQueue<string>(100);
            _demoMode = false;

            SetDefaultWindowsBindings();

            _buffer = new StringBuilder(8 * 1024);
            _statusBuffer = new StringBuilder(256);
            _savedCurrentLine = new HistoryItem();
            _queuedKeys = new Queue<ConsoleKeyInfo>();

            _pushedEditGroupCount = new Stack<int>();

            _options = new PSConsoleReadlineOptions();

            _history = new HistoryQueue<HistoryItem>(Options.MaximumHistoryCount)
            {
                OnDequeue = HistoryOnDequeueHandler,
                OnEnqueue = HistoryOnEnqueueHandler
            };
            _currentHistoryIndex = 0;
            _hashedHistory = new HashSet<string>();

            _killIndex = -1;    // So first add indexes 0.
            _killRing = new List<string>(Options.MaximumKillRingCount);
        }

        private void Initialize()
        {
            _buffer.Clear();
            _edits = new List<EditItem>();
            _undoEditIndex = 0;
            _editGroupCount = 0;
            _pushedEditGroupCount.Clear();
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
            _tabCommandCount = 0;
            _visualSelectionCommandCount = 0;

            _consoleBuffer = ReadBufferLines(_initialY, 1 + Options.ExtraPromptLineCount);
            _lastRenderTime = DateTime.Now;

            if (_getNextHistoryIndex > 0)
            {
                _currentHistoryIndex = _getNextHistoryIndex;
                UpdateFromHistory(moveCursor: true);
                _getNextHistoryIndex = 0;
            }
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

            bool firstKeyAfterNegative = false;
            _singleton._statusLinePrompt = "digit-argument: ";
            var argBuffer = _singleton._statusBuffer;
            argBuffer.Append(key.Value.KeyChar);
            if (key.Value.KeyChar == '-')
            {
                argBuffer.Append('1');
                firstKeyAfterNegative = true;
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
                        if (firstKeyAfterNegative)
                        {
                            argBuffer.Remove(1, 1);
                            firstKeyAfterNegative = false;
                        }
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
            _singleton._statusLinePrompt = null;
            _singleton.Render(); // Render prompt
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

            var ps = PowerShell.Create(RunspaceMode.CurrentRunspace);
            ps.AddCommand("prompt");
            var result = ps.Invoke<string>();
            var strResult = result.Count == 1 ? result[0] : "PS> ";
            Console.Write(strResult);

            _singleton._consoleBuffer = ReadBufferLines(_singleton._initialY, 1 + _singleton.Options.ExtraPromptLineCount);
            _singleton._buffer.Append(currentBuffer);
            _singleton._current = currentPos;
            _singleton.Render();
        }

        #endregion Miscellaneous bindable functions

        private static void EnsureIsInitialized()
        {
            // The check that ConsoleBuffer is not null exists mostly
            // for unit tests that may execute this function before
            // the _singleton is initialized.
            if (_singleton.ConsoleBuffer == null)
                _singleton.Initialize();
        }
    }
}
