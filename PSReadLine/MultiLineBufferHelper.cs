using System;
using System.Text;

namespace Microsoft.PowerShell
{
    internal static class MultiLineBufferHelper
    {
        /// <summary>
        /// Represents a range of text (subset) of the buffer.
        /// </summary>
        public class Range
        {
            public int Offset { get; set; }
            public int Count { get; set; }
        }

        /// <summary>
        /// Determines the offset and the length of the fragment
        /// in the specified buffer that corresponds to a
        /// given number of lines starting from the specified line index
        /// </summary>
        /// <param name="buffer" />
        /// <param name="lineOffset">
        /// The 0-based number of the logical line for the current cursor position.
        /// This argument comes from a call to the <see cref="GetLogicalLineNumnber" />
        /// method and is thus guaranteed to represent a valid line number.
        /// </param>
        /// <param name="lineCount">
        /// The number of lines to be taken into account.
        /// If more lines are taken into account than there are lines available,
        /// this method still returns a valid range corresponding to the available
        /// lines from the buffer.
        /// </param>
        public static Range GetRange(StringBuilder buffer, int lineOffset, int lineCount)
        {
            var length = buffer.Length;

            var startPosition = 0;
            var startPositionIdentified = false;

            var endPosition = length - 1;

            var currentLine = 0;

            for (var position = 0; position < length; position++)
            {
                if (currentLine == lineOffset && !startPositionIdentified)
                {
                    startPosition = position;
                    startPositionIdentified = true;
                }

                if (buffer[position] == '\n')
                {
                    currentLine++;
                }

                if (currentLine == lineOffset + lineCount)
                {
                    endPosition = position;
                    break;
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