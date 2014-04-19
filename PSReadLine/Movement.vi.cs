using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Returns 0 if the cursor is allowed to go past the last character in the line, -1 otherwise.
        /// </summary>
        /// <seealso cref="ForwardChar"/>
        private static int ViEndOfLineFactor
        {
            get
            {
                if (_singleton._dispatchTable == _viCmdKeyMap)
                {
                    return -1;
                }
                return 0;
            }
        }

        /// <summary>
        /// Move the cursor to the end of the input.
        /// </summary>
        public static void MoveToEndOfLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._current = _singleton._buffer.Length + ViEndOfLineFactor;
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// Move the cursor forward to the end of the current word, or if between words,
        /// to the end of the next word.  Word boundaries are defined by a configurable
        /// set of characters.
        /// </summary>
        public static void NextWordEnd(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = (arg is int) ? (int) arg : 1;
            for (; qty > 0 && _singleton._current < _singleton._buffer.Length - 1; qty--)
            {
                int i = _singleton.FindNextWordEnd(_singleton.Options.WordDelimiters) - 1;
                _singleton._current = i;
                _singleton.PlaceCursor();
            }
        }

        /// <summary>
        /// Find the end of the current/next word as defined by wordDelimiters and whitespace.
        /// </summary>
        private int FindNextWordEnd(string wordDelimiters)
        {
            int i = _current;

            if (InWord(i, wordDelimiters))
            {
                if (i < _buffer.Length - 1 && !InWord(i + 1, wordDelimiters))
                {
                    i++;
                }
            }

            if (i == _buffer.Length)
            {
                return i;
            }

            if (!InWord(i, wordDelimiters))
            {
                // Scan to end of current non-word region
                while (i < _buffer.Length)
                {
                    if (InWord(i, wordDelimiters))
                    {
                        break;
                    }
                    i += 1;
                }
            }
            while (i < _buffer.Length)
            {
                if (!InWord(i, wordDelimiters))
                {
                    break;
                }
                i += 1;
            }
            return i;
        }

        /// <summary>
        /// Move to the column indicated by arg.
        /// </summary>
        public static void GotoColumn(ConsoleKeyInfo? key = null, object arg = null)
        {
            int col = (arg is int) ? (int) arg : -1;
            if (col < 0 ) {
                Ding();
                return;
            }

            if (col < _singleton._buffer.Length + ViEndOfLineFactor)
            {
                _singleton._current = Math.Min(col, _singleton._buffer.Length) - 1;
            }
            else
            {
                _singleton._current = _singleton._buffer.Length + ViEndOfLineFactor;
                Ding();
            }
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// Move the cursor to the first non-blank character in the line.
        /// </summary>
        public static void GotoFirstNonBlankOfLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            for (int i = 0; i < _singleton._buffer.Length; i++)
            {
                if (!Char.IsWhiteSpace(_singleton._buffer[i]))
                {
                    _singleton._current = i;
                    _singleton.PlaceCursor();
                    return;
                }
            }
        }
    }
}
