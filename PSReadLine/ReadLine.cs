using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;

namespace PSConsoleUtilities
{
    public enum EditMode
    {
        Windows,
        Emacs,
#if FALSE
        Vi,
#endif
    }

    public enum BellStyle
    {
        None,
        Visual,
        Audible
    }

    public enum TokenClassification
    {
        None,
        Comment,
        Keyword,
        String,
        Operator,
        Variable,
        Command,
        Parameter,
        Type,
        Number,
    }

    public class KeyHandler
    {
        public string Key { get; set; }
        public string BriefDescription { get; set; }
        public string LongDescription
        {
            get
            {
                var result = _longDescription;
                if (string.IsNullOrWhiteSpace(result))
                    result = PSReadLineResources.ResourceManager.GetString(BriefDescription + "Description");
                if (string.IsNullOrWhiteSpace(result))
                    result = BriefDescription;
                return result;
            }
            set { _longDescription = value; }
        }
        private string _longDescription;
    }

    public class PSConsoleReadLine
    {
        private static readonly PSConsoleReadLine _singleton;

        class KeyHandler
        {
            public Action Action;
            public string BriefDescription;
            public string LongDescription;
        }
        static KeyHandler MakeKeyHandler(Action action, string briefDescription, string longDescription = null)
        {
            return new KeyHandler
            {
                Action = action,
                BriefDescription = briefDescription,
                LongDescription = longDescription
            };
        }

        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _emacsKeyMap; 
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _emacsMetaMap; 
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _emacsCtrlXMap; 
        private static readonly Dictionary<ConsoleKeyInfo, KeyHandler> _cmdKeyMap;

        private Dictionary<ConsoleKeyInfo, KeyHandler> _dispatchTable;
        private Dictionary<ConsoleKeyInfo, KeyHandler> _dispatchCtrlXTable;
        private Dictionary<ConsoleKeyInfo, KeyHandler> _dispatchMetaTable;

        private readonly StringBuilder _buffer;
        private CHAR_INFO[] _consoleBuffer;
        private int _current;
        private int _mark;
        private bool _inputAccepted;
        private int _initialX;
        private int _initialY;
        private int _bufferWidth;
        private ConsoleColor _initialBackgroundColor;
        private ConsoleColor _initialForegroundColor;
        private CHAR_INFO _space;

        // History state
        private HistoryQueue<string> _history;
        private int _currentHistoryIndex;
        private int _searchHistoryCommandCount;
        private string _searchHistoryPrefix;

        // Yank/Kill state
        private readonly List<string> _killRing;
        private int _killIndex;
        private int _killCommandCount;
        private int _yankCommandCount;
        private int _yankStartPoint;

        // Tab completion state
        private int _tabCommandCount;
        private CommandCompletion _tabCompletions;

        // Tokens etc.
        private Token[] _tokens;
        private Ast _ast;
        private ParseError[] _parseErrors;

#region Configuration options

        private readonly ConsoleColor[] _tokenForegroundColors;
        private readonly ConsoleColor[] _tokenBackgroundColors;

        public static readonly ConsoleColor DefaultTokenForegroundColor     = Console.ForegroundColor;
        public static readonly ConsoleColor DefaultTokenBackgroundColor     = Console.BackgroundColor;

        public const ConsoleColor DefaultCommentForegroundColor   = ConsoleColor.DarkGreen;
        public const ConsoleColor DefaultKeywordForegroundColor   = ConsoleColor.Green;
        public const ConsoleColor DefaultStringForegroundColor    = ConsoleColor.DarkCyan;
        public const ConsoleColor DefaultOperatorForegroundColor  = ConsoleColor.DarkGray;
        public const ConsoleColor DefaultVariableForegroundColor  = ConsoleColor.Green;
        public const ConsoleColor DefaultCommandForegroundColor   = ConsoleColor.Yellow;
        public const ConsoleColor DefaultParameterForegroundColor = ConsoleColor.DarkGray;
        public const ConsoleColor DefaultTypeForegroundColor      = ConsoleColor.Gray;
        public const ConsoleColor DefaultNumberForegroundColor    = ConsoleColor.White;

        private string _continuationPrompt;
        public const string DefaultContinuationPrompt = ">>> ";
        public static readonly ConsoleColor DefaultContinuationPromptForegroundColor = Console.ForegroundColor;
        public static readonly ConsoleColor DefaultContinuationPromptBackgroundColor = Console.BackgroundColor;
        private ConsoleColor _continuationPromptForegroundColor;
        private ConsoleColor _continuationPromptBackgroundColor;

        /// <summary>
        /// Prompts are typically 1 line, but sometimes they may span lines.  This
        /// count is used to make sure we can display the full prompt after showing
        /// ambiguous completions
        /// </summary>
        public const int DefaultExtraPromptLineCount = 0;
        private int _extraPromptLineCount;

        /// <summary>
        /// This delegate is called before adding a command line to the history.
        /// The command line is not added to the history if this delegate returns false.
        /// </summary>
        private Func<string, bool> _addToHistoryHandler;

        /// <summary>
        /// Commands shorter than MinimumHistoryCommandLength will not be added
        /// to the history.
        /// </summary>
        public const int DefaultMinimumHistoryCommandLength = 4;
        private int _minimumHistoryCommandLength;

        /// <summary>
        /// When true, duplicates will not be added to the history.
        /// </summary>
        public const bool DefaultHistoryNoDuplicates = false;
        private bool _historyNoDuplicates;

        /// <summary>
        /// The maximum number of commands to store in the history.
        /// </summary>
        public const int DefaultMaximumHistoryCount = 1024;
        private int _maximumHistoryCount;

        /// <summary>
        /// The maximum number of items to store in the kill ring.
        /// </summary>
        public const int DefaultMaximumKillRingCount = 10;
        private int _maximumKillRingCount;

        /// <summary>
        /// In Emacs, when searching history, the cursor doesn't move.
        /// In 4NT, the cursor moves to the end.  This option allows
        /// for either behavior.
        /// </summary>
        public const bool DefaultHistorySearchCursorMovesToEnd = false;
        private bool _historySearchCursorMovesToEnd;

        /// <summary>
        /// When displaying possible completions, either display
        /// tooltips or dipslay just the completions.
        /// </summary>
        public const bool DefaultShowToolTips = false;
        private bool _showToolTips;

        /// <summary>
        /// When ringing the bell, what frequency do we use?
        /// </summary>
        public const int DefaultDingTone = 1221;
        private int _dingTone;

        /// <summary>
        /// When ringing the bell, how long (in ms)?
        /// </summary>
        public const int DefaultDingDuration = 50;
        private int _dingDuration;

        /// <summary>
        /// When ringing the bell, what should be done?
        /// </summary>
        public const BellStyle DefaultBellStyle = BellStyle.Audible;
        private BellStyle _bellStyle;

        #endregion Configuration options

        #region Unit test only properties

        // These properties exist solely so the Fakes assembly has something
        // that can be used to access the private bits here.  It's annoying
        // to be so close to 100% coverage and not have 100% coverage!
        private CHAR_INFO[] ConsoleBuffer { get { return _consoleBuffer; } }

        #endregion Unit test only properties

