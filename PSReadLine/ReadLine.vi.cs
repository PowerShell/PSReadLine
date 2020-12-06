/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.PowerShell
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
            private bool wasBackward;
            private bool wasBackoff;

            public static readonly ViCharacterSearcher instance = new ViCharacterSearcher();

            public static bool IsRepeatable => instance.searchChar != '\0';
            public static char SearchChar => instance.searchChar;
            public static bool WasBackward => instance.wasBackward;
            public static bool WasBackoff => instance.wasBackoff;

            public static void Set(char theChar, bool isBackward, bool isBackoff)
            {
                instance.searchChar = theChar;
                instance.wasBackward = isBackward;
                instance.wasBackoff = isBackoff;
            }

            public static void Search(char keyChar, object arg, bool backoff)
            {
                int qty = arg as int? ?? 1;

                for (int i = _singleton._current + 1; i < _singleton._buffer.Length; i++)
                {
                    if (_singleton._buffer[i] == keyChar)
                    {
                        qty -= 1;
                        if (qty == 0)
                        {
                            _singleton.MoveCursor(backoff ? i - 1 : i);
                            return;
                        }
                    }
                }
                Ding();
            }

            public static bool SearchDelete(char keyChar, object arg, bool backoff, Action<ConsoleKeyInfo?, object> instigator)
            {
                int qty = arg as int? ?? 1;

                for (int i = _singleton._current + 1; i < _singleton._buffer.Length; i++)
                {
                    if (_singleton._buffer[i] == keyChar)
                    {
                        qty -= 1;
                        if (qty == 0)
                        {
                            DeleteToEndPoint(arg, backoff ? i : i + 1, instigator);
                            return true;
                        }
                    }
                }
                Ding();
                return false;
            }

            public static void SearchBackward(char keyChar, object arg, bool backoff)
            {
                int qty = arg as int? ?? 1;

                for (int i = _singleton._current - 1; i >= 0; i--)
                {
                    if (_singleton._buffer[i] == keyChar)
                    {
                        qty -= 1;
                        if (qty == 0)
                        {
                            _singleton.MoveCursor(backoff ? i + 1 : i);
                            return;
                        }
                    }
                }
                Ding();
            }

            public static bool SearchBackwardDelete(char keyChar, object arg, bool backoff, Action<ConsoleKeyInfo?, object> instigator)
            {
                Set(keyChar, isBackward: true, isBackoff: backoff);
                int qty = arg as int? ?? 1;

                for (int i = _singleton._current - 1; i >= 0; i--)
                {
                    if (_singleton._buffer[i] == keyChar)
                    {
                        qty -= 1;
                        if (qty == 0)
                        {
                            DeleteBackwardToEndPoint(arg, backoff ? i + 1 : i, instigator);
                            return true;
                        }
                    }
                }
                Ding();
                return false;
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
        /// Read the next character and then find it, going backward, and then back off a character.
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
        /// Read the next character and then find it, going backward, and then back off a character.
        /// This is for 'T' functionality.
        /// </summary>
        public static void SearchCharBackwardWithBackoff(ConsoleKeyInfo? key = null, object arg = null)
        {
            char keyChar = ReadKey().KeyChar;
            ViCharacterSearcher.Set(keyChar, isBackward: true, isBackoff: true);
            ViCharacterSearcher.SearchBackward(keyChar, arg, backoff: true);
        }

        /// <summary>
        /// Exits the shell.
        /// </summary>
        public static void ViExit(ConsoleKeyInfo? key = null, object arg = null)
        {
            throw new ExitException();
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

            var lineCount = _singleton.GetLogicalLineCount();
            var lineIndex = _singleton.GetLogicalLineNumber() - 1;

            if (TryGetArgAsInt(arg, out var requestedLineCount, 1))
            {
                var targetLineIndex = lineIndex + requestedLineCount - 1;
                if (targetLineIndex >= lineCount)
                {
                    targetLineIndex = lineCount - 1;
                }

                var startPosition = _singleton._current;
                var endPosition = GetEndOfNthLogicalLinePos(targetLineIndex);

                var length = endPosition - startPosition + 1;
                if (length > 0)
                {
                    _singleton.RemoveTextToClipboard(
                        startPosition,
                        length,
                        DeleteToEnd,
                        arg);

                    // the cursor will go back one character, unless at the beginning of the line
                    var endOfLineCursorPos = GetEndOfLogicalLinePos(_singleton._current) - 1;
                    var beginningOfLinePos = GetBeginningOfLinePos(_singleton._current);

                    _singleton._current = Math.Max(
                        beginningOfLinePos,
                        Math.Min(_singleton._current, endOfLineCursorPos));

                    _singleton.Render();
                }
            }
        }

        /// <summary>
        /// Delete the next word.
        /// </summary>
        public static void DeleteWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = arg as int? ?? 1;
            int endPoint = _singleton._current;
            for (int i = 0; i < qty; i++)
            {
                endPoint = _singleton.ViFindNextWordPoint(endPoint, _singleton.Options.WordDelimiters);
            }

            if (endPoint <= _singleton._current)
            {
                Ding();
                return;
            }

            DeleteToEndPoint(arg, endPoint, DeleteWord);
        }

        private static void DeleteToEndPoint(object arg, int endPoint, Action<ConsoleKeyInfo?, object> instigator)
        {
            _singleton.RemoveTextToClipboard(
                _singleton._current,
                endPoint - _singleton._current,
                instigator,
                arg
                );

            if (_singleton._current >= _singleton._buffer.Length)
            {
                _singleton._current = Math.Max(0, _singleton._buffer.Length - 1);
            }
            _singleton.Render();
        }

        private static void DeleteBackwardToEndPoint(object arg, int endPoint, Action<ConsoleKeyInfo?, object> instigator)
        {
            int deleteLength = _singleton._current - endPoint + 1;

            _singleton.RemoveTextToClipboard(
                endPoint,
                deleteLength,
                instigator,
                arg);

            _singleton._current = endPoint;
            _singleton.Render();
        }

        /// <summary>
        /// Delete the next glob (white space delimited word).
        /// </summary>
        public static void ViDeleteGlob(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = arg as int? ?? 1;
            int endPoint = _singleton._current;
            while (qty-- > 0)
            {
                endPoint = _singleton.ViFindNextGlob(endPoint);
            }

            DeleteToEndPoint(arg, endPoint, ViDeleteGlob);
        }

        /// <summary>
        /// Delete to the end of the word.
        /// </summary>
        public static void DeleteEndOfWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = arg as int? ?? 1;
            int endPoint = _singleton._current;
            for (int i = 0; i < qty; i++)
            {
                endPoint = _singleton.ViFindNextWordEnd(endPoint, _singleton.Options.WordDelimiters);
            }

            if (endPoint <= _singleton._current)
            {
                Ding();
                return;
            }

            DeleteToEndPoint(arg, 1 + endPoint, DeleteEndOfWord);
        }

        /// <summary>
        /// Delete to the end of the word.
        /// </summary>
        public static void ViDeleteEndOfGlob(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = arg as int? ?? 1;
            int endPoint = _singleton._current;
            for (int i = 0; i < qty; i++)
            {
                endPoint = _singleton.ViFindGlobEnd(endPoint);
            }

            DeleteToEndPoint(arg, 1 + endPoint, ViDeleteEndOfGlob);
        }

        /// <summary>
        /// Deletes until given character.
        /// </summary>
        public static void ViDeleteToChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            var keyChar = ReadKey().KeyChar;
            ViDeleteToChar(keyChar, key, arg);
        }

        /// <summary>
        /// Deletes until given character.
        /// </summary>
        public static void ViDeleteToChar(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
        {
            ViCharacterSearcher.Set(keyChar, isBackward: false, isBackoff: false);
            ViCharacterSearcher.SearchDelete(keyChar, arg, backoff: false, instigator: (_key, _arg) => ViDeleteToChar(keyChar, _key, _arg));
        }

        /// <summary>
        /// Deletes backwards until given character.
        /// </summary>
        public static void ViDeleteToCharBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            var keyChar = ReadKey().KeyChar;
            ViDeleteToCharBack(keyChar, key, arg);
        }

        /// <summary>
        /// Deletes backwards until given character.
        /// </summary>
        public static void ViDeleteToCharBack(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
        {
            ViCharacterSearcher.SearchBackwardDelete(keyChar, arg, backoff: false, instigator: (_key, _arg) => ViDeleteToCharBack(keyChar, _key, _arg));
        }

        /// <summary>
        /// Deletes until given character.
        /// </summary>
        public static void ViDeleteToBeforeChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            var keyChar = ReadKey().KeyChar;
            ViDeleteToBeforeChar(keyChar, key, arg);
        }

        /// <summary>
        /// Deletes until given character.
        /// </summary>
        public static void ViDeleteToBeforeChar(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
        {
            ViCharacterSearcher.Set(keyChar, isBackward: false, isBackoff: true);
            ViCharacterSearcher.SearchDelete(keyChar, arg, backoff: true, instigator: (_key, _arg) => ViDeleteToBeforeChar(keyChar, _key, _arg));
        }

        /// <summary>
        /// Deletes until given character.
        /// </summary>
        public static void ViDeleteToBeforeCharBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            var keyChar = ReadKey().KeyChar;
            ViDeleteToBeforeCharBack(keyChar, key, arg);
        }

        private static void ViDeleteToBeforeCharBack(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
        {
            ViCharacterSearcher.Set(keyChar, isBackward: true, isBackoff: true);
            ViCharacterSearcher.SearchBackwardDelete(keyChar, arg, backoff: true, instigator: (_key, _arg) => ViDeleteToBeforeCharBack(keyChar, _key, _arg));
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
            if (_singleton._editGroupStart >= 0)
            {
                _singleton._groupUndoHelper.EndGroup();
            }
            _singleton._dispatchTable = _viCmdKeyMap;
            _singleton._chordDispatchTable = _viCmdChordTable;
            ViBackwardChar();
            _singleton.ViIndicateCommandMode();
        }

        /// <summary>
        /// Switch to Insert mode.
        /// </summary>
        public static void ViInsertMode(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._dispatchTable = _viInsKeyMap;
            _singleton._chordDispatchTable = _viInsChordTable;
            _singleton.ViIndicateInsertMode();
        }

        /// <summary>
        /// Returns true if in Vi Command mode, otherwise false.
        /// </summary>
        public static bool InViCommandMode() => _singleton._dispatchTable == _viCmdKeyMap;

        /// <summary>
        /// Returns true if in Vi Insert mode, otherwise false.
        /// </summary>
        public static bool InViInsertMode() => _singleton._dispatchTable == _viInsKeyMap;

        /// <summary>
        /// Temporarily swap in Vi-Command dispatch tables. Used for setting handlers.
        /// </summary>
        internal static IDisposable UseViCommandModeTables()
        {
            var oldDispatchTable = _singleton._dispatchTable;
            var oldChordDispatchTable = _singleton._chordDispatchTable;

            _singleton._dispatchTable = _viCmdKeyMap;
            _singleton._chordDispatchTable = _viCmdChordTable;

            return new Disposable(() =>
           {
               _singleton._dispatchTable = oldDispatchTable;
               _singleton._chordDispatchTable = oldChordDispatchTable;
           });
        }

        /// <summary>
        /// Temporarily swap in Vi-Insert dispatch tables. Used for setting handlers.
        /// </summary>
        internal static IDisposable UseViInsertModeTables()
        {
            var oldDispatchTable = _singleton._dispatchTable;
            var oldChordDispatchTable = _singleton._chordDispatchTable;

            _singleton._dispatchTable = _viInsKeyMap;
            _singleton._chordDispatchTable = _viInsChordTable;

            return new Disposable(() =>
           {
               _singleton._dispatchTable = oldDispatchTable;
               _singleton._chordDispatchTable = oldChordDispatchTable;
           });
        }

        private void ViIndicateCommandMode()
        {
            // Show suggestion in 'InsertMode' but not 'CommandMode'.
            _prediction.DisableGlobal(cursorAtEol: false);

            if (_options.ViModeIndicator == ViModeStyle.Cursor)
            {
                _console.CursorSize = _normalCursorSize < 50 ? 100 : 25;
            }
            else if (_options.ViModeIndicator == ViModeStyle.Prompt)
            {
                ConsoleColor savedBackground = _console.BackgroundColor;
                _console.BackgroundColor = AlternateBackground(_console.BackgroundColor);
                InvokePrompt();
                _console.BackgroundColor = savedBackground;
            }
            else if (_options.ViModeIndicator == ViModeStyle.Script && _options.ViModeChangeHandler != null)
            {
                _options.ViModeChangeHandler.InvokeReturnAsIs(ViMode.Command);
            }
        }

        private void ViIndicateInsertMode()
        {
            // Show suggestion in 'InsertMode' but not 'CommandMode'.
            _prediction.EnableGlobal();

            if (_options.ViModeIndicator == ViModeStyle.Cursor)
            {
                _console.CursorSize = _normalCursorSize;
            }
            else if (_options.ViModeIndicator == ViModeStyle.Prompt)
            {
                InvokePrompt();
            }
            else if (_options.ViModeIndicator == ViModeStyle.Script && _options.ViModeChangeHandler != null)
            {
                _options.ViModeChangeHandler.InvokeReturnAsIs(ViMode.Insert);
            }
        }

        /// <summary>
        /// Switch to Insert mode and position the cursor at the beginning of the line.
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

            ViInsertMode(key, arg);
            DeleteChar(key, arg);
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

            int qty = arg as int? ?? 1;

            for (; qty > 0 && _singleton._current < _singleton._buffer.Length; qty--)
            {
                char c = _singleton._buffer[_singleton._current];
                if (Char.IsLetter(c))
                {
                    char newChar = Char.IsUpper(c) ? Char.ToLower(c, CultureInfo.CurrentCulture) : char.ToUpper(c, CultureInfo.CurrentCulture);
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
                _singleton.MoveCursor(Math.Min(_singleton._current + 1, _singleton._buffer.Length));
            }
            _singleton.Render();
        }

        /// <summary>
        /// Swap the current character and the one before it.
        /// </summary>
        public static void SwapCharacters(ConsoleKeyInfo? key = null, object arg = null)
        {
            // if in vi command mode, the cursor can't go as far
            var bufferLength = _singleton._buffer.Length;
            int cursorRightLimit = bufferLength + ViEndOfLineFactor;
            if (_singleton._current <= 0 || bufferLength < 2 || _singleton._current > cursorRightLimit)
            {
                Ding();
                return;
            }

            int cursor = _singleton._current;
            if (cursor == bufferLength)
                --cursor; // if at end of line, swap previous two chars

            char current = _singleton._buffer[cursor];
            char previous = _singleton._buffer[cursor - 1];

            _singleton.StartEditGroup();
            _singleton.SaveEditItem(EditItemDelete.Create(_singleton._buffer.ToString(cursor - 1, 2), cursor - 1));
            _singleton.SaveEditItem(EditItemInsertChar.Create(current, cursor - 1));
            _singleton.SaveEditItem(EditItemInsertChar.Create(previous, cursor));
            _singleton.EndEditGroup();

            _singleton._buffer[cursor] = previous;
            _singleton._buffer[cursor - 1] = current;
            _singleton.MoveCursor(Math.Min(cursor + 1, cursorRightLimit));
            _singleton.Render();
        }

        /// <summary>
        /// Deletes text from the cursor to the first non-blank character of the line.
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

                _singleton.RemoveTextToClipboard(
                    i,
                    _singleton._current - i,
                    DeleteLineToFirstChar,
                    arg);

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
            var lineCount = _singleton.GetLogicalLineCount();
            var lineIndex = _singleton.GetLogicalLineNumber() - 1;

            TryGetArgAsInt(arg, out var requestedLineCount, 1);

            var deletePosition = DeleteLineImpl(lineIndex, requestedLineCount);

            // goto the first character of the first remaining logical line
            var newCurrent = deletePosition + 1;

            if (lineIndex + requestedLineCount >= lineCount)
            {
                // if the delete operation has removed all the remaining lines
                // goto the first character of the previous logical line 
                newCurrent = GetBeginningOfLinePos(deletePosition);
            }

            _singleton._current = newCurrent;
            _singleton.Render();
        }

        /// <summary>
        /// Deletes as many requested lines from the buffer
        /// starting from the specified line index, and
        /// return the offset to the deleted position.
        /// </summary>
        /// <returns></returns>
        private static int DeleteLineImpl(int lineIndex, int lineCount)
        {
            var range = _singleton._buffer.GetRange(lineIndex, lineCount);

            var deleteText = _singleton._buffer.ToString(range.Offset, range.Count);

            _clipboard.LinewiseRecord(deleteText);

            var deletePosition = range.Offset;
            var anchor = _singleton._current;

            _singleton._buffer.Remove(range.Offset, range.Count);

            _singleton.SaveEditItem(
                EditItemDeleteLines.Create(
                    deleteText,
                    deletePosition,
                    anchor));

            return deletePosition;
        }

        /// <summary>
        /// Deletes from the current logical line to the end of the buffer.
        /// </summary>
        public static void DeleteEndOfBuffer(ConsoleKeyInfo? key = null, object arg = null)
        {
            var lineIndex = _singleton.GetLogicalLineNumber() - 1;
            var lineCount = _singleton.GetLogicalLineCount() - lineIndex;

            DeleteLineImpl(lineIndex, lineCount);

            // move the cursor to the beginning of the previous line
            var previousLineIndex = Math.Max(0, lineIndex - 1);
            var newPosition = GetBeginningOfNthLinePos(previousLineIndex);

            _singleton._current = newPosition;
            _singleton.Render();
        }

        /// <summary>
        /// Deletes the current and next n logical lines.
        /// </summary>
        private static void DeleteNextLines(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (TryGetArgAsInt(arg, out int requestedLineCount, 1))
            {
                DeleteLine(key, requestedLineCount + 1);
            }
        }

        /// <summary>
        /// Deletes from the previous n logical lines to the current logical line included.
        /// </summary>
        public static void DeletePreviousLines(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (TryGetArgAsInt(arg, out int requestedLineCount, 1))
            {
                var currentLineIndex = _singleton.GetLogicalLineNumber() - 1;
                var startLineIndex = Math.Max(0, currentLineIndex - requestedLineCount);

                DeleteLineImpl(startLineIndex, currentLineIndex - startLineIndex + 1);

                // go the beginning of the line at index 'startLineIndex'
                // or at the beginning of the last line
                startLineIndex = Math.Min(startLineIndex, _singleton.GetLogicalLineCount() - 1);
                var newCurrent = GetBeginningOfNthLinePos(startLineIndex);

                _singleton._current = newCurrent;
                _singleton.Render();
            }
        }

        /// <summary>
        /// Delete from the current logical line to the n-th requested logical line in a multiline buffer
        /// </summary>
        private static void DeleteRelativeLines(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (TryGetArgAsInt(arg, out var requestedLineNumber, 1))
            {
                var currentLineIndex = _singleton.GetLogicalLineNumber() - 1;
                var requestedLineIndex = requestedLineNumber - 1;
                if (requestedLineIndex < 0)
                {
                    requestedLineIndex = 0;
                }

                var logicalLineCount = _singleton.GetLogicalLineCount();
                if (requestedLineIndex >= logicalLineCount)
                {
                    requestedLineIndex = logicalLineCount - 1;
                }

                var requestedLineCount = requestedLineIndex - currentLineIndex;
                if (requestedLineCount < 0)
                {
                    DeletePreviousLines(null, -requestedLineCount);
                }
                else
                {
                    DeleteNextLines(null, requestedLineCount);
                }
            }
        }

        /// <summary>
        /// Deletes the previous word.
        /// </summary>
        public static void BackwardDeleteWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = arg as int? ?? 1;
            int deletePoint = _singleton._current;
            for (int i = 0; i < qty; i++)
            {
                deletePoint = _singleton.ViFindPreviousWordPoint(deletePoint, _singleton.Options.WordDelimiters);
            }
            if (deletePoint == _singleton._current)
            {
                Ding();
                return;
            }
            _singleton.RemoveTextToClipboard(
                deletePoint,
                _singleton._current - deletePoint,
                BackwardDeleteWord,
                arg);

            _singleton._current = deletePoint;
            _singleton.Render();
        }

        /// <summary>
        /// Deletes the previous word, using only white space as the word delimiter.
        /// </summary>
        public static void ViBackwardDeleteGlob(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current == 0)
            {
                Ding();
                return;
            }
            int qty = arg as int? ?? 1;
            int deletePoint = _singleton._current;
            for (int i = 0; i < qty && deletePoint > 0; i++)
            {
                deletePoint = _singleton.ViFindPreviousGlob(deletePoint - 1);
            }
            if (deletePoint == _singleton._current)
            {
                Ding();
                return;
            }
            _singleton.RemoveTextToClipboard(
                deletePoint,
                _singleton._current - deletePoint,
                BackwardDeleteWord,
                arg);

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

            _singleton.RemoveTextToClipboard(
                first,
                length,
                action);

            _singleton._current = first;
            _singleton.Render();
        }


        /// <summary>
        /// Prompts for a search string and initiates search upon AcceptLine.
        /// </summary>
        public static void ViSearchHistoryBackward(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
            _singleton.StartSearch(backward: true);
        }

        /// <summary>
        /// Prompts for a search string and initiates search upon AcceptLine.
        /// </summary>
        public static void SearchForward(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.SaveCurrentLine();
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

            _singleton._anyHistoryCommandCount++;
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
        /// <param name="backward">True for searching backward in the history.</param>
        private void StartSearch(bool backward)
        {
            _statusLinePrompt = "find: ";
            var argBuffer = _statusBuffer;
            Render(); // Render prompt

            while (true)
            {
                var nextKey = ReadKey();
                if (nextKey == Keys.Enter || nextKey == Keys.Tab)
                {
                    _searchHistoryPrefix = argBuffer.ToString();
                    _searchHistoryBackward = backward;
                    HistorySearch();
                    break;
                }
                if (nextKey == Keys.Escape)
                {
                    break;
                }
                if (nextKey == Keys.Backspace)
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
            var moveCursor = Options.HistorySearchCursorMovesToEnd
                ? HistoryMoveCursor.ToEnd
                : HistoryMoveCursor.DontMove;
            for (int i = _currentHistoryIndex + incr; i >= 0 && i < _history.Count; i += incr)
            {
                if (Options.HistoryStringComparison.HasFlag(StringComparison.OrdinalIgnoreCase))
                {
                    if (_history[i].CommandLine.ToLower().Contains(_searchHistoryPrefix.ToLower()))
                    {
                        _currentHistoryIndex = i;
                        UpdateFromHistory(moveCursor);
                        return;
                    }
                }
                else
                {
                    if (_history[i].CommandLine.Contains(_searchHistoryPrefix))
                    {
                        _currentHistoryIndex = i;
                        UpdateFromHistory(moveCursor);
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

        /// <summary>
        /// Chords in vi needs special handling because a numeric argument can be input between the 1st and 2nd key.
        /// </summary>
        private static void ViChord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (arg != null)
            {
                Chord(key, arg);
                return;
            }

            if (_singleton._chordDispatchTable.TryGetValue(PSKeyInfo.FromConsoleKeyInfo(key.Value), out var secondKeyDispatchTable))
            {
                ViChordHandler(secondKeyDispatchTable, arg);
            }
        }

        private static void ViChordHandler(Dictionary<PSKeyInfo, KeyHandler> secondKeyDispatchTable, object arg = null)
        {
            var secondKey = ReadKey();
            if (secondKeyDispatchTable.TryGetValue(secondKey, out var handler))
            {
                _singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, ignoreIfNoAction: true, arg: arg);
            }
            else if (!IsNumeric(secondKey))
            {
                _singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, ignoreIfNoAction: true, arg: arg);
            }
            else
            {
                var argBuffer = _singleton._statusBuffer;
                argBuffer.Clear();
                _singleton._statusLinePrompt = "digit-argument: ";
                while (IsNumeric(secondKey))
                {
                    argBuffer.Append(secondKey.KeyChar);
                    _singleton.Render();
                    secondKey = ReadKey();
                }
                int numericArg = int.Parse(argBuffer.ToString());
                if (secondKeyDispatchTable.TryGetValue(secondKey, out handler))
                {
                    _singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, ignoreIfNoAction: true, arg: numericArg);
                }
                else
                {
                    Ding();
                }
                argBuffer.Clear();
                _singleton.ClearStatusMessage(render: true);
            }
        }

        private static void ViDGChord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                throw new ArgumentNullException(nameof(key));
            }

            ViChordHandler(_viChordDGTable, arg);
        }

        private static bool IsNumeric(PSKeyInfo key)
        {
            return key.KeyChar >= '0' && key.KeyChar <= '9' && !key.Control && !key.Alt;
        }

        /// <summary>
        /// Start a new digit argument to pass to other functions while in one of vi's chords.
        /// </summary>
        public static void ViDigitArgumentInChord(ConsoleKeyInfo? key = null, object arg = null)
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
                if (_singleton._dispatchTable.TryGetValue(nextKey, out var handler) && handler.Action == DigitArgument)
                {
                    if (nextKey == Keys.Minus)
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

                    if (IsNumeric(nextKey))
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
        }

        /// <summary>
        /// Like DeleteCharOrExit in Emacs mode, but accepts the line instead of deleting a character.
        /// </summary>
        public static void ViAcceptLineOrExit(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._buffer.Length > 0)
            {
                ViInsertMode(key, arg);
                _singleton.AcceptLineImpl(false);
            }
            else
            {
                ViExit(key, arg);
            }
        }

        /// <summary>
        /// A new line is inserted above the current line.
        /// </summary>
        public static void ViInsertLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._groupUndoHelper.StartGroup(ViInsertLine, arg);
            _singleton.MoveToBeginningOfPhrase();
            _singleton._buffer.Insert(_singleton._current, '\n');
            //_singleton._current = Math.Max(0, _singleton._current - 1);
            _singleton.SaveEditItem(EditItemInsertChar.Create('\n', _singleton._current));
            _singleton.Render();
            ViInsertMode();
        }

        private void MoveToBeginningOfPhrase()
        {
            while (!IsAtBeginningOfPhrase())
            {
                _current--;
            }
        }

        private bool IsAtBeginningOfPhrase()
        {
            if (_current == 0)
            {
                return true;
            }
            if (_buffer[_current - 1] == '\n')
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// A new line is inserted below the current line.
        /// </summary>
        public static void ViAppendLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._groupUndoHelper.StartGroup(ViInsertLine, arg);
            _singleton.MoveToEndOfPhrase();
            int insertPoint = 0;
            if (_singleton.IsAtEndOfLine(_singleton._current))
            {
                insertPoint = _singleton._buffer.Length;
                _singleton._buffer.Append('\n');
                _singleton._current = insertPoint;
            }
            else
            {
                insertPoint = _singleton._current + 1;
                _singleton._buffer.Insert(insertPoint, '\n');
            }
            _singleton.SaveEditItem(EditItemInsertChar.Create('\n', insertPoint));
            _singleton.Render();
            ViInsertWithAppend();
        }

        private void MoveToEndOfPhrase()
        {
            while (!IsAtEndOfPhrase())
            {
                _current++;
            }
        }

        private bool IsAtEndOfPhrase()
        {
            if (_buffer.Length == 0 || _current == _buffer.Length + ViEndOfLineFactor)
            {
                return true;
            }
            if (_current == _buffer.Length && _buffer[_current - 1] == '\n')
            {
                return true;
            }
            if (_buffer[_current] == '\n')
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Joins the current line and the next line.
        /// </summary>
        public static void ViJoinLines(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.MoveToEndOfPhrase();
            if (_singleton.IsAtEndOfLine(_singleton._current))
            {
                Ding();
            }
            else
            {
                _singleton._buffer[_singleton._current] = ' ';
                _singleton._groupUndoHelper.StartGroup(ViJoinLines, arg);
                _singleton.SaveEditItem(EditItemDelete.Create("\n", _singleton._current));
                _singleton.SaveEditItem(EditItemInsertChar.Create(' ', _singleton._current));
                _singleton._groupUndoHelper.EndGroup();
                _singleton.Render();
            }
        }
    }
}
