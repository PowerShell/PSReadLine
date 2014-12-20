using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Remembers last history search direction.
        /// </summary>
        private bool _searchHistoryBackward = true;

        private class ViCharacterSearcher
        {
            private char searchChar = '\0';
            private bool wasBackward = false;
            private bool wasBackoff = false;

            public static ViCharacterSearcher instance = new ViCharacterSearcher();

            public static bool IsRepeatable
            {
                get { return instance.searchChar != '\0'; }
            }

            public static char SearchChar
            {
                get { return instance.searchChar; }
            }

            public static bool WasBackward
            {
                get { return instance.wasBackward; }
            }

            public static bool WasBackoff
            {
                get { return instance.wasBackoff; }
            }

            public static void Set(char theChar, bool isBackward = false, bool isBackoff = false)
            {
                instance.searchChar = theChar;
                instance.wasBackward = isBackward;
                instance.wasBackoff = isBackoff;
            }


            public static void Search(char keyChar, object arg, bool backoff)
            {
                int qty = (arg is int) ? (int) arg : 1;

                for (int i = _singleton._current + 1; i < _singleton._buffer.Length; i++)
                {
                    if (_singleton._buffer[i] == keyChar)
                    {
                        qty -= 1;
                        if (qty == 0)
                        {
                            _singleton._current = backoff ? i - 1 : i;
                            _singleton.PlaceCursor();
                            return;
                        }
                    }
                }
                Ding();
            }

            public static void SearchBackward(char keyChar, object arg, bool backoff)
            {
                Set(keyChar, isBackward: true, isBackoff: backoff);
                int qty = (arg is int) ? (int) arg : 1;

                for (int i = _singleton._current - 1; i >= 0; i--)
                {
                    if (_singleton._buffer[i] == keyChar)
                    {
                        qty -= 1;
                        if (qty == 0)
                        {
                            _singleton._current = backoff ? i + 1 : i;
                            _singleton.PlaceCursor();
                            return;
                        }
                    }
                }
                Ding();
            }
        }

        /// <summary>
        /// Repeat the last recorded character search.
        /// </summary>
        public static void RepeatLastCharSearch(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!ViCharacterSearcher.IsRepeatable)
            {
                Ding();
                return;
            }

            if (ViCharacterSearcher.WasBackward)
            {
                ViCharacterSearcher.SearchBackward(ViCharacterSearcher.SearchChar, null, ViCharacterSearcher.WasBackoff);
            }
            else
            {
                ViCharacterSearcher.Search(ViCharacterSearcher.SearchChar, null, ViCharacterSearcher.WasBackoff);
            }
        }

        /// <summary>
        /// Repeat the last recorded character search, but in the opposite direction.
        /// </summary>
        public static void RepeatLastCharSearchBackwards(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!ViCharacterSearcher.IsRepeatable)
            {
                Ding();
                return;
            }

            if (ViCharacterSearcher.WasBackward)
            {
                ViCharacterSearcher.Search(ViCharacterSearcher.SearchChar, null, ViCharacterSearcher.WasBackoff);
            }
            else
            {
                ViCharacterSearcher.SearchBackward(ViCharacterSearcher.SearchChar, null, ViCharacterSearcher.WasBackoff);
            }
        }

        /// <summary>
        /// Read the next character and then find it, going forward, and then back off a character.
        /// This is for 't' functionality.
        /// </summary>
        public static void SearchChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            char keyChar = ReadKey().KeyChar;
            ViCharacterSearcher.Set(keyChar, isBackward: false, isBackoff: false);
            ViCharacterSearcher.Search(keyChar, arg, backoff: false);
        }

        /// <summary>
        /// Read the next character and then find it, going backard, and then back off a character.
        /// This is for 'T' functionality.
        /// </summary>
        public static void SearchCharBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            char keyChar = ReadKey().KeyChar;
            ViCharacterSearcher.Set(keyChar, isBackward: true, isBackoff: false);
            ViCharacterSearcher.SearchBackward(keyChar, arg, backoff: false);
        }

        /// <summary>
        /// Read the next character and then find it, going forward, and then back off a character.
        /// This is for 't' functionality.
        /// </summary>
        public static void SearchCharWithBackoff(ConsoleKeyInfo? key = null, object arg = null)
        {
            char keyChar = ReadKey().KeyChar;
            ViCharacterSearcher.Set(keyChar, isBackward: false, isBackoff: true);
            ViCharacterSearcher.Search(keyChar, arg, backoff: true);
        }

        /// <summary>
        /// Read the next character and then find it, going backard, and then back off a character.
        /// This is for 'T' functionality.
        /// </summary>
        public static void SearchCharBackwardWithBackoff(ConsoleKeyInfo? key = null, object arg = null)
        {
            char keyChar = ReadKey().KeyChar;
            ViCharacterSearcher.Set(keyChar, isBackward: true, isBackoff: true);
            ViCharacterSearcher.SearchBackward(keyChar, arg, backoff: true);
        }

        /// <summary>
        /// Find the start of the next word from the supplied location.
        /// Needed by VI.
        /// </summary>
        private int FindNextWordPointFrom(int cursor, string wordDelimiters)
        {
            int i = cursor;
            if (i == _singleton._buffer.Length)
            {
                return i;
            }

            if (InWord(i, wordDelimiters))
            {
                // Scan to end of current word region
                while (i < _singleton._buffer.Length)
                {
                    if (!InWord(i, wordDelimiters))
                    {
                        break;
                    }
                    i += 1;
                }
            }

            while (i < _singleton._buffer.Length)
            {
                if (InWord(i, wordDelimiters))
                {
                    break;
                }
                i += 1;
            }
            return i;
        }

        /// <summary>
        /// Find the beginning of the previous word from the supplied spot.
        /// </summary>
        private int FindPreviousWordPointFrom(int cursor, string wordDelimiters)
        {
            int i = cursor - 1;
            if (i < 0)
            {
                return 0;
            }

            if (!InWord(i, wordDelimiters))
            {
                // Scan backwards until we are at the end of the previous word.
                while (i > 0)
                {
                    if (InWord(i, wordDelimiters))
                    {
                        break;
                    }
                    i -= 1;
                }
            }
            while (i > 0)
            {
                if (!InWord(i, wordDelimiters))
                {
                    i += 1;
                    break;
                }
                i -= 1;
            }
            return i;
        }

        /// <summary>
        /// Insert the next key entered.
        /// </summary>
        private static void InsertCharacter(object arg = null)
        {
            ConsoleKeyInfo secondKey = ReadKey();
            _singleton.ProcessOneKey(secondKey, _viInsKeyMap, ignoreIfNoAction: false, arg: arg);
        }

        /// <summary>
        /// Delete to the end of the line.
        /// </summary>
        public static void DeleteToEnd(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current >= _singleton._buffer.Length)
            {
                Ding();
                return;
            }

            _singleton._clipboard = _singleton._buffer.ToString(_singleton._current, _singleton._buffer.Length - _singleton._current);
            _singleton.SaveEditItem(EditItemDelete.Create(
                _singleton._clipboard,
                _singleton._current,
                DeleteToEnd,
                arg
                ));
            _singleton._buffer.Remove(_singleton._current, _singleton._buffer.Length - _singleton._current);
            _singleton._current = _singleton._buffer.Length - 1;
            _singleton.Render();
        }

        /// <summary>
        /// Delete the next word.
        /// </summary>
        public static void DeleteWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = (arg is int) ? (int) arg : 1;
            int endPoint = _singleton._current;
            for (int i = 0; i < qty; i++)
            {
                endPoint = _singleton.FindNextWordPointFrom(endPoint, _singleton.Options.WordDelimiters);
            }

            if (endPoint <= _singleton._current)
            {
                Ding();
                return;
            }
            _singleton.SaveToClipboard(_singleton._current, endPoint - _singleton._current);
            _singleton.SaveEditItem(EditItemDelete.Create(
                _singleton._clipboard,
                _singleton._current,
                DeleteWord,
                arg
                ));
            _singleton._buffer.Remove(_singleton._current, endPoint - _singleton._current);
            if (_singleton._current >= _singleton._buffer.Length && _singleton._buffer.Length > 0)
            {
                _singleton._current = _singleton._buffer.Length - 1;
            }
            _singleton.Render();
        }

        /// <summary>
        /// Delete to the end of the word.
        /// </summary>
        public static void DeleteToEndOfWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = (arg is int) ? (int) arg : 1;
            int endPoint = _singleton._current;
            for (int i = 0; i < qty; i++)
            {
                endPoint = _singleton.FindNextWordEnd(_singleton.Options.WordDelimiters);
            }

            if (endPoint <= _singleton._current)
            {
                Ding();
                return;
            }
            _singleton.SaveToClipboard(_singleton._current, 1 + endPoint - _singleton._current);
            _singleton.SaveEditItem(EditItemDelete.Create(
                _singleton._clipboard,
                _singleton._current,
                DeleteToEndOfWord,
                arg
                ));
            _singleton._buffer.Remove(_singleton._current, 1 + endPoint - _singleton._current);
            if (_singleton._current >= _singleton._buffer.Length)
            {
                _singleton._current = _singleton._buffer.Length - 1;
            }
            _singleton.Render();
        }

        /// <summary>
        /// Ring the bell.
        /// </summary>
        private static void Ding(ConsoleKeyInfo? key = null, object arg = null)
        {
            Ding();
        }

        /// <summary>
        /// Switch the current operating mode from Vi-Insert to Vi-Command.
        /// </summary>
        public static void ViCommandMode(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._pushedEditGroupCount.Count > 0)
            {
                _singleton._groupUndoHelper.EndGroup();
            }
            _singleton._dispatchTable = _viCmdKeyMap;
            _singleton._chordDispatchTable = _viCmdChordTable;
            BackwardChar();
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// Switch to Insert move.
        /// </summary>
        public static void ViInsertMode(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._dispatchTable = _viInsKeyMap;
            _singleton._chordDispatchTable = _viInsChordTable;
        }

        /// <summary>
        /// Switch to Insert mode and position the cursor at the begining of the line.
        /// </summary>
        public static void ViInsertAtBegining(ConsoleKeyInfo? key = null, object arg = null)
        {
            ViInsertMode(key, arg);
            BeginningOfLine(key, arg);
        }

        /// <summary>
        /// Switch to Insert mode and position the cursor at the end of the line.
        /// </summary>
        public static void ViInsertAtEnd(ConsoleKeyInfo? key = null, object arg = null)
        {
            ViInsertMode(key, arg);
            EndOfLine(key, arg);
        }

        /// <summary>
        /// Append from the current line position.
        /// </summary>
        public static void ViInsertWithAppend(ConsoleKeyInfo? key = null, object arg = null)
        {
            ViInsertMode(key, arg);
            ForwardChar(key, arg);
        }

        /// <summary>
        /// Delete the current character and switch to Insert mode.
        /// </summary>
        public static void ViInsertWithDelete(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._groupUndoHelper.StartGroup(ViInsertWithDelete, arg);
            DeleteChar(key, arg);
            ViInsertMode(key, arg);
        }

        /// <summary>
        /// Accept the line and switch to Insert mode.
        /// </summary>
        public static void ViAcceptLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            ViInsertMode(key, arg);
            AcceptLine(key, arg);
        }

        /// <summary>
        /// Prepend a '#' and accept the line.
        /// </summary>
        public static void PrependAndAccept(ConsoleKeyInfo? key = null, object arg = null)
        {
            BeginningOfLine(key, arg);
            SelfInsert(key, arg);
            ViAcceptLine(key, arg);
        }

        /// <summary>
        /// Invert the case of the current character and move to the next one.
        /// </summary>
        public static void InvertCase(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current >= _singleton._buffer.Length)
            {
                Ding();
                return;
            }

            int qty = (arg is int) ? (int) arg : 1;

            for (; qty > 0 && _singleton._current < _singleton._buffer.Length; qty--)
            {
                char c = _singleton._buffer[_singleton._current];
                if (Char.IsLetter(c))
                {
                    char newChar = Char.IsUpper(c) ? Char.ToLower(c) : char.ToUpper(c);
                    EditItem delEditItem = EditItemDelete.Create(c.ToString(), _singleton._current);
                    EditItem insEditItem = EditItemInsertChar.Create(newChar, _singleton._current);
                    _singleton.SaveEditItem(GroupedEdit.Create(new List<EditItem> 
                        {
                            delEditItem,
                            insEditItem
                        },
                        InvertCase,
                        arg
                    ));

                    _singleton._buffer[_singleton._current] = newChar;
                }
                _singleton._current = Math.Min(_singleton._current + 1, _singleton._buffer.Length);
                _singleton.PlaceCursor();
            }
            _singleton.Render();
        }

        /// <summary>
        /// Swap the current character and the one before it.
        /// </summary>
        public static void SwapCharacters(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current <= 0 || _singleton._current >= _singleton._buffer.Length)
            {
                Ding();
                return;
            }

            char current = _singleton._buffer[_singleton._current];
            char previous = _singleton._buffer[_singleton._current - 1];

            _singleton.StartEditGroup();
            _singleton.SaveEditItem(EditItemDelete.Create(_singleton._buffer.ToString(_singleton._current - 1, 2), _singleton._current - 1));
            _singleton.SaveEditItem(EditItemInsertChar.Create(current, _singleton._current - 1));
            _singleton.SaveEditItem(EditItemInsertChar.Create(previous, _singleton._current));
            _singleton.EndEditGroup();

            _singleton._buffer[_singleton._current] = previous;
            _singleton._buffer[_singleton._current - 1] = current;
            _singleton._current = Math.Min(_singleton._current + 1, _singleton._buffer.Length - 1);
            _singleton.PlaceCursor();
            _singleton.Render();
        }

        /// <summary>
        /// Deletes text from the cursor to the first non-blank character of the line,
        /// </summary>
        public static void DeleteLineToFirstChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current > 0)
            {
                int i = 0;
                for (; i < _singleton._current; i++)
                {
                    if (!Char.IsWhiteSpace(_singleton._buffer[i]))
                    {
                        break;
                    }
                }

                _singleton.SaveToClipboard(i, _singleton._current - i);
                _singleton.SaveEditItem(EditItemDelete.Create(_singleton._clipboard, i, DeleteLineToFirstChar));

                _singleton._buffer.Remove(i, _singleton._current - i);
                _singleton._current = i;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Deletes the current line, enabling undo.
        /// </summary>
        public static void DeleteLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._clipboard = _singleton._buffer.ToString();
            _singleton.SaveEditItem(EditItemDelete.Create(_singleton._clipboard, 0));
            _singleton._current = 0;
            _singleton._buffer.Remove(0, _singleton._buffer.Length);
            _singleton.Render();
        }

        /// <summary>
        /// Deletes the previous word.
        /// </summary>
        public static void BackwardDeleteWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = (arg is int) ? (int) arg : 1;
            int deletePoint = _singleton._current;
            for (int i = 0; i < qty; i++)
            {
                deletePoint = _singleton.FindPreviousWordPointFrom(deletePoint, _singleton.Options.WordDelimiters);
            }
            if (deletePoint == _singleton._current)
            {
                Ding();
                return;
            }
            _singleton._clipboard = _singleton._buffer.ToString(deletePoint, _singleton._current - deletePoint);
            _singleton.SaveEditItem(EditItemDelete.Create(
                _singleton._clipboard,
                deletePoint,
                BackwardDeleteWord,
                arg
                ));
            _singleton._buffer.Remove(deletePoint, _singleton._current - deletePoint);
            _singleton._current = deletePoint;
            _singleton.Render();
        }

        /// <summary>
        /// Find the matching brace, paren, or square bracket and delete all contents within, including the brace.
        /// </summary>
        public static void ViDeleteBrace(ConsoleKeyInfo? key = null, object arg = null)
        {
            int newCursor = _singleton.ViFindBrace(_singleton._current);

            if (_singleton._current < newCursor)
            {
                DeleteRange(_singleton._current, newCursor, ViDeleteBrace);
            }
            else if (newCursor < _singleton._current)
            {
                DeleteRange(newCursor, _singleton._current, ViDeleteBrace);
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Delete all characters included in the supplied range.
        /// </summary>
        /// <param name="first">Index of where to begin the delete.</param>
        /// <param name="last">Index of where to end the delete.</param>
        /// <param name="action">Action that generated this request, used for repeat command ('.').</param>
        private static void DeleteRange(int first, int last, Action<ConsoleKeyInfo?, object> action)
        {
            int length = last - first + 1;

            _singleton.SaveToClipboard(first, length);
            _singleton.SaveEditItem(EditItemDelete.Create(_singleton._clipboard, first, action));
            _singleton._current = first;
            _singleton._buffer.Remove(first, length);
            _singleton.Render();
        }


        /// <summary>
        /// Prompts for a search string and initiates search upon AcceptLine.
        /// </summary>
        public static void SearchBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue || char.IsControl(key.Value.KeyChar))
            {
                Ding();
                return;
            }

            _singleton.StartSearch(backward: true);
        }

        /// <summary>
        /// Prompts for a search string and initiates search upon AcceptLine.
        /// </summary>
        public static void SearchForward(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue || char.IsControl(key.Value.KeyChar))
            {
                Ding();
                return;
            }

            _singleton.StartSearch(backward: false);
        }

        /// <summary>
        /// Repeat the last search in the same direction as before.
        /// </summary>
        public static void RepeatSearch(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (string.IsNullOrEmpty(_singleton._searchHistoryPrefix))
            {
                Ding();
                return;
            }

            _singleton.HistorySearch();
        }

        /// <summary>
        /// Repeat the last search in the same direction as before.
        /// </summary>
        public static void RepeatSearchBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._searchHistoryBackward = !_singleton._searchHistoryBackward;
            RepeatSearch();
            _singleton._searchHistoryBackward = !_singleton._searchHistoryBackward;
        }

        /// <summary>
        /// Prompts for a string for history searching.
        /// </summary>
        /// <param name="backward">True for seaching backward in the history.</param>
        private void StartSearch(bool backward)
        {
            _statusLinePrompt = "find: ";
            var argBuffer = _statusBuffer;
            Render(); // Render prompt

            while (true)
            {
                var nextKey = ReadKey();
                if (nextKey.Key == Keys.Enter.Key)
                {
                    _searchHistoryPrefix = argBuffer.ToString();
                    _searchHistoryBackward = backward;
                    HistorySearch();
                    break;
                }
                if (nextKey.Key == Keys.Escape.Key)
                {
                    break;
                }
                if (nextKey.Key == Keys.Backspace.Key)
                {
                    if (argBuffer.Length > 0)
                    {
                        argBuffer.Remove(argBuffer.Length - 1, 1);
                        Render(); // Render prompt
                        continue;
                    }
                    break;
                }
                argBuffer.Append(nextKey.KeyChar);
                Render(); // Render prompt
            }

            // Remove our status line
            argBuffer.Clear();
            _statusLinePrompt = null;
            Render(); // Render prompt
        }

        /// <summary>
        /// Searches line history.
        /// </summary>
        private void HistorySearch()
        {
            _searchHistoryCommandCount++;

            int incr = _searchHistoryBackward ? -1 : +1;
            for (int i = _currentHistoryIndex + incr; i >= 0 && i < _history.Count; i += incr)
            {
                if (Options.HistoryStringComparison.HasFlag(StringComparison.OrdinalIgnoreCase))
                {
                    if (_history[i]._line.ToLower().Contains(_searchHistoryPrefix.ToLower()))
                    {
                        _currentHistoryIndex = i;
                        UpdateFromHistory(moveCursor: Options.HistorySearchCursorMovesToEnd);
                        return;
                    }
                }
                else
                {
                    if (_history[i]._line.Contains(_searchHistoryPrefix))
                    {
                        _currentHistoryIndex = i;
                        UpdateFromHistory(moveCursor: Options.HistorySearchCursorMovesToEnd);
                        return;
                    }
                }
            }

            Ding();
        }

        /// <summary>
        /// Repeat the last text modification.
        /// </summary>
        public static void RepeatLastCommand(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._undoEditIndex > 0)
            {
                EditItem editItem = _singleton._edits[_singleton._undoEditIndex - 1];
                if (editItem._instigator != null)
                {
                    editItem._instigator(key, editItem._instigatorArg);
                    return;
                }
            }
            Ding();
        }
    }
}