        [ExcludeFromCodeCoverage]
        private static ConsoleKeyInfo ReadKey()
        {
            return Console.ReadKey(true);
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
            try
            {
                NativeMethods.GetConsoleMode(handle, out dwConsoleMode);
                NativeMethods.SetConsoleMode(handle, dwConsoleMode & ~NativeMethods.ENABLE_PROCESSED_INPUT);

                _singleton.Initialize();
                return _singleton.InputLoop();
            }
            finally
            {
                NativeMethods.GetConsoleMode(handle, out dwConsoleMode);
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

                var key = ReadKey();
                ProcessOneKey(key, _dispatchTable, ignoreIfNoAction: false);
                if (_inputAccepted)
                {
                    return MaybeAddToHistory();
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
            }
        }

        // Test hook.
        [ExcludeFromCodeCoverage]
        private static void PostKeyHandler()
        {
        }

        void ProcessOneKey(ConsoleKeyInfo key, Dictionary<ConsoleKeyInfo, KeyHandler> dispatchTable, bool ignoreIfNoAction)
        {
            KeyHandler handler;
            if (dispatchTable.TryGetValue(key, out handler))
            {
                handler.Action();
            }
            else if (!ignoreIfNoAction && key.KeyChar != 0)
            {
                Insert(key.KeyChar);
            }
            PostKeyHandler();
        }

        private string MaybeAddToHistory()
        {
            var result = _buffer.ToString();
            bool addToHistory = (result.Length >= _minimumHistoryCommandLength);
            if (addToHistory && _addToHistoryHandler != null)
            {
                addToHistory = _addToHistoryHandler(result);
            }
            if (addToHistory && _historyNoDuplicates)
            {
                // REVIEW: should history be case sensitive - it is now.
                // A smart comparer could use the ast to ignore case on commands, parameters,
                // operators and keywords while remaining case sensitive on command arguments.
                addToHistory = !_history.Contains(result);
            }
            if (addToHistory)
            {
                _history.Enqueue(result);
                _currentHistoryIndex = _history.Count;
            }
            return result;
        }

        static PSConsoleReadLine()
        {
            _cmdKeyMap = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.Enter,           MakeKeyHandler(AcceptLine,           "AcceptLine") },
                { Keys.ShiftEnter,      MakeKeyHandler(AddLine,              "AddLine") },
                { Keys.Escape,          MakeKeyHandler(RevertLine,           "RevertLine") },
                { Keys.LeftArrow,       MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.RightArrow,      MakeKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.CtrlLeftArrow,   MakeKeyHandler(BackwardWord,         "BackwordWord") },
                { Keys.CtrlRightArrow,  MakeKeyHandler(ForwardWord,          "ForwardWord") },
                { Keys.UpArrow,         MakeKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.DownArrow,       MakeKeyHandler(NextHistory,          "NextHistory") },
                { Keys.Home,            MakeKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.End,             MakeKeyHandler(EndOfLine,            "EndOfLine") },
                { Keys.Delete,          MakeKeyHandler(DeleteChar,           "DeleteChar") },
                { Keys.Backspace,       MakeKeyHandler(BackwardDeleteChar,   "BackwardDeleteChar") },
                { Keys.CtrlSpace,       MakeKeyHandler(PossibleCompletions,  "PossibleCompletions") },
                { Keys.Tab,             MakeKeyHandler(TabCompleteNext,      "TabCompleteNext") },
                { Keys.ShiftTab,        MakeKeyHandler(TabCompletePrevious,  "TabCompletePrevious") },
                { Keys.CtrlV,           MakeKeyHandler(Paste,                "Paste") },
                { Keys.VolumeDown,      MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeUp,        MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeMute,      MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.CtrlC,           MakeKeyHandler(CancelLine,           "CancelLine") },
            };

