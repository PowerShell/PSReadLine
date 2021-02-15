using System;
using System.Text;

namespace Microsoft.PowerShell
{
    internal class Range
    {
        internal Range(int offset, int count)
        {
            Offset = offset;
            Count = count;
        }
        internal int Offset { get; }
        internal int Count { get; }
    }

    internal static partial class StringBuilderLinewiseExtensions
    {
        /// <summary>
        /// Determines the offset and the length of the fragment
        /// in the specified buffer that corresponds to a
        /// given number of lines starting from the specified line index
        /// </summary>
        /// <param name="buffer" />
        /// <param name="lineIndex" />
        /// <param name="lineCount" />
        internal static Range GetRange(this StringBuilder buffer, int lineIndex, int lineCount)
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
                    currentLine++;

                    if (!startPositionIdentified && currentLine == lineIndex)
                    {
                        startPosition = position;
                        startPositionIdentified = true;
                    }

                    if (currentLine == lineIndex + lineCount)
                    {
                        endPosition = position - 1;
                        break;
                    }
                }
            }

            return new Range(
                startPosition,
                endPosition - startPosition + 1
                );
        }

        /// <summary>
        /// Returns true if the specified position
        /// is on an empty logical line
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="cursor"></param>
        /// <returns></returns>
        public static bool IsLogigalLineEmpty(this StringBuilder buffer, int cursor)
        {
            // the cursor is on a logical line considered empty if...

            return 

                // the entire buffer is empty (by definition), or

                buffer.Length == 0 ||

                // the cursor sits at the start of the empty last line, 
                // meaning that it is past the end of the buffer and the
                // last character in the buffer is a newline character, or

                (cursor == buffer.Length && buffer[cursor - 1] == '\n') ||

                // if the cursor is on a newline character, or

                (cursor > 0 && buffer[cursor] == '\n')

                ;
        }
    }

    internal static class StringBuilderPredictionExtensions
    {
        internal static StringBuilder EndColorSection(this StringBuilder buffer, string selectionHighlighting)
        {
            return buffer.Append(VTColorUtils.AnsiReset).Append(selectionHighlighting);
        }
    }
}
