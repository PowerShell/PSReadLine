using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        [DebuggerDisplay("{_line}")]
        class HistoryItem
        {
            public string _line;
            public List<EditItem> _edits;
            public int _undoEditIndex;
        }

        // History state
        private HistoryQueue<HistoryItem> _history;
        private Dictionary<string, int> _hashedHistory;
        private int _currentHistoryIndex;
        private int _getNextHistoryIndex;
        private int _searchHistoryCommandCount;
        private int _recallHistoryCommandCount;
        private string _searchHistoryPrefix;
        // When cycling through history, the current line (not yet added to history)
        // is saved here so it can be restored.
        private readonly HistoryItem _savedCurrentLine;

        private const string _forwardISearchPrompt = "fwd-i-search: ";
        private const string _backwardISearchPrompt = "bck-i-search: ";
        private const string _failedForwardISearchPrompt = "failed-fwd-i-search: ";
        private const string _failedBackwardISearchPrompt = "failed-bck-i-search: ";

        private string MaybeAddToHistory(string result, List<EditItem> edits, int undoEditIndex)
        {
            bool addToHistory = !string.IsNullOrWhiteSpace(result) && ((Options.AddToHistoryHandler == null) || Options.AddToHistoryHandler(result));
            if (addToHistory)
            {
                _history.Enqueue(new HistoryItem
                {
                    _line = result,
                    _edits = edits,
                    _undoEditIndex = undoEditIndex
                });
                _currentHistoryIndex = _history.Count;
            }
            if (_demoMode)
            {
                ClearDemoWindow();
            }
            _savedCurrentLine._line = null;
            _savedCurrentLine._edits = null;
            _savedCurrentLine._undoEditIndex = 0;
            return result;
        }

        /// <summary>
        /// Add a command to the history - typically used to restore
        /// history from a previous session.
        /// </summary>
        public static void AddToHistory(string command)
        {
            command = command.Replace("\r\n", "\n");
            _singleton.MaybeAddToHistory(command, new List<EditItem>(), 0);
        }

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
            string line;
            if (_currentHistoryIndex == _history.Count)
            {
                line = _savedCurrentLine._line;
                _edits = _savedCurrentLine._edits;
                _undoEditIndex = _savedCurrentLine._undoEditIndex;
            }
            else
            {
                line = _history[_currentHistoryIndex]._line;
                _edits = _history[_currentHistoryIndex]._edits;
                _undoEditIndex = _history[_currentHistoryIndex]._undoEditIndex;
            }
            _buffer.Clear();
            _buffer.Append(line);
            if (moveCursor)
            {
                _current = _buffer.Length;
            }
            else if (_current > _buffer.Length)
            {
                _current = _buffer.Length;
            }
            Render();
        }

        private void SaveCurrentLine()
        {
            if (_singleton._currentHistoryIndex == _history.Count)
            {
                _savedCurrentLine._line = _buffer.ToString();
                _savedCurrentLine._edits = _edits;
                _savedCurrentLine._undoEditIndex = _undoEditIndex;
            }
        }

        private void HistoryRecall(int direction)
        {
            int newHistoryIndex;
            if (Options.HistoryNoDuplicates)
            {
                if (_recallHistoryCommandCount == 0)
                {
                    _hashedHistory = new Dictionary<string, int>();
                }

                newHistoryIndex = _currentHistoryIndex;
                do
                {
                    newHistoryIndex = newHistoryIndex + direction;
                    var line = _history[newHistoryIndex]._line;
                    if (!_hashedHistory.ContainsKey(line))
                    {
                        _hashedHistory.Add(line, newHistoryIndex);
                        break;
                    }
                } while (newHistoryIndex >= 0 && newHistoryIndex < _history.Count);
            }
            else
            {
                newHistoryIndex = _currentHistoryIndex + direction;
            }
            _recallHistoryCommandCount += 1;
            if (newHistoryIndex >= 0 && newHistoryIndex < _history.Count)
            {
                _currentHistoryIndex = newHistoryIndex;
                UpdateFromHistory(moveCursor: true);
            }

        }

        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadline history.
        /// </summary>
        public static void PreviousHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            _singleton.HistoryRecall(-1);
        }

        /// <summary>
        /// Replace the current input with the 'next' item from PSReadline history.
        /// </summary>
        public static void NextHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            _singleton.HistoryRecall(+1);
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
                if (_history[i]._line.StartsWith(_searchHistoryPrefix, Options.HistoryStringComparison))
                {
                    _currentHistoryIndex = i;
                    UpdateFromHistory(moveCursor: Options.HistorySearchCursorMovesToEnd);
                    break;
                }
            }
        }

        /// <summary>
        /// Move to the first item in the history.
        /// </summary>
        public static void BeginningOfHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            _singleton._currentHistoryIndex = 0;
            _singleton.UpdateFromHistory(moveCursor: _singleton.Options.HistorySearchCursorMovesToEnd);
        }

        /// <summary>
        /// Move to the last item (the current input) in the history.
        /// </summary>
        public static void EndOfHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._currentHistoryIndex = _singleton._history.Count;
            _singleton.UpdateFromHistory(moveCursor: _singleton.Options.HistorySearchCursorMovesToEnd);
        }

        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadline history
        /// that matches the characters between the start and the input and the cursor.
        /// </summary>
        public static void HistorySearchBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            _singleton.HistorySearch(backward: true);
        }

        /// <summary>
        /// Replace the current input with the 'next' item from PSReadline history
        /// that matches the characters between the start and the input and the cursor.
        /// </summary>
        public static void HistorySearchForward(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            _singleton.HistorySearch(backward: false);
        }

        private void UpdateHistoryDuringInteractiveSearch(string toMatch, int direction, ref int searchFromPoint)
        {
            searchFromPoint += direction;
            while (searchFromPoint >= 0 && searchFromPoint < _history.Count)
            {
                var startIndex = _history[searchFromPoint]._line.IndexOf(toMatch, Options.HistoryStringComparison);
                if (startIndex >= 0)
                {
                    _statusLinePrompt = direction > 0 ? _forwardISearchPrompt : _backwardISearchPrompt;
                    _current = startIndex;
                    _emphasisStart = startIndex;
                    _emphasisLength = toMatch.Length;
                    _currentHistoryIndex = searchFromPoint;
                    UpdateFromHistory(moveCursor: Options.HistorySearchCursorMovesToEnd);
                    return;
                }
                searchFromPoint += direction;
            }

            // Make sure we're never more than 1 away from being in range so if they
            // reverse direction, the first time they reverse they are back in range.
            if (searchFromPoint < 0)
                searchFromPoint = -1;
            else if (searchFromPoint >= _history.Count)
                searchFromPoint = _history.Count;

            _emphasisStart = -1;
            _emphasisLength = 0;
            _statusLinePrompt = direction > 0 ? _failedForwardISearchPrompt : _failedBackwardISearchPrompt;
            Render();
        }

        private void InteractiveHistorySearchLoop(int direction)
        {
            var searchFromPoint = _currentHistoryIndex;
            var searchPositions = new Stack<int>();
            searchPositions.Push(_currentHistoryIndex);

            var toMatch = new StringBuilder(64);
            while (true)
            {
                var key = ReadKey();
                KeyHandler handler;
                _dispatchTable.TryGetValue(key, out handler);
                var function = handler != null ? handler.Action : null;
                if (function == ReverseSearchHistory)
                {
                    UpdateHistoryDuringInteractiveSearch(toMatch.ToString(), -1, ref searchFromPoint);
                }
                else if (function == ForwardSearchHistory)
                {
                    UpdateHistoryDuringInteractiveSearch(toMatch.ToString(), +1, ref searchFromPoint);
                }
                else if (function == BackwardDeleteChar || key == Keys.Backspace || key == Keys.CtrlH)
                {
                    if (toMatch.Length > 0)
                    {
                        toMatch.Remove(toMatch.Length - 1, 1);
                        _statusBuffer.Remove(_statusBuffer.Length - 2, 1);
                        searchPositions.Pop();
                        searchFromPoint = _currentHistoryIndex = searchPositions.Peek();
                        UpdateFromHistory(moveCursor: Options.HistorySearchCursorMovesToEnd);

                        // Prompt may need to have 'failed-' removed.
                        var toMatchStr = toMatch.ToString();
                        var startIndex = _buffer.ToString().IndexOf(toMatchStr, Options.HistoryStringComparison);
                        if (startIndex >= 0)
                        {
                            _statusLinePrompt = direction > 0 ? _forwardISearchPrompt : _backwardISearchPrompt;
                            _current = startIndex;
                            _emphasisStart = startIndex;
                            _emphasisLength = toMatch.Length;
                            Render();
                        }
                    }
                    else
                    {
                        Ding();
                    }
                }
                else if (key == Keys.Escape)
                {
                    // End search
                    break;
                }
                else if (function == Abort)
                {
                    // Abort search
                    EndOfHistory();
                    break;
                }
                else if (EndInteractiveHistorySearch(key, function))
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
                    break;
                }
                else
                {
                    toMatch.Append(key.KeyChar);
                    _statusBuffer.Insert(_statusBuffer.Length - 1, key.KeyChar);

                    var toMatchStr = toMatch.ToString();
                    var startIndex = _buffer.ToString().IndexOf(toMatchStr, Options.HistoryStringComparison);
                    if (startIndex < 0)
                    {
                        UpdateHistoryDuringInteractiveSearch(toMatchStr, direction, ref searchFromPoint);
                    }
                    else
                    {
                        _current = startIndex;
                        _emphasisStart = startIndex;
                        _emphasisLength = toMatch.Length;
                        Render();
                    }
                    searchPositions.Push(_currentHistoryIndex);
                }
            }
        }

        private static bool EndInteractiveHistorySearch(ConsoleKeyInfo key, Action<ConsoleKeyInfo?, object> function)
        {
            // Keys < ' ' are control characters
            if (key.KeyChar < ' ')
            {
                return true;
            }

            if ((key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0)
            {
                return true;
            }

            return false;
        }

        private void InteractiveHistorySearch(int direction)
        {
            SaveCurrentLine();

            // Add a status line that will contain the search prompt and string
            _statusLinePrompt = direction > 0 ? _forwardISearchPrompt : _backwardISearchPrompt;
            _statusBuffer.Append("_");

            Render(); // Render prompt
            InteractiveHistorySearchLoop(direction);

            // Remove our status line
            _statusBuffer.Clear();
            _statusLinePrompt = null;
            _emphasisStart = -1;
            _emphasisLength = 0;

#if FALSE
            int promptStart = _bufferWidth * Options.ExtraPromptLineCount;
            int promptWidth = _initialX;

            // Copy the prompt (ignoring the possible extra lines which we'll leave alone)
            var savedPrompt = new CHAR_INFO[promptWidth];
            Array.Copy(_consoleBuffer, promptStart, savedPrompt, 0, promptWidth);

            string newPrompt = "(reverse-i-search)`': ";
            _initialX = newPrompt.Length;
            int i, j;
            for (i = promptStart, j = 0; j < newPrompt.Length; i++, j++)
            {
                _consoleBuffer[i].UnicodeChar = newPrompt[j];
                _consoleBuffer[i].BackgroundColor = Console.BackgroundColor;
                _consoleBuffer[i].ForegroundColor = Console.ForegroundColor;
            }

            InteractiveHistorySearchLoop(direction);

            // Restore the original prompt
            _initialX = promptWidth;
            Array.Copy(savedPrompt, 0, _consoleBuffer, promptStart, savedPrompt.Length);
#endif

            Render();
        }

        /// <summary>
        /// Perform an incremental forward search through history
        /// </summary>
        public static void ForwardSearchHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.InteractiveHistorySearch(+1);
        }

        /// <summary>
        /// Perform an incremental backward search through history
        /// </summary>
        public static void ReverseSearchHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.InteractiveHistorySearch(-1);
        }
    }
}