            _emacsKeyMap = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.Backspace,       MakeKeyHandler(BackwardDeleteChar,   "BackwardDeleteChar") },
                { Keys.Enter,           MakeKeyHandler(AcceptLine,           "AcceptLine") },
                { Keys.ShiftEnter,      MakeKeyHandler(AddLine,              "AddLine") },
                { Keys.LeftArrow,       MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.RightArrow,      MakeKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.UpArrow,         MakeKeyHandler(PreviousHistory,      "PreviousHistory") },
                { Keys.DownArrow,       MakeKeyHandler(NextHistory,          "NextHistory") },
                { Keys.Home,            MakeKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.End,             MakeKeyHandler(EndOfLine,            "EndOfLine") },
                { Keys.Escape,          MakeKeyHandler(EmacsMeta,            "EmacsMeta") },
                { Keys.Delete,          MakeKeyHandler(DeleteChar,           "DeleteChar") },
                { Keys.Tab,             MakeKeyHandler(Complete,             "Complete") },
                { Keys.CtrlA,           MakeKeyHandler(BeginningOfLine,      "BeginningOfLine") },
                { Keys.CtrlB,           MakeKeyHandler(BackwardChar,         "BackwardChar") },
                { Keys.CtrlC,           MakeKeyHandler(CancelLine,           "CancelLine") },
                { Keys.CtrlD,           MakeKeyHandler(DeleteChar,           "DeleteChar") },
                { Keys.CtrlE,           MakeKeyHandler(EndOfLine,            "EndOfLine") },
                { Keys.CtrlF,           MakeKeyHandler(ForwardChar,          "ForwardChar") },
                { Keys.CtrlH,           MakeKeyHandler(BackwardDeleteChar,   "BackwardDeleteChar") },
                { Keys.CtrlK,           MakeKeyHandler(KillLine,             "KillLine") },
                { Keys.CtrlM,           MakeKeyHandler(AcceptLine,           "AcceptLine") },
                { Keys.CtrlU,           MakeKeyHandler(BackwardKillLine,     "BackwardKillLine") },
                { Keys.CtrlX,           MakeKeyHandler(EmacsCtrlX,           "EmacsCtrlX") },
                { Keys.CtrlY,           MakeKeyHandler(Yank,                 "Yank") },
                { Keys.CtrlAt,          MakeKeyHandler(SetMark,              "SetMark") },
                { Keys.AltB,            MakeKeyHandler(EmacsBackwardWord,    "EmacsBackwardWord") },
                { Keys.AltD,            MakeKeyHandler(KillWord,             "KillWord") },
                { Keys.AltF,            MakeKeyHandler(EmacsForwardWord,     "EmacsForwardWord") },
                { Keys.AltR,            MakeKeyHandler(RevertLine,           "RevertLine") },
                { Keys.AltY,            MakeKeyHandler(YankPop,              "YankPop") },
                { Keys.AltBackspace,    MakeKeyHandler(KillBackwardWord,     "KillBackwardWord") },
                { Keys.AltEquals,       MakeKeyHandler(PossibleCompletions,  "PossibleCompletions") },
                { Keys.AltSpace,        MakeKeyHandler(SetMark,              "SetMark") },  // useless entry here for completeness - brings up system menu on Windows
                { Keys.VolumeDown,      MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeUp,        MakeKeyHandler(Ignore,               "Ignore") },
                { Keys.VolumeMute,      MakeKeyHandler(Ignore,               "Ignore") },
            };

            _emacsMetaMap = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.B,               MakeKeyHandler(EmacsBackwardWord,    "EmacsBackwardWord") },
                { Keys.D,               MakeKeyHandler(KillWord,             "KillWord") },
                { Keys.F,               MakeKeyHandler(EmacsForwardWord,     "EmacsForwardWord") },
                { Keys.R,               MakeKeyHandler(RevertLine,           "RevertLine") },
                { Keys.Y,               MakeKeyHandler(YankPop,              "YankPop") },
                { Keys.Backspace,       MakeKeyHandler(KillBackwardWord,     "KillBackwardWord") },
            };

            _emacsCtrlXMap = new Dictionary<ConsoleKeyInfo, KeyHandler>(new ConsoleKeyInfoComparer())
            {
                { Keys.Backspace,       MakeKeyHandler(BackwardKillLine,     "BackwardKillLine") },
                { Keys.CtrlX,           MakeKeyHandler(ExchangePointAndMark, "ExchangePointAndMark") },
            };

            _singleton = new PSConsoleReadLine();
        }

        private PSConsoleReadLine()
        {
            _dispatchTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(_cmdKeyMap);

            _buffer = new StringBuilder();

            _tokenForegroundColors = new ConsoleColor[(int)TokenClassification.Number + 1];
            _tokenBackgroundColors = new ConsoleColor[_tokenForegroundColors.Length];
            ResetColors();

            _continuationPrompt = DefaultContinuationPrompt;
            _continuationPromptForegroundColor = DefaultContinuationPromptForegroundColor;
            _continuationPromptBackgroundColor = DefaultContinuationPromptBackgroundColor;

            _minimumHistoryCommandLength = DefaultMinimumHistoryCommandLength;
            _addToHistoryHandler = null;
            _historyNoDuplicates = DefaultHistoryNoDuplicates;
            _maximumHistoryCount = DefaultMaximumHistoryCount;
            _history = new HistoryQueue<string>(_maximumHistoryCount);
            _currentHistoryIndex = 0;
            _historySearchCursorMovesToEnd = DefaultHistorySearchCursorMovesToEnd;

            _maximumKillRingCount = DefaultMaximumKillRingCount;
            _killIndex = -1;    // So first add indexes 0.
            _killRing = new List<string>(_maximumKillRingCount);

            _extraPromptLineCount = DefaultExtraPromptLineCount;
            _showToolTips = DefaultShowToolTips;

            _dingTone = DefaultDingTone;
            _dingDuration = DefaultDingDuration;
            _bellStyle = DefaultBellStyle;
        }

        private void ResetColors()
        {
            _tokenForegroundColors[(int)TokenClassification.None]      = DefaultTokenForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Comment]   = DefaultCommentForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Keyword]   = DefaultKeywordForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.String]    = DefaultStringForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Operator]  = DefaultOperatorForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Variable]  = DefaultVariableForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Command]   = DefaultCommandForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Parameter] = DefaultParameterForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Type]      = DefaultTypeForegroundColor;
            _tokenForegroundColors[(int)TokenClassification.Number]    = DefaultNumberForegroundColor;
            for (int i = 0; i < _tokenBackgroundColors.Length; i++)
            {
                _tokenBackgroundColors[i] = DefaultTokenBackgroundColor;
            }
        }

        private void Initialize()
        {
            _buffer.Clear();
            _current = 0;
            _mark = 0;
            _tokens = null;
            _parseErrors = null;
            _inputAccepted = false;
            _initialX = Console.CursorLeft;
            _initialY = Console.CursorTop - _extraPromptLineCount;
            _initialBackgroundColor = Console.BackgroundColor;
            _initialForegroundColor = Console.ForegroundColor;
            _space = new CHAR_INFO(' ', _initialForegroundColor, _initialBackgroundColor);
            _bufferWidth = Console.BufferWidth;
            _killCommandCount = 0;
            _yankCommandCount = 0;
            _tabCommandCount = 0;

            _consoleBuffer = ReadBufferLines(_initialY, 1 + _extraPromptLineCount);
        }

        private static void EmacsMeta()
        {
            var key = ReadKey();
            _singleton.ProcessOneKey(key, _singleton._dispatchMetaTable, ignoreIfNoAction: true);
        }

        private static void EmacsCtrlX()
        {
            var key = ReadKey();
            _singleton.ProcessOneKey(key, _singleton._dispatchCtrlXTable, ignoreIfNoAction: true);
        }

        private static void Ignore()
        {
        }

        private void RevertLine(bool supportUndo)
        {
            _buffer.Clear();
            _current = 0;
            Render();
        }

        /// <summary>
        /// Reverts all of the input to the current input.
        /// </summary>
        public static void RevertLine()
        {
            _singleton.RevertLine(supportUndo: true);
        }

        /// <summary>
        /// Cancel the current input, leaving the input on the screen,
        /// but returns back to the host so the prompt is evaluated again.
        /// </summary>
        public static void CancelLine()
        {
            // Clear out the input so it doesn't get executed, but
            // don't call render so the screen is left alone.
            _singleton._buffer.Clear();
            AcceptLine();
        }

        /// <summary>
        /// Delete the character before the cursor.
        /// </summary>
        public static void BackwardDeleteChar()
        {
            if (_singleton._buffer.Length > 0 && _singleton._current > 0)
            {
                _singleton._buffer.Remove(_singleton._current - 1, 1);
                _singleton._current--;
                _singleton.Render();
            }
        }

        /// <summary>
        /// Delete the character under the cursor.
        /// </summary>
        public static void DeleteChar()
        {
            if (_singleton._buffer.Length > 0 && _singleton._current < _singleton._buffer.Length)
            {
                _singleton._buffer.Remove(_singleton._current, 1);
                _singleton.Render();
            }
        }

        /// <summary>
        /// Attempt to execute the current input.  If the current input is incomplete (for
        /// example there is a missing closing parenthesis, bracket, or quote, then the
        /// continuation prompt is displayed on the next line and PSReadline waits for
        /// keys to edit the current input.
        /// </summary>
        public static void AcceptLine()
        {
            _singleton.ParseInput();
            if (_singleton._parseErrors.Any(e => e.IncompleteInput))
            {
                _singleton.Insert('\n');
                return;
            }
            // Make sure cursor is at the end before writing the line
            _singleton._current = _singleton._buffer.Length;
            _singleton.PlaceCursor();
            Console.Out.Write("\n");
            _singleton._inputAccepted = true;
        }

        /// <summary>
        /// The continuation prompt is displayed on the next line and PSReadline waits for
        /// keys to edit the current input.  This is useful to enter multi-line input as
        /// a single command even when a single line is complete input by itself.
        /// </summary>
        public static void AddLine()
        {
            _singleton.Insert('\n');
        }

        /// <summary>
        /// Paste text from the system clipboard.
        /// </summary>
        public static void Paste()
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                string textToPaste = System.Windows.Clipboard.GetText();
                textToPaste = textToPaste.Replace("\r", "");
                _singleton._buffer.Insert(_singleton._current, textToPaste);
                _singleton._current += textToPaste.Length;
                _singleton.Render();
            }
        }

