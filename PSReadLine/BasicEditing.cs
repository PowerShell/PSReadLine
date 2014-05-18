using System;
using System.Linq;

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
                if (count <= 0)
                    return;
                if (count > 1)
                {
                    var toInsert = new string(key.Value.KeyChar, count);
                    if (_singleton._visualSelectionCommandCount > 0)
                    {
                        int start, length;
                        _singleton.GetRegion(out start, out length);
                        Replace(start, length, toInsert);
                    }
                    else
                    {
                        Insert(toInsert);
                    }
                    return;
                }
            }

            if (_singleton._visualSelectionCommandCount > 0)
            {
                int start, length;
                _singleton.GetRegion(out start, out length);
                Replace(start, length, new string(key.Value.KeyChar, 1));
            }
            else
            {
                Insert(key.Value.KeyChar);
            }

        }

        /// <summary>
        /// Reverts all of the input to the current input.
        /// </summary>
        public static void RevertLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            while (_singleton._undoEditIndex > 0)
            {
                _singleton._edits[_singleton._undoEditIndex - 1].Undo();
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
            _singleton._current = _singleton._buffer.Length;
            // We want to display ^C to show the line was canceled.  Instead of appending ^C
            // (or (char)3), we append 2 spaces so we don't affect tokenization too much, e.g.
            // changing a keyword to a command.
            _singleton._buffer.Append("  ");
            _singleton.ReallyRender();

            // Now that we've rendered with this extra spaces, go back and replace the spaces
            // with ^C colored in red (so it stands out.)
            var coordinates = _singleton.ConvertOffsetToCoordinates(_singleton._current);
            int i = (coordinates.Y - _singleton._initialY) * Console.BufferWidth + coordinates.X;
            _singleton._consoleBuffer[i].UnicodeChar = '^';
            _singleton._consoleBuffer[i].ForegroundColor = ConsoleColor.Red;
            _singleton._consoleBuffer[i].BackgroundColor = Console.BackgroundColor;
            _singleton._consoleBuffer[i+1].UnicodeChar = 'C';
            _singleton._consoleBuffer[i+1].ForegroundColor = ConsoleColor.Red;
            _singleton._consoleBuffer[i+1].BackgroundColor = Console.BackgroundColor;
            WriteBufferLines(_singleton._consoleBuffer, ref _singleton._initialY);

            var y = coordinates.Y + 1;
            _singleton.PlaceCursor(0, ref y);
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
                int qty = (arg is int) ? (int) arg : 1;
                qty = Math.Min(qty, _singleton._current);

                int startDeleteIndex = _singleton._current - qty;
                _singleton.SaveEditItem(
                    EditItemDelete.Create(
                        _singleton._buffer.ToString(startDeleteIndex, qty),
                        startDeleteIndex,
                        BackwardDeleteChar,
                        arg)
                        );
                _singleton._buffer.Remove(startDeleteIndex, qty);
                _singleton._current = startDeleteIndex;
                _singleton.Render();
            }
            else
            {
                Ding();
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
                int qty = (arg is int) ? (int) arg : 1;
                qty = Math.Min(qty, _singleton._buffer.Length - _singleton._current);

                _singleton.SaveEditItem(
                    EditItemDelete.Create(_singleton._buffer.ToString(_singleton._current, qty),
                    _singleton._current,
                    DeleteChar,
                    arg));
                _singleton._buffer.Remove(_singleton._current, qty);
                if (_singleton._current >= _singleton._buffer.Length)
                {
                    _singleton._current = _singleton._buffer.Length - 1;
                }
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
            var y = coordinates.Y + 1;
            PlaceCursor(0, ref y);
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
