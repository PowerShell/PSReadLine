using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Returns the position of the beginning of line
        /// starting from the specified "current" position.
        /// </summary>
        /// <param name="current">The position in the current logical line.</param>
        private static int GetBeginningOfLinePos(int current)
        {
            var newCurrent = current;

            if (_singleton.LineIsMultiLine())
            {
                int i = Math.Max(0, current - 1);
                for (; i > 0; i--)
                {
                    if (_singleton._buffer[i] == '\n')
                    {
                        i += 1;
                        break;
                    }
                }

                newCurrent = i;
            }
            else
            {
                newCurrent = 0;
            }

            return newCurrent;
        }

        /// <summary>
        /// Returns the position of the end of the logical line
        /// as specified by the "current" position.
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private static int GetEndOfLogicalLinePos(int current)
        {
            var newCurrent = current;

            for (var position = newCurrent; position < _singleton._buffer.Length; position++)
            {
                if (_singleton._buffer[position] == '\n')
                {
                    break;
                }

                newCurrent = position;
            }

            return newCurrent;
        }

        /// <summary>
        /// Returns the position of the first non whitespace character in
        /// the current logical line as specified by the "current" position.
        /// </summary>
        /// <param name="current">The position in the current logical line.</param>
        private static int GetFirstNonBlankOfLogicalLinePos(int current)
        {
            var beginningOfLine = GetBeginningOfLinePos(current);

            var newCurrent = beginningOfLine;

            while (IsVisibleBlank(newCurrent))
                newCurrent++;

            return newCurrent;
        }

        private static bool IsVisibleBlank(int newCurrent)
        {
            var c = _singleton._buffer[newCurrent];

            // [:blank:] of vim's pattern matching behavior
            // defines blanks as SPACE and TAB characters.

            return c == ' ' || c == '\t';
        }
    }
}
