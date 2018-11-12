using System;
using System.Text;

namespace Microsoft.PowerShell
{
    public class Range
    {
        public int Offset { get; set; }
        public int Count { get; set; }
    }

    public static class StringBuilderLinewiseExtensions
    {
        /// <summary>
        /// Determines the offset and the length of the fragment
        /// in the specified buffer that corresponds to a
        /// given number of lines starting from the specified line index
        /// </summary>
        /// <param name="buffer" />
        /// <param name="lineIndex" />
        /// <param name="lineCount" />
        public static Range GetRange(this StringBuilder buffer, int lineIndex, int lineCount)
        {
            // this method considers lines by the first '\n' character from the previous line
            // up until the last non new-line character of the current line.
            //
            // buffer: line 0\nline 1\nline 2[...]\nline n
            // lines:  0....._1......_2......[...]_3......

            var length = buffer.Length;

            var startPosition = 0;
            var startPositionIdentified = false;

            var endPosition = length - 1;
            var endPositionIdentified = false;

            var currentLine = 0;

            if (lineIndex == 0)
            {
                startPosition = 0;
                startPositionIdentified = true;
            }

            for (var position = 0; position < length; position++)
            {
                if (buffer[position] == '\n')
                {
                    if (currentLine + 1 == lineIndex && !startPositionIdentified)
                    {
                        startPosition = position;
                        startPositionIdentified = true;
                    }

                    currentLine++;

                    if (currentLine == lineIndex + lineCount && !endPositionIdentified)
                    {
                        endPosition = position - 1;
                        endPositionIdentified = true;
                    }
                }
            }

            return new Range
            {
                Offset = startPosition,
                Count = endPosition - startPosition + 1,
            };
        }
    }
}