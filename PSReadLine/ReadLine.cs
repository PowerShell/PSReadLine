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

    public class PSConsoleReadLine
    {
        private static readonly PSConsoleReadLine _singleton;

        private static readonly Dictionary<ConsoleKeyInfo, Action> _emacsKeyMap; 
        private static readonly Dictionary<ConsoleKeyInfo, Action> _emacsMetaMap; 
        private static readonly Dictionary<ConsoleKeyInfo, Action> _emacsCtrlXMap; 
        private static readonly Dictionary<ConsoleKeyInfo, Action> _cmdKeyMap;

        private Dictionary<ConsoleKeyInfo, Action> _dispatchTable;

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

        // Tokens
        private Token[] _tokens;
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
            _singleton.Initialize();
            return _singleton.InputLoop();
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

        void ProcessOneKey(ConsoleKeyInfo key, Dictionary<ConsoleKeyInfo, Action> dispatchTable, bool ignoreIfNoAction)
        {
            Action action;
            if (dispatchTable.TryGetValue(key, out action))
            {
                action();
            }
            else if (!ignoreIfNoAction
                && key.KeyChar != 0
                && (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0)
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
            _cmdKeyMap = new Dictionary<ConsoleKeyInfo, Action>(new ConsoleKeyInfoComparer())
            {
                { Keys.Enter,           AcceptLine },
                { Keys.ShiftEnter,      AddLine },
                { Keys.Escape,          RevertLine },
                { Keys.LeftArrow,       BackwardChar },
                { Keys.RightArrow,      ForwardChar },
                { Keys.CtrlLeftArrow,   BackwardWord },
                { Keys.CtrlRightArrow,  ForwardWord },
                { Keys.UpArrow,         PreviousHistory },
                { Keys.DownArrow,       NextHistory },
                { Keys.Home,            BeginningOfLine },
                { Keys.End,             EndOfLine },
                { Keys.Delete,          DeleteChar },
                { Keys.Backspace,       BackwardDeleteChar },
                { Keys.Tab,             TabCompleteNext },
                { Keys.ShiftTab,        TabCompletePrevious },
                { Keys.VolumeDown,      Ignore },
                { Keys.VolumeUp,        Ignore },
                { Keys.VolumeMute,      Ignore },
            };

            _emacsKeyMap = new Dictionary<ConsoleKeyInfo, Action>(new ConsoleKeyInfoComparer())
            {
                { Keys.Backspace,       BackwardDeleteChar },
                { Keys.Enter,           AcceptLine },
                { Keys.ShiftEnter,      AddLine },
                { Keys.LeftArrow,       BackwardChar },
                { Keys.RightArrow,      ForwardChar },
                { Keys.UpArrow,         PreviousHistory },
                { Keys.DownArrow,       NextHistory },
                { Keys.Home,            BeginningOfLine },
                { Keys.End,             EndOfLine },
                { Keys.Escape,          EmacsMeta },
                { Keys.Delete,          DeleteChar },
                { Keys.Tab,             Complete},
                { Keys.CtrlA,           BeginningOfLine },
                { Keys.CtrlB,           BackwardChar },
                { Keys.CtrlD,           DeleteChar },
                { Keys.CtrlE,           EndOfLine },
                { Keys.CtrlF,           ForwardChar },
                { Keys.CtrlH,           BackwardDeleteChar },
                { Keys.CtrlK,           KillLine },
                { Keys.CtrlM,           AcceptLine },
                { Keys.CtrlU,           BackwardKillLine },
                { Keys.CtrlX,           EmacsCtrlX },
                { Keys.CtrlY,           Yank },
                { Keys.CtrlAt,          SetMark },
                { Keys.AltB,            EmacsBackwardWord },
                { Keys.AltD,            KillWord },
                { Keys.AltF,            EmacsForwardWord },
                { Keys.AltR,            RevertLine },
                { Keys.AltY,            YankPop },
                { Keys.AltBackspace,    KillBackwardWord },
                { Keys.AltEquals,       PossibleCompletions },
                { Keys.AltSpace,        SetMark },  // useless entry here for completeness - brings up system menu on Windows
                { Keys.VolumeDown,      Ignore },
                { Keys.VolumeUp,        Ignore },
                { Keys.VolumeMute,      Ignore },
            };

            _emacsMetaMap = new Dictionary<ConsoleKeyInfo, Action>(new ConsoleKeyInfoComparer())
            {
                { Keys.B,               EmacsBackwardWord },
                { Keys.D,               KillWord },
                { Keys.F,               EmacsForwardWord },
                { Keys.R,               RevertLine },
                { Keys.Y,               YankPop },
                { Keys.Backspace,       KillBackwardWord },
            };

            _emacsCtrlXMap = new Dictionary<ConsoleKeyInfo, Action>(new ConsoleKeyInfoComparer())
            {
                { Keys.Backspace,       BackwardKillLine },
                { Keys.CtrlX,           ExchangePointAndMark },
            };

            _singleton = new PSConsoleReadLine();
        }

        private PSConsoleReadLine()
        {
            _dispatchTable = new Dictionary<ConsoleKeyInfo, Action>(_cmdKeyMap);

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
            // Most command lines will be about 1 line - we'll reallocate this buffer
            // later if necessary.
            _consoleBuffer = new CHAR_INFO[_bufferWidth * (1 + _extraPromptLineCount)];
            _killCommandCount = 0;
            _yankCommandCount = 0;
            _tabCommandCount = 0;

            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            // Read the prompt into the buffer just once - so we can avoid re-reading
            // it later.
            var widthToRead = _extraPromptLineCount > 0 ? _bufferWidth : _initialX;
            var readBufferSize = new COORD {X = (short)widthToRead, Y = (short)(1 + _extraPromptLineCount)};
            var readBufferCoord = new COORD {X = 0, Y = 0};
            var readRegion = new SMALL_RECT
            {
                Top = (short)_initialY,
                Left = 0,
                Bottom = (short)(_initialY + _extraPromptLineCount),
                Right = (short)(widthToRead - 1)
            };
            NativeMethods.ReadConsoleOutput(handle, _consoleBuffer,
                readBufferSize, readBufferCoord, ref readRegion);
        }

        private static void EmacsMeta()
        {
            var key = ReadKey();
            _singleton.ProcessOneKey(key, _emacsMetaMap, ignoreIfNoAction: true);
        }

        private static void EmacsCtrlX()
        {
            var key = ReadKey();
            _singleton.ProcessOneKey(key, _emacsCtrlXMap, ignoreIfNoAction: true);
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
        /// Undo all changes made to this line. This is like executing the undo command enough
        /// times to get back to the beginning.
        /// </summary>
        public static void RevertLine()
        {
            _singleton.RevertLine(supportUndo: true);
        }

        /// <summary>
        /// Delete the character behind the cursor. A numeric argument means to kill the
        /// characters instead of deleting them.
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
        /// Delete the character at point. If point is at the beginning of the line, there are
        /// no characters in the line, and the last character typed was not bound to delete-char,
        /// then return EOF.
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
        /// Attempt to commit the input.  If the input is incomplete (according to the PowerShell
        /// parser), then just add a newline and keep accepting more input.
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
        /// Add a newline without attempting to accept the input.
        /// </summary>
        public static void AddLine()
        {
            _singleton.Insert('\n');
        }

#region Movement

        /// <summary>
        /// Move to the end of the line.
        /// </summary>
        public static void EndOfLine()
        {
            _singleton.MoveCursor(_singleton._buffer.Length);
        }

        /// <summary>
        /// Move to the start of the current line.
        /// </summary>
        public static void BeginningOfLine()
        {
            _singleton.MoveCursor(0);
        }

        /// <summary>
        /// Move forward a character.
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
        /// Move back a character.
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
        /// Move forward to the end of the next word. Words are composed of letters and digits.
        /// </summary>
        public static void EmacsForwardWord()
        {
            _singleton.ForwardWord(EditMode.Emacs);
        }

        /// <summary>
        /// Move forward to the end of the next word. Words are composed of letters and digits.
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
        /// Move back to the start of the current or previous word. Words are composed
        /// of letters and digits.
        /// </summary>
        public static void BackwardWord()
        {
            _singleton.BackwardWord(EditMode.Windows);
        }

        /// <summary>
        /// Move back to the start of the current or previous word. Words are composed
        /// of letters and digits.
        /// </summary>
        public static void EmacsBackwardWord()
        {
            _singleton.BackwardWord(EditMode.Emacs);
        }

#endregion Movement

#region History

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
        /// Move `back' through the history list, fetching the previous command.
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
        /// Move `forward' through the history list, fetching the next command.
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
        /// Search backward through the history for the string of characters between the start
        /// of the current line and the point. This is a non-incremental search. By default,
        /// this command is unbound.
        /// </summary>
        public static void HistorySearchBackward()
        {
            _singleton.HistorySearch(backward: true);
        }

        /// <summary>
        /// Search forward through the history for the string of characters between the start
        /// of the current line and the point. This is a non-incremental search. By default,
        /// this command is unbound.
        /// </summary>
        public static void HistorySearchForward()
        {
            _singleton.HistorySearch(backward: false);
        }

        /// <summary>
        /// Attempt to perform completion on the text surrounding the cursor.
        /// Completion is performed like PowerShell or cmd, completing using
        /// the next completion.
        /// </summary>
        public static void TabCompleteNext()
        {
            _singleton.Complete(forward: true);
        }

        /// <summary>
        /// Attempt to perform completion on the text surrounding the cursor.
        /// Completion is performed like PowerShell or cmd, completing using
        /// the previous completion.
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
        /// Completion is performed like bash.
        /// </summary>
        public static void Complete()
        {
            var completions = _singleton.GetCompletions();
            if (completions == null || completions.CompletionMatches.Count == 0)
                return;

            if (_singleton._tabCommandCount > 0)
            {
                PossibleCompletions();
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
                    if (char.ToLowerInvariant(replacementText[i]) != char.ToLowerInvariant(matchText[i]))
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
                    _singleton.Ding();
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

        public static void PossibleCompletions()
        {
            var completions = _singleton.GetCompletions();
            if (completions == null || completions.CompletionMatches.Count == 0)
            {
                _singleton.Ding();
                return;
            }

            // Don't overwrite any of the line - so move to first line after the end of our buffer.
            var coords = _singleton.ConvertOffsetToCoordinates(_singleton._buffer.Length);
            Console.CursorLeft = 0;
            Console.CursorTop = coords.Y + 1;

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

        public static void SetMark()
        {
            _singleton._mark = _singleton._current;
        }

        public static void ExchangePointAndMark()
        {
            var tmp = _singleton._mark;
            _singleton._mark = _singleton._current;
            _singleton._current = tmp;
            _singleton.PlaceCursor();
        }

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
        /// (C-k)
        /// Kill the text from point to the end of the line.
        /// </summary>
        public static void KillLine()
        {
            _singleton.Kill(_singleton._current, _singleton._buffer.Length - _singleton._current);
        }

        /// <summary>
        /// (C-x Rubout)
        /// Kill backward to the beginning of the line.
        /// </summary>
        public static void BackwardKillLine()
        {
            _singleton.Kill(0, _singleton._current);
        }

        /// <summary>
        /// (M-d)
        /// Kill from point to the end of the current word, or if between words, to the end
        /// of the next word. Word boundaries are the same as forward-word.
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
        /// (M-DEL)
        /// Kill the word behind point. Word boundaries are the same as backward-word.
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
            Parser.ParseInput(text, out _tokens, out _parseErrors);
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
                    RenderBufferToConsole();
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
                        RenderBufferToConsole();
                        rendered = true;
                        _consoleBuffer[promptChar].ForegroundColor = prevColor;
                        break;
                    }
                    promptChar -= 1;
                }
            }

            if (!rendered)
            {
                RenderBufferToConsole();
            }

            PlaceCursor();
        }

        private void RenderBufferToConsole()
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            int bufferWidth = Console.BufferWidth;
            int bufferLineCount = _consoleBuffer.Length / bufferWidth;
            var bufferSize = new COORD
                             {
                                 X = (short) bufferWidth,
                                 Y = (short) bufferLineCount
                             };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var writeRegion = new SMALL_RECT
                              {
                                  Top = (short) _initialY,
                                  Left = 0,
                                  Bottom = (short) (_initialY + bufferLineCount - 1),
                                  Right = (short) bufferWidth
                              };
            NativeMethods.WriteConsoleOutput(handle, _consoleBuffer,
                                                        bufferSize, bufferCoord, ref writeRegion);
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

        private void PlaceCursor()
        {
            var coordinates = ConvertOffsetToCoordinates(_current);
            Console.SetCursorPosition(coordinates.X, coordinates.Y);
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
                    x += 1;
                    // Wrap?  No prompt when wrapping
                    if (x == bufferWidth)
                    {
                        x = 0;
                        y += 1;
                    }
                }
            }

            return new COORD {X = (short)x, Y = (short)y};
        }

        private void Ding()
        {
            switch (_bellStyle)
            {
            case BellStyle.None:
                break;
            case BellStyle.Audible:
                Console.Beep(_dingTone, _dingDuration);
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

            Console.CursorLeft = 0;
            Console.CursorTop += 1;
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
                    _dispatchTable = new Dictionary<ConsoleKeyInfo, Action>(_emacsKeyMap);
                    break;
#if FALSE
                case EditMode.Vi:
                    //TODO: _dispatchTable = _viKeyMap;
                    break;
#endif
                case EditMode.Windows:
                    _dispatchTable = new Dictionary<ConsoleKeyInfo, Action>(_cmdKeyMap);
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

        private void SetKeyHandlerInternal(ConsoleKeyInfo key, Action handler)
        {
            _dispatchTable[key] = handler;
        }

        public static void SetOptions(SetPSReadlineOption options)
        {
            _singleton.SetOptionsInternal(options);
        }

        public static void SetKeyHandler(ConsoleKeyInfo key, Action handler)
        {
            _singleton.SetKeyHandlerInternal(key, handler);
        }

        public static void GetBufferState(out string input, out int cursor)
        {
            input = _singleton._buffer.ToString();
            cursor = _singleton._current;
        }

        public static void SetBufferState(string input, int cursor)
        {
            // Check ConsoleBuffer is somewhat arbitrary and this check exists
            // most for unit tests that may execute this function before
            // the _singleton is initialized.
            if (_singleton.ConsoleBuffer == null)
                _singleton.Initialize();

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
