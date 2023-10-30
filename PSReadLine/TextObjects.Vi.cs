using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        public static void ViDeleteInnerWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            var delimiters = _singleton.Options.WordDelimiters;

            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            if (_singleton._buffer.Length == 0)
            {
                if (numericArg > 1)
                {
                    Ding();
                }
                return;
            }

            // Unless at the end of the buffer a single delete word should not delete backwards
            // so if the cursor is on an empty line, do nothing.
            if (numericArg == 1 &&
                _singleton._current < _singleton._buffer.Length &&
                _singleton._buffer.IsLogigalLineEmpty(_singleton._current))
            {
                return;
            }

            var start = _singleton._buffer.ViFindBeginningOfWordObjectBoundary(_singleton._current, delimiters);
            var end = _singleton._current;

            // Attempting to find a valid position for multiple words.
            // If no valid position is found, this is a no-op
            {
                while (numericArg-- > 0 && end < _singleton._buffer.Length)
                {
                    end = _singleton._buffer.ViFindBeginningOfNextWordObjectBoundary(end, delimiters);
                }

                // Attempting to delete too many words should ding.
                if (numericArg > 0)
                {
                    Ding();
                    return;
                }
            }

            if (end > 0 && _singleton._buffer.IsAtEndOfBuffer(end - 1) && _singleton._buffer.InWord(end - 1, delimiters))
            {
                _singleton._shouldAppend = true;
            }

            _singleton.RemoveTextToViRegister(start, end - start);
            _singleton.AdjustCursorPosition(start);
            _singleton.Render();
        }

        /// <summary>
        /// Attempt to set the cursor at the specified position.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        private int AdjustCursorPosition(int position)
        {
            // This method might prove useful in a more general case.
            if (_buffer.Length == 0)
            {
                _current = 0;
                return 0;
            }

            var maxPosition = _buffer[_buffer.Length - 1] == '\n'
                ? _buffer.Length
                : _buffer.Length - 1;

            var newCurrent = Math.Min(position, maxPosition);
            var beginning = GetBeginningOfLinePos(newCurrent);

            if (newCurrent < _buffer.Length && _buffer[newCurrent] == '\n' && (newCurrent + ViEndOfLineFactor > beginning))
            {
                newCurrent += ViEndOfLineFactor;
            }

            _current = newCurrent;
            return newCurrent;
        }
    }
}