#region Movement

        /// <summary>
        /// Move the cursor to the end of the input.
        /// </summary>
        public static void EndOfLine()
        {
            _singleton.MoveCursor(_singleton._buffer.Length);
        }

        /// <summary>
        /// Move the cursor to the end of the input.
        /// </summary>
        public static void BeginningOfLine()
        {
            _singleton.MoveCursor(0);
        }

        /// <summary>
        /// Move the cursor one character to the right.  This may move the cursor to the next
        /// line of multi-line input.
        /// </summary>
        public static void ForwardChar()
        {
            if (_singleton._current < _singleton._buffer.Length)
            {
                _singleton._current += 1;
                _singleton.PlaceCursor();
            }
        }

        /// <summary>
        /// Move the cursor one character to the left.  This may move the cursor to the previous
        /// line of multi-line input.
        /// </summary>
        public static void BackwardChar()
        {
            if (_singleton._current > 0 && (_singleton._current - 1 < _singleton._buffer.Length))
            {
                _singleton._current -= 1;
                _singleton.PlaceCursor();
            }
        }

        private void ForwardWord(EditMode mode)
        {
            var findTokenMode = mode == EditMode.Windows
                                    ? FindTokenMode.Next
                                    : FindTokenMode.CurrentOrNext;
            var token = FindToken(_current, findTokenMode);

            Debug.Assert(token != null, "We'll always find EOF");

            switch (mode)
            {
            case EditMode.Emacs:
                _current = token.Kind == TokenKind.EndOfInput
                    ? _buffer.Length
                    : token.Extent.EndOffset;
                break;
            case EditMode.Windows:
                _current = token.Kind == TokenKind.EndOfInput
                    ? _buffer.Length
                    : token.Extent.StartOffset;
                break;
            }
            PlaceCursor();
        }

        /// <summary>
        /// Move the cursor forward to the end of the current word, or if between words,
        /// to the end of the next word.
        /// </summary>
        public static void EmacsForwardWord()
        {
            _singleton.ForwardWord(EditMode.Emacs);
        }

        /// <summary>
        /// Move the cursor forward to the start of the next word.
        /// </summary>
        public static void ForwardWord()
        {
            _singleton.ForwardWord(EditMode.Windows);
        }

        private void BackwardWord(EditMode mode)
        {
            var token = _singleton.FindToken(_singleton._current, FindTokenMode.Previous);

            _singleton._current = (token != null) ? token.Extent.StartOffset : 0;
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// Move the cursor back to the start of the current word, or if between words,
        /// the start of the previous word.
        /// </summary>
        public static void BackwardWord()
        {
            _singleton.BackwardWord(EditMode.Windows);
        }

        /// <summary>
        /// Move the cursor back to the start of the current word, or if between words,
        /// the start of the previous word.
        /// </summary>
        public static void EmacsBackwardWord()
        {
            _singleton.BackwardWord(EditMode.Emacs);
        }

#endregion Movement

#region History

        /// <summary>
        /// Clears history in PSReadline.  This does not affect PowerShell history.
        /// </summary>
        public static void ClearHistory()
        {
            _singleton._history.Clear();
            _singleton._currentHistoryIndex = 0;
        }

        private void UpdateFromHistory(bool moveCursor)
        {
            _buffer.Clear();
            _buffer.Append(_history[_currentHistoryIndex]);
            if (moveCursor)
            {
                _current = _buffer.Length;
            }
            Render();
        }

        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadline history.
        /// </summary>
        public static void PreviousHistory()
        {
            if (_singleton._currentHistoryIndex > 0)
            {
                _singleton._currentHistoryIndex -= 1;
                _singleton.UpdateFromHistory(moveCursor: true);
            }
        }

        /// <summary>
        /// Replace the current input with the 'next' item from PSReadline history.
        /// </summary>
        public static void NextHistory()
        {
            if (_singleton._currentHistoryIndex < (_singleton._history.Count - 1))
            {
                _singleton._currentHistoryIndex += 1;
                _singleton.UpdateFromHistory(moveCursor: true);
            }
        }

        private void HistorySearch(bool backward)
        {
            if (_searchHistoryCommandCount == 0)
            {
                _searchHistoryPrefix = _buffer.ToString(0, _current);
            }
            _searchHistoryCommandCount += 1;

            int incr = backward ? -1 : +1;
            for (int i = _currentHistoryIndex + incr; i >=0 && i < _history.Count; i += incr)
            {
                if (_history[i].StartsWith(_searchHistoryPrefix))
                {
                    _currentHistoryIndex = i;
                    UpdateFromHistory(moveCursor: _historySearchCursorMovesToEnd);
                    break;
                }
            }
        }

        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadline history
        /// that matches the characters between the start and the input and the cursor.
        /// </summary>
        public static void HistorySearchBackward()
        {
            _singleton.HistorySearch(backward: true);
        }

        /// <summary>
        /// Replace the current input with the 'next' item from PSReadline history
        /// that matches the characters between the start and the input and the cursor.
        /// </summary>
        public static void HistorySearchForward()
        {
            _singleton.HistorySearch(backward: false);
        }

        /// <summary>
        /// Attempt to complete the text surrounding the cursor with the next
        /// available completion.
        /// </summary>
        public static void TabCompleteNext()
        {
            _singleton.Complete(forward: true);
        }

        /// <summary>
        /// Attempt to complete the text surrounding the cursor with the previous
        /// available completion.
        /// </summary>
        public static void TabCompletePrevious()
        {
            _singleton.Complete(forward: false);
        }

        private static bool IsSingleQuote(char c)
        {
            return c == '\'' || c == (char)8216 || c == (char)8217 || c == (char)8218;
        }

        private static bool IsDoubleQuote(char c)
        {
            return c == '"' || c == (char)8220 || c == (char)8221;
        }

        private static bool IsQuoted(string s)
        {
            if (s.Length >= 2)
            {
                var first = s[0];
                var last = s[s.Length - 1];

                return ((IsSingleQuote(first) && IsSingleQuote(last))
                        ||
                        (IsDoubleQuote(first) && IsDoubleQuote(last)));
            }
            return false;
        }

        private static string GetUnquotedText(string s, bool consistentQuoting)
        {
            if (!consistentQuoting && IsQuoted(s))
            {
                s = s.Substring(1, s.Length - 2);
            }
            return s;
        }

        /// <summary>
        /// Attempt to perform completion on the text surrounding the cursor.
        /// If there are multiple possible completions, the longest unambiguous
        /// prefix is used for completion.  If trying to complete the longest
        /// unambiguous completion, a list of possible completions is displayed.
        /// </summary>
        public static void Complete()
        {
            var completions = _singleton.GetCompletions();
            if (completions == null || completions.CompletionMatches.Count == 0)
                return;

            if (_singleton._tabCommandCount > 0)
            {
                if (completions.CompletionMatches.Count == 1)
                {
                    Ding();
                }
                else
                {
                    PossibleCompletions();
                }
                return;
            }

            // Find the longest unambiguous prefix.  This might be the empty
            // string, in which case we don't want to remove any of the users, instead
            // we'll immediately show possible completions.
            // For the purposes of unambiguous prefix, we'll ignore quotes if
            // some completions aren't quoted.
            var firstResult = completions.CompletionMatches[0];
            int quotedCompletions = completions.CompletionMatches.Count(match => IsQuoted(match.CompletionText));
            bool consistentQuoting =
                quotedCompletions == 0 ||
                (quotedCompletions == completions.CompletionMatches.Count &&
                 quotedCompletions == completions.CompletionMatches.Count(
                    m => m.CompletionText[0] == firstResult.CompletionText[0]));

            bool ambiguous = false;
            var replacementText = GetUnquotedText(firstResult.CompletionText, consistentQuoting);
            foreach (var match in completions.CompletionMatches.Skip(1)) 
            {
                var matchText = GetUnquotedText(match.CompletionText, consistentQuoting);
                for (int i = 0; i < replacementText.Length; i++)
                {
                    if (i == matchText.Length
                        || char.ToLowerInvariant(replacementText[i]) != char.ToLowerInvariant(matchText[i]))
                    {
                        ambiguous = true;
                        replacementText = replacementText.Substring(0, i);
                        break;
                    }
                }
                if (replacementText.Length == 0)
                {
                    break;
                }
            }

            if (replacementText.Length > 0)
            {
                _singleton.Replace(completions.ReplacementIndex, completions.ReplacementLength, replacementText);
                completions.ReplacementLength = replacementText.Length;

                if (ambiguous)
                {
                    Ding();
                }
            }
            else
            {
                // No common prefix, don't wait for a second tab, just show the possible completions
                // right away.
                PossibleCompletions();
            }

            _singleton._tabCommandCount += 1;
        }

        private CommandCompletion GetCompletions()
        {
            if (_tabCommandCount == 0)
            {
                try
                {
                    _tabCompletions = null;

                    // Could use the overload that takes an AST as it's faster (we've already parsed the
                    // input for coloring) but that overload is a little more complicated in passing in the
                    // cursor position.
                    var ps = PowerShell.Create(RunspaceMode.CurrentRunspace);
                    _tabCompletions = CommandCompletion.CompleteInput(_buffer.ToString(), _current, null, ps);

                    if (_tabCompletions.CompletionMatches.Count == 0)
                        return null;
                }
                catch (Exception)
                {
                }                
            }

            return _tabCompletions;
        }

        private void Complete(bool forward)
        {
            var completions = GetCompletions();
            if (completions == null)
                return;

            completions.CurrentMatchIndex += forward ? 1 : -1;
            if (completions.CurrentMatchIndex < 0)
            {
                completions.CurrentMatchIndex = completions.CompletionMatches.Count - 1;
            }
            else if (completions.CurrentMatchIndex == completions.CompletionMatches.Count)
            {
                completions.CurrentMatchIndex = 0;
            }

            var replacementText = completions.CompletionMatches[completions.CurrentMatchIndex].CompletionText;
            Replace(completions.ReplacementIndex, completions.ReplacementLength, replacementText);
            completions.ReplacementLength = replacementText.Length;
            _tabCommandCount += 1;
        }

        /// <summary>
        /// Display the list of possible completions.
        /// </summary>
        public static void PossibleCompletions()
        {
            var completions = _singleton.GetCompletions();
            if (completions == null || completions.CompletionMatches.Count == 0)
            {
                Ding();
                return;
            }

            // Don't overwrite any of the line - so move to first line after the end of our buffer.
            var coords = _singleton.ConvertOffsetToCoordinates(_singleton._buffer.Length);
            PlaceCursor(0, coords.Y + 1);

            var sb = new StringBuilder();
            var minColWidth = completions.CompletionMatches.Max(c => c.ListItemText.Length);
            minColWidth += 2;

            if (_singleton._showToolTips)
            {
                const string seperator = "- ";
                var maxTooltipWidth = Console.BufferWidth - minColWidth - seperator.Length;

                foreach (var match in completions.CompletionMatches)
                {
                    sb.Append(match.ListItemText);
                    var spacesNeeded = minColWidth - match.ListItemText.Length;
                    if (spacesNeeded > 0)
                        sb.Append(' ', spacesNeeded);
                    sb.Append(seperator);
                    var toolTip = match.ToolTip.Length <= maxTooltipWidth
                                      ? match.ToolTip
                                      : match.ToolTip.Substring(0, maxTooltipWidth);
                    sb.Append(toolTip.Trim());
                    WriteLine(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                // Use BufferWidth here instead of WindowWidth?  Probably shouldn't.
                var screenColumns = Console.WindowWidth;
                var displayColumns = Math.Max(1, screenColumns / minColWidth);
                var displayRows = (completions.CompletionMatches.Count + displayColumns - 1) / displayColumns;
                for (var row = 0; row < displayRows; row++)
                {
                    for (var col = 0; col < displayColumns; col++)
                    {
                        var index = row + (displayRows * col);
                        if (index >= completions.CompletionMatches.Count)
                            break;
                        var item = completions.CompletionMatches[index].ListItemText;
                        sb.Append(item);
                        sb.Append(' ', minColWidth - item.Length);
                    }
                    WriteLine(sb.ToString());
                    sb.Clear();
                }
            }

            _singleton._initialY = Console.CursorTop;
            _singleton.Render();
        }

#endregion History

        #region Kill/Yank

        /// <summary>
        /// Mark the current loction of the cursor for use in a subsequent editing command.
        /// </summary>
        public static void SetMark()
        {
            _singleton._mark = _singleton._current;
        }

        /// <summary>
        /// The cursor is placed at the location of the mark and the mark is moved
        /// to the location of the cursor.
        /// </summary>
        public static void ExchangePointAndMark()
        {
            var tmp = _singleton._mark;
            _singleton._mark = _singleton._current;
            _singleton._current = tmp;
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// The contents of the kill ring are cleared.
        /// </summary>
        public static void ClearKillRing()
        {
            _singleton._killRing.Clear();
            _singleton._killIndex = -1;    // So first add indexes 0.
        }

        private void Kill(int start, int length)
        {
            if (length > 0)
            {
                var killText = _buffer.ToString(start, length);
                _buffer.Remove(start, length);
                _current = start;
                Render();
                if (_killCommandCount > 0)
                {
                    _killRing[_killIndex] += killText;
                }
                else
                {
                    if (_killRing.Count < _maximumKillRingCount)
                    {
                        _killRing.Add(killText);
                        _killIndex = _killRing.Count - 1;
                    }
                    else
                    {
                        _killIndex += 1;
                        if (_killIndex == _killRing.Count)
                        {
                            _killIndex = 0;
                        }
                        _killRing[_killIndex] = killText;
                    }
                }
            }
            _killCommandCount += 1;
        }

        /// <summary>
        /// Clear the input from the cursor to the end of the input.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void KillLine()
        {
            _singleton.Kill(_singleton._current, _singleton._buffer.Length - _singleton._current);
        }

        /// <summary>
        /// Clear the input from the start of the input to the cursor.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void BackwardKillLine()
        {
            _singleton.Kill(0, _singleton._current);
        }

        /// <summary>
        /// Clear the input from the cursor to the end of the current word.  If the cursor
        /// is between words, the input is cleared from the cursor to the end of the next word.
        /// The cleared text is placed in the kill ring.
        /// </summary>
        public static void KillWord()
        {
            var token = _singleton.FindToken(_singleton._current, FindTokenMode.CurrentOrNext);
            var end = (token.Kind == TokenKind.EndOfInput)
                ? _singleton._buffer.Length 
                : token.Extent.EndOffset;
            _singleton.Kill(_singleton._current, end - _singleton._current);
        }

        /// <summary>
        /// Clear the input from the start of the current word to the cursor.  If the cursor
        /// is between words, the input is cleared from the start of the previous word to the
        /// cursor.  The cleared text is placed in the kill ring.
        /// </summary>
        public static void KillBackwardWord()
        {
            var token = _singleton.FindToken(_singleton._current, FindTokenMode.Previous);
            var start = token == null 
                ? 0
                : token.Extent.StartOffset;
            _singleton.Kill(start, _singleton._current - start);
        }

        private void YankImpl()
        {
            if (_killRing.Count == 0)
                return;

            // Starting a yank session, yank the last thing killed and
            // remember where we started.
            _mark = _yankStartPoint = _current;
            Insert(_killRing[_killIndex]);
            
            _yankCommandCount += 1;
        }

        /// <summary>
        /// Add the most recently killed text to the input.
        /// </summary>
        public static void Yank()
        {
            _singleton.YankImpl();
        }

        private void YankPopImpl()
        {
            if (_yankCommandCount == 0)
                return;

            _killIndex -= 1;
            if (_killIndex < 0)
            {
                _killIndex = _killRing.Count - 1;
            }
            var yankText = _killRing[_killIndex];
            Replace(_yankStartPoint, _current - _yankStartPoint, yankText);
            _yankCommandCount += 1;
        }

        /// <summary>
        /// If the previous operation was Yank or YankPop, replace the previously yanked
        /// text with the next killed text from the kill ring.
        /// </summary>
        public static void YankPop()
        {
            _singleton.YankPopImpl();
        }

#endregion Kill/Yank

        private enum FindTokenMode
        {
            CurrentOrNext,
            Next,
            Previous,
        }

        static bool OffsetWithinToken(int offset, Token token)
        {
           return offset < token.Extent.EndOffset && offset >= token.Extent.StartOffset;
        }

        private Token FindNestedToken(int offset, IList<Token> tokens, FindTokenMode mode)
        {
            Token token = null;
            bool foundNestedToken = false;
            int i;
            for (i = tokens.Count - 1; i >= 0; i--)
            {
                if (OffsetWithinToken(offset, tokens[i]))
                {
                    token = tokens[i];
                    var strToken = token as StringExpandableToken;
                    if (strToken != null && strToken.NestedTokens != null)
                    {
                        var nestedToken = FindNestedToken(offset, strToken.NestedTokens, mode);
                        if (nestedToken != null)
                        {
                            token = nestedToken;
                            foundNestedToken = true;
                        }
                    }
                    break;
                }
                if (offset >= tokens[i].Extent.EndOffset)
                {
                    break;
                }
            }

            switch (mode)
            {
            case FindTokenMode.CurrentOrNext:
                if (token == null && (i + 1) < tokens.Count)
                {
                    token = tokens[i + 1];
                }
                break;
            case FindTokenMode.Next:
                if (!foundNestedToken)
                {
                    // If there is no next token, return null (happens with nested
                    // tokens where there is no EOF/EOS token).
                    token = ((i + 1) < tokens.Count) ? tokens[i + 1] : null;
                }
                break;
            case FindTokenMode.Previous:
                if (token == null)
                {
                    if (i >= 0)
                    {
                        token = tokens[i];
                    }
                }
                else if (offset == token.Extent.StartOffset)
                {
                    token = i > 0 ? tokens[i - 1] : null;
                }
                break;
            }

            return token;
        }

        private Token FindToken(int current, FindTokenMode mode)
        {
            if (_tokens == null)
            {
                ParseInput();
            }

            return FindNestedToken(current, _tokens, mode);
        }

        private void MoveCursor(int offset)
        {
            _current = offset;
            PlaceCursor();
        }

        private void Insert(char c)
        {
            _buffer.Insert(_current, c);
            _current += 1;
            Render();
        }

        private void Insert(string s)
        {
            _buffer.Insert(_current, s);
            _current += s.Length;
            Render();
        }

        private void Replace(int start, int length, string replacement)
        {
            _buffer.Remove(start, length);
            _buffer.Insert(start, replacement);
            _current = start + replacement.Length;
            Render();
        }

#region Rendering

        private class SavedTokenState
        {
            internal Token[] Tokens { get; set; }
            internal int Index { get; set; }
            internal ConsoleColor BackgroundColor { get; set; }
            internal ConsoleColor ForegroundColor { get; set; }
        }

        private string ParseInput()
        {
            var text = _buffer.ToString();
            _ast = Parser.ParseInput(text, out _tokens, out _parseErrors);
            return text;
        }

        private void Render()
        {
            // This function is not very effecient when pasting large chunks of text
            // into the console.

            var text = ParseInput();

            int bufferLineCount = ConvertOffsetToCoordinates(text.Length).Y - _initialY + 1;
            int bufferWidth = Console.BufferWidth;
            if (_consoleBuffer.Length != bufferLineCount * bufferWidth)
            {
                var newBuffer = new CHAR_INFO[bufferLineCount * bufferWidth];
                Array.Copy(_consoleBuffer, newBuffer, _initialX + (_extraPromptLineCount * _bufferWidth));
                if (_consoleBuffer.Length > bufferLineCount * bufferWidth)
                {
                    // Need to erase the extra lines that we won't draw again
                    for (int i = bufferLineCount * bufferWidth; i < _consoleBuffer.Length; i++)
                    {
                        _consoleBuffer[i] = _space;
                    }
                    WriteBufferLines(_consoleBuffer, ref _initialY);
                }
                _consoleBuffer = newBuffer;
            }

            var tokenStack = new Stack<SavedTokenState>();
            tokenStack.Push(new SavedTokenState
            {
                Tokens          = _tokens,
                Index           = 0,
                BackgroundColor = _initialBackgroundColor,
                ForegroundColor = _initialForegroundColor
            });

            int j               = _initialX + (_bufferWidth * _extraPromptLineCount);
            var backgroundColor = _initialBackgroundColor;
            var foregroundColor = _initialForegroundColor;

            for (int i = 0; i < text.Length; i++)
            {
                // Figure out the color of the character - if it's in a token,
                // use the tokens color otherwise use the initial color.
                var state = tokenStack.Peek();
                var token = state.Tokens[state.Index];
                if (i == token.Extent.EndOffset)
                {
                    if (token == state.Tokens[state.Tokens.Length - 1])
                    {
                        tokenStack.Pop();
                        state = tokenStack.Peek();
                    }
                    foregroundColor = state.ForegroundColor;
                    backgroundColor = state.BackgroundColor;

                    token = state.Tokens[++state.Index];
                }

                if (i == token.Extent.StartOffset)
                {
                    foregroundColor = _tokenForegroundColors[(int)GetTokenClassification(token)];
                    backgroundColor = _tokenBackgroundColors[(int)GetTokenClassification(token)];

                    if (token.Kind == TokenKind.StringExpandable || token.Kind == TokenKind.HereStringExpandable)
                    {
                        // We might have nested tokens.
                        var stringToken = (StringExpandableToken)token;
                        if (stringToken.NestedTokens != null && stringToken.NestedTokens.Any())
                        {
                            var tokens = new Token[stringToken.NestedTokens.Count + 1];
                            stringToken.NestedTokens.CopyTo(tokens, 0);
                            // NestedTokens doesn't have an "EOS" token, so we use
                            // the string literal token for that purpose.
                            tokens[tokens.Length - 1] = stringToken;

                            tokenStack.Push(new SavedTokenState
                            {
                                Tokens          = tokens,
                                Index           = 0,
                                BackgroundColor = backgroundColor,
                                ForegroundColor = foregroundColor
                            });
                        }
                    }
                }

                if (text[i] == '\n')
                {
                    while ((j % bufferWidth) != 0)
                    {
                        _consoleBuffer[j++] = _space;
                    }

                    for (int k = 0; k < _continuationPrompt.Length; k++, j++)
                    {
                        _consoleBuffer[j].UnicodeChar = _continuationPrompt[k];
                        _consoleBuffer[j].ForegroundColor = _continuationPromptForegroundColor;
                        _consoleBuffer[j].BackgroundColor = _continuationPromptBackgroundColor;
                    }
                }
                else if (char.IsControl(text[i]))
                {
                    _consoleBuffer[j].UnicodeChar = '^';
                    _consoleBuffer[j].ForegroundColor = foregroundColor;
                    _consoleBuffer[j++].BackgroundColor = backgroundColor;
                    _consoleBuffer[j].UnicodeChar = (char)('@' + text[i]);
                    _consoleBuffer[j].ForegroundColor = foregroundColor;
                    _consoleBuffer[j++].BackgroundColor = backgroundColor;
                }
                else
                {
                    _consoleBuffer[j].UnicodeChar = text[i];
                    _consoleBuffer[j].ForegroundColor = foregroundColor;
                    _consoleBuffer[j++].BackgroundColor = backgroundColor;
                }
            }
            for (; j < _consoleBuffer.Length; j++)
            {
                _consoleBuffer[j] = _space;
            }

            bool rendered = false;
            if (_parseErrors.Length > 0)
            {
                int promptChar = _initialX - 1 + (_bufferWidth * _extraPromptLineCount);

                while (promptChar >= 0)
                {
                    if (char.IsSymbol((char)_consoleBuffer[promptChar].UnicodeChar))
                    {
                        ConsoleColor prevColor = _consoleBuffer[promptChar].ForegroundColor;
                        _consoleBuffer[promptChar].ForegroundColor = ConsoleColor.Red;
                        WriteBufferLines(_consoleBuffer, ref _initialY);
                        rendered = true;
                        _consoleBuffer[promptChar].ForegroundColor = prevColor;
                        break;
                    }
                    promptChar -= 1;
                }
            }

            if (!rendered)
            {
                WriteBufferLines(_consoleBuffer, ref _initialY);
            }

            PlaceCursor();
        }

        private static void WriteBufferLines(CHAR_INFO[] buffer, ref int top)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            int bufferWidth = Console.BufferWidth;
            int bufferLineCount = buffer.Length / bufferWidth;
            if ((top + bufferLineCount) > Console.BufferHeight)
            {
                var scrollCount = (top + bufferLineCount) - Console.BufferHeight;
                ScrollBuffer(scrollCount);
                top -= scrollCount;
            }
            var bufferSize = new COORD
            {
                X = (short) bufferWidth,
                Y = (short) bufferLineCount
            };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var writeRegion = new SMALL_RECT
            {
                Top = (short) top,
                Left = 0,
                Bottom = (short) (top + bufferLineCount - 1),
                Right = (short) bufferWidth
            };
            NativeMethods.WriteConsoleOutput(handle, buffer,
                                             bufferSize, bufferCoord, ref writeRegion);
        }

        private static CHAR_INFO[] ReadBufferLines(int top, int count)
        {
            var result = new CHAR_INFO[Console.BufferWidth * count];
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var readBufferSize = new COORD {
                X = (short)Console.BufferWidth,
                Y = (short)count};
            var readBufferCoord = new COORD {X = 0, Y = 0};
            var readRegion = new SMALL_RECT
            {
                Top = (short)top,
                Left = 0,
                Bottom = (short)(top + count),
                Right = (short)(Console.BufferWidth - 1)
            };
            NativeMethods.ReadConsoleOutput(handle, result,
                readBufferSize, readBufferCoord, ref readRegion);
            return result;
        }

        private TokenClassification GetTokenClassification(Token token)
        {
            switch (token.Kind)
            {
            case TokenKind.Comment:
                return TokenClassification.Comment;
            case TokenKind.Parameter:
                return TokenClassification.Parameter;
            case TokenKind.Variable:
            case TokenKind.SplattedVariable:
                return TokenClassification.Variable;
            case TokenKind.StringExpandable:
            case TokenKind.StringLiteral:
            case TokenKind.HereStringExpandable:
            case TokenKind.HereStringLiteral:
                return TokenClassification.String;
            case TokenKind.Number:
                return TokenClassification.Number;
            }

            if ((token.TokenFlags & TokenFlags.CommandName) != 0)
                return TokenClassification.Command;

            if ((token.TokenFlags & TokenFlags.Keyword) != 0)
                return TokenClassification.Keyword;

            if ((token.TokenFlags & (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator)) != 0)
                return TokenClassification.Operator;

            if ((token.TokenFlags & TokenFlags.TypeName) != 0)
                return TokenClassification.Type;

            return TokenClassification.None;
        }

        private static void ScrollBuffer(int lines)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var scrollRectangle = new SMALL_RECT
            {
                Top = (short) lines,
                Left = 0,
                Bottom = (short) (lines + Console.BufferHeight - 1),
                Right = (short)Console.BufferWidth
            };
            var destinationOrigin = new COORD {X = 0, Y = 0};
            var fillChar = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            NativeMethods.ScrollConsoleScreenBuffer(handle, ref scrollRectangle, ref scrollRectangle, destinationOrigin, ref fillChar);
        }

        private static void PlaceCursor(int x, int y)
        {
            if (y >= Console.BufferHeight)
            {
                ScrollBuffer(y - Console.BufferHeight + 1);
                y = Console.BufferHeight - 1;
            }
            Console.SetCursorPosition(x, y);
        }

        private void PlaceCursor()
        {
            var coordinates = ConvertOffsetToCoordinates(_current);
            PlaceCursor(coordinates.X, coordinates.Y);
        }

        private COORD ConvertOffsetToCoordinates(int offset)
        {
            int x = _initialX;
            int y = _initialY + _extraPromptLineCount;

            int bufferWidth = Console.BufferWidth;
            for (int i = 0; i < offset; i++)
            {
                if (_buffer[i] == '\n')
                {
                    y += 1;
                    x = _continuationPrompt.Length;
                }
                else
                {
                    x += char.IsControl(_buffer[i]) ? 2 : 1;
                    // Wrap?  No prompt when wrapping
                    if (x >= bufferWidth)
                    {
                        x -= bufferWidth;
                        y += 1;
                    }
                }
            }

            return new COORD {X = (short)x, Y = (short)y};
        }

        /// <summary>
        /// Notify the user based on their preference for notification.
        /// </summary>
        public static void Ding()
        {
            switch (_singleton._bellStyle)
            {
            case BellStyle.None:
                break;
            case BellStyle.Audible:
                Console.Beep(_singleton._dingTone, _singleton._dingDuration);
                break;
            case BellStyle.Visual:
                // TODO: flash prompt? command line?
                break;
            }
        }

        // Console.WriteLine works as expected in PowerShell but not in the unit test framework
        // so we use our own special (and limited) version as we don't need WriteLine much.
        // The unit test framework redirects stdout - so it would see Console.WriteLine calls.
        // Unfortunately, we are testing exact placement of characters on the screen, so redirection
        // doesn't work for us.
        static private void WriteLine(string s)
        {
            Debug.Assert(s.Length <= Console.BufferWidth);

            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var buffer = new CHAR_INFO[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                Debug.Assert(s[i] != '\n');
                buffer[i] = new CHAR_INFO(s[i], Console.ForegroundColor, Console.BackgroundColor);
            }

            var bufferSize = new COORD
                             {
                                 X = (short) s.Length,
                                 Y = 1
                             };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var writeRegion = new SMALL_RECT
                              {
                                  Top = (short) Console.CursorTop,
                                  Left = 0,
                                  Bottom = (short) Console.CursorTop,
                                  Right = (short) s.Length
                              };
            NativeMethods.WriteConsoleOutput(handle, buffer, bufferSize, bufferCoord, ref writeRegion);

            PlaceCursor(0, Console.CursorTop + 1);
        }

#endregion Rendering

        private void SetOptionsInternal(SetPSReadlineOption options)
        {
            if (options.ContinuationPrompt != null)
            {
                _continuationPrompt = options.ContinuationPrompt;
            }
            if (options._continuationPromptForegroundColor.HasValue)
            {
                _continuationPromptForegroundColor = options.ContinuationPromptForegroundColor;
            }
            if (options._continuationPromptBackgroundColor.HasValue)
            {
                _continuationPromptBackgroundColor = options.ContinuationPromptBackgroundColor;
            }
            if (options._historyNoDuplicates.HasValue)
            {
                _historyNoDuplicates = options.HistoryNoDuplicates;
                if (_historyNoDuplicates)
                {
                    var historyItems = new HashSet<string>();
                    var newHistory = new HistoryQueue<string>(_maximumHistoryCount);
                    while (_history.Count > 0)
                    {
                        var item = _history.Dequeue();
                        if (!historyItems.Contains(item))
                        {
                            newHistory.Enqueue(item);
                            historyItems.Add(item);
                        }
                    }
                    _history = newHistory;
                    _currentHistoryIndex = _history.Count;
                }
            }
            if (options._historySearchCursorMovesToEnd.HasValue)
            {
                _historySearchCursorMovesToEnd = options.HistorySearchCursorMovesToEnd;
            }
            if (options._addToHistoryHandlerSpecified)
            {
                _addToHistoryHandler = options.AddToHistoryHandler;
            }
            if (options._minimumHistoryCommandLength.HasValue)
            {
                _minimumHistoryCommandLength = options.MinimumHistoryCommandLength;
                var newHistory = new HistoryQueue<string>(_maximumHistoryCount);
                while (_history.Count > 0)
                {
                    var item = _history.Dequeue();
                    if (item.Length >= _minimumHistoryCommandLength)
                    {
                        newHistory.Enqueue(item);
                    }
                }
                _history = newHistory;
                _currentHistoryIndex = _history.Count;
            }
            if (options._maximumHistoryCount.HasValue)
            {
                _maximumHistoryCount = options.MaximumHistoryCount;
                var newHistory = new HistoryQueue<string>(_maximumHistoryCount);
                while (_history.Count > _maximumHistoryCount)
                {
                    _history.Dequeue();
                }
                while (_history.Count > 0)
                {
                    newHistory.Enqueue(_history.Dequeue());
                }
                _history = newHistory;
                _currentHistoryIndex = _history.Count;
            }
            if (options._maximumKillRingCount.HasValue)
            {
                _maximumKillRingCount = options.MaximumKillRingCount;
                // TODO - make _killRing smaller
            }
            if (options._editMode.HasValue)
            {
                switch (options._editMode)
                {
                case EditMode.Emacs:
                    _dispatchTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(_emacsKeyMap);
                    _dispatchCtrlXTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(_emacsCtrlXMap);
                    _dispatchMetaTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(_emacsMetaMap);
                    break;
#if FALSE
                case EditMode.Vi:
                    //TODO: _dispatchTable = _viKeyMap;
                    break;
#endif
                case EditMode.Windows:
                    _dispatchTable = new Dictionary<ConsoleKeyInfo, KeyHandler>(_cmdKeyMap);
                    break;
                }
            }
            if (options._showToolTips.HasValue)
            {
                _showToolTips = options.ShowToolTips;
            }
            if (options._extraPromptLineCount.HasValue)
            {
                _extraPromptLineCount = options.ExtraPromptLineCount;
            }
            if (options._dingTone.HasValue)
            {
                _dingTone = options.DingTone;
            }
            if (options._dingDuration.HasValue)
            {
                _dingDuration = options.DingDuration;
            }
            if (options._bellStyle.HasValue)
            {
                _bellStyle = options.BellStyle;
            }
            if (options.ResetTokenColors)
            {
                ResetColors();
            }
            if (options._tokenKind.HasValue)
            {
                if (options._foregroundColor.HasValue)
                {
                    _tokenForegroundColors[(int)options.TokenKind] = options.ForegroundColor;
                }
                if (options._backgroundColor.HasValue)
                {
                    _tokenBackgroundColors[(int)options.TokenKind] = options.BackgroundColor;
                }
            }
        }

        private void SetKeyHandlerInternal(ConsoleKeyInfo key, bool ctrlX, Action handler, string briefDescription, string longDescription)
        {
            var table = ctrlX ? _dispatchCtrlXTable : _dispatchTable;
            table[key] = MakeKeyHandler(handler, briefDescription, longDescription);
        }

        /// <summary>
        /// Helper function for the Set-PSReadlineOption cmdlet.
        /// </summary>
        public static void SetOptions(SetPSReadlineOption options)
        {
            _singleton.SetOptionsInternal(options);
        }

        /// <summary>
        /// Helper function for the Set-PSReadlineKeyHandler cmdlet.
        /// </summary>
        public static void SetKeyHandler(ConsoleKeyInfo key, bool ctrlX, Action handler, string briefDescription, string longDescription)
        {
            _singleton.SetKeyHandlerInternal(key, ctrlX, handler, briefDescription, longDescription);
        }

        /// <summary>
        /// Helper function for the Get-PSReadlineKeyHandler cmdlet.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<PSConsoleUtilities.KeyHandler> GetKeyHandlers()
        {
            foreach (var entry in _singleton._dispatchTable)
            {
                if (entry.Value.BriefDescription == "Ignore" || entry.Value.BriefDescription == "EmacsMeta")
                {
                    continue;
                }
                if (entry.Value.BriefDescription == "EmacsCtrlX")
                {
                    foreach (var xEntry in _singleton._dispatchCtrlXTable)
                    {
                        yield return new PSConsoleUtilities.KeyHandler
                        {
                            Key = "Ctrl+X," + xEntry.Key.ToGestureString(),
                            BriefDescription = xEntry.Value.BriefDescription,
                            LongDescription = xEntry.Value.LongDescription,
                        };
                    }
                }
                else
                {
                    yield return new PSConsoleUtilities.KeyHandler
                    {
                        Key = entry.Key.ToGestureString(),
                        BriefDescription = entry.Value.BriefDescription,
                        LongDescription = entry.Value.LongDescription,
                    };
                }
            }
        }

        public static void GetBufferState(out string input, out int cursor)
        {
            input = _singleton._buffer.ToString();
            cursor = _singleton._current;
        }

        public static void GetBufferState(out Ast ast, out Token[] tokens, out ParseError[] parseErrors, out int cursor)
        {
            _singleton.ParseInput();
            ast = _singleton._ast;
            tokens = _singleton._tokens;
            parseErrors = _singleton._parseErrors;
            cursor = _singleton._current;
        }

        private static void EnsureIsInitialized()
        {
            // The check that ConsoleBuffer is not null exists mostly
            // for unit tests that may execute this function before
            // the _singleton is initialized.
            if (_singleton.ConsoleBuffer == null)
                _singleton.Initialize();
        }

        /// <summary>
        /// Set the position of the cursor.
        /// </summary>
        public static void SetCursorPosition(int cursor)
        {
            EnsureIsInitialized();

            if (cursor > _singleton._buffer.Length)
            {
                cursor = _singleton._buffer.Length;
            }

            _singleton._current = cursor;
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// Set the text in the buffer and the position of the cursor.
        /// </summary>
        public static void SetBufferState(string input, int cursor)
        {
            if (input == null)
                return;

            EnsureIsInitialized();

            if (cursor > input.Length)
                cursor = input.Length;
            if (cursor < 0)
                cursor = 0;

            _singleton._buffer.Clear();
            _singleton._buffer.Append(input);
            _singleton._current = cursor;
            _singleton.Render();
        }
    }

    internal class ConsoleKeyInfoComparer : IEqualityComparer<ConsoleKeyInfo>
    {
        public bool Equals(ConsoleKeyInfo x, ConsoleKeyInfo y)
        {
            return x.Key == y.Key && x.KeyChar == y.KeyChar && x.Modifiers == y.Modifiers;
        }

        public int GetHashCode(ConsoleKeyInfo obj)
        {
            return obj.GetHashCode();
        }
    }

}
