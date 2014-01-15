using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Insert the key
        /// </summary>
        public static void SelfInsert(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                return;
            }

            if (arg is int)
            {
                var count = (int)arg;
                if (count > 0)
                {
                    Insert(new string(key.Value.KeyChar, count));
                }
                return;
            }

            Insert(key.Value.KeyChar);
        }

        /// <summary>
        /// Reverts all of the input to the current input.
        /// </summary>
        public static void RevertLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            while (_singleton._undoEditIndex > 0)
            {
                _singleton._edits[_singleton._undoEditIndex - 1].Undo(_singleton);
                _singleton._undoEditIndex--;
            }
            _singleton.Render();
        }

        /// <summary>
        /// Cancel the current input, leaving the input on the screen,
        /// but returns back to the host so the prompt is evaluated again.
        /// </summary>
        public static void CancelLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            // Append the key that canceled input and display it so we have some visual
            // hint that the command didn't run.
            _singleton._current = _singleton._buffer.Length;
            _singleton._buffer.Append(key.HasValue ? key.Value.KeyChar : Keys.CtrlC.KeyChar);
            _singleton.Render();

            var coordinates = _singleton.ConvertOffsetToCoordinates(_singleton._current);
            _singleton.PlaceCursor(0, coordinates.Y + 1);
            _singleton._buffer.Clear(); // Clear so we don't actually run the input
            _singleton._currentHistoryIndex = _singleton._history.Count;
            _singleton._inputAccepted = true;
        }

        /// <summary>
        /// Like ForwardKillLine - deletes text from the point to the end of the line,
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        public static void ForwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            var current = _singleton._current;
            var buffer = _singleton._buffer;
            if (buffer.Length > 0 && current < buffer.Length)
            {
                int length = buffer.Length - current;
                var str = buffer.ToString(current, length);
                _singleton.SaveEditItem(EditItemDelete.Create(str, current));
                buffer.Remove(current, length);
                _singleton.Render();
            }
        }

        /// <summary>
        /// Like BackwardKillLine - deletes text from the point to the start of the line,
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        public static void BackwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current > 0)
            {
                var str = _singleton._buffer.ToString(0, _singleton._current);
                _singleton.SaveEditItem(EditItemDelete.Create(str, 0));
                _singleton._buffer.Remove(0, _singleton._current);
                _singleton._current = 0;
                _singleton.Render();
            }
        }

        /// <summary>
        /// Delete the character before the cursor.
        /// </summary>
        public static void BackwardDeleteChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                int start, length;
                _singleton.GetRegion(out start, out length);
                Delete(start, length);
                return;
            }

            if (_singleton._buffer.Length > 0 && _singleton._current > 0)
            {
                int startDeleteIndex = _singleton._current - 1;
                _singleton.SaveEditItem(
                    EditItemDelete.Create(new string(_singleton._buffer[startDeleteIndex], 1), startDeleteIndex));
                _singleton._buffer.Remove(startDeleteIndex, 1);
                _singleton._current--;
                _singleton.Render();
            }
        }

        /// <summary>
        /// Delete the character under the cursor.
        /// </summary>
        public static void DeleteChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                int start, length;
                _singleton.GetRegion(out start, out length);
                Delete(start, length);
                return;
            }

            if (_singleton._buffer.Length > 0 && _singleton._current < _singleton._buffer.Length)
            {
                _singleton.SaveEditItem(
                    EditItemDelete.Create(new string(_singleton._buffer[_singleton._current], 1), _singleton._current));
                _singleton._buffer.Remove(_singleton._current, 1);
                _singleton.Render();
            }
        }

        private bool AcceptLineImpl()
        {
            ParseInput();
            if (_parseErrors.Any(e => e.IncompleteInput))
            {
                Insert('\n');
                return false;
            }

            _renderForDemoNeeded = false;

            // Make sure cursor is at the end before writing the line
            _current = _buffer.Length;
            if (_queuedKeys.Count > 0)
            {
                // If text was pasted, for performance reasons we skip rendering for some time,
                // but if input is accepted, we won't have another chance to render.
                ReallyRender();
            }
            var coordinates = ConvertOffsetToCoordinates(_current);
            PlaceCursor(0, coordinates.Y + 1);
            _inputAccepted = true;
            return true;
        }

        /// <summary>
        /// Attempt to execute the current input.  If the current input is incomplete (for
        /// example there is a missing closing parenthesis, bracket, or quote, then the
        /// continuation prompt is displayed on the next line and PSReadline waits for
        /// keys to edit the current input.
        /// </summary>
        public static void AcceptLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.AcceptLineImpl();
        }

        /// <summary>
        /// Attempt to execute the current input.  If it can be executed (like AcceptLine),
        /// then recall the next item from history the next time Readline is called.
        /// </summary>
        public static void AcceptAndGetNext(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton.AcceptLineImpl())
            {
                if (_singleton._currentHistoryIndex < (_singleton._history.Count - 1))
                {
                    _singleton._getNextHistoryIndex = _singleton._currentHistoryIndex + 1;
                }
                else
                {
                    Ding();
                }
            }
        }

        /// <summary>
        /// The continuation prompt is displayed on the next line and PSReadline waits for
        /// keys to edit the current input.  This is useful to enter multi-line input as
        /// a single command even when a single line is complete input by itself.
        /// </summary>
        public static void AddLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            Insert('\n');
        }
    }
}
