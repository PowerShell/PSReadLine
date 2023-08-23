using System;
using System.Management.Automation;
using System.Text;

namespace Microsoft.PowerShell
{
    internal static class StringBuilderTextObjectExtensions
    {
        private const string WhiteSpace = " \n\t";

        /// <summary>
        /// Returns the position of the beginning of the current word as delimited by white space and delimiters
        /// This method differs from <see cref="ViFindPreviousWordPoint(string)"/>:
        /// - When the cursor location is on the first character of a word, <see cref="ViFindPreviousWordPoint(string)"/>
        ///   returns the position of the previous word, whereas this method returns the cursor location.
        /// - When the cursor location is in a word, both methods return the same result.
        /// This method supports VI "iw" text object.
        /// </summary>
        public static int ViFindBeginningOfWordObjectBoundary(this StringBuilder buffer, int position, string wordDelimiters)
        {
            // Cursor may be past the end of the buffer when calling this method
            // this may happen if the cursor is at the beginning of a new line.
            var i = Math.Min(position, buffer.Length - 1);

            // If starting on a word consider a text object as a sequence of characters excluding the delimiters,
            // otherwise, consider a word as a sequence of delimiters.
            var delimiters = wordDelimiters;
            var isInWord = buffer.InWord(i, wordDelimiters);

            if (isInWord)
            {
                // For the purpose of this method, whitespace character is considered a delimiter.
                delimiters += WhiteSpace;
            }
            else
            {
                char c = buffer[i];
                if ((wordDelimiters + '\n').IndexOf(c) == -1 && char.IsWhiteSpace(c))
                {
                    // Current position points to a whitespace that is not a newline.
                    delimiters = WhiteSpace;
                }
                else
                {
                    delimiters += '\n';
                }
            }

            var isTextObjectChar = isInWord
                ? (Func<char, bool>)(c => delimiters.IndexOf(c) == -1)
                : c => delimiters.IndexOf(c) != -1;

            var beginning = i;
            while (i >= 0 && isTextObjectChar(buffer[i]))
            {
                beginning = i--;
            }

            return beginning;
        }

        /// <summary>
        /// Finds the position of the beginning of the next word object starting from the specified position.
        /// If positioned on the last word in the buffer, returns buffer length + 1.
        /// This method supports VI "iw" text-object.
        /// iw: "inner word", select words. White space between words is counted too.
        /// </summary>
        public static int ViFindBeginningOfNextWordObjectBoundary(this StringBuilder buffer, int position, string wordDelimiters)
        {
            // Cursor may be past the end of the buffer when calling this method
            // this may happen if the cursor is at the beginning of a new line.
            var i = Math.Min(position, buffer.Length - 1);

            // Always skip the first newline character.
            if (buffer[i] == '\n' && i < buffer.Length - 1)
            {
                ++i;
            }

            // If starting on a word consider a text object as a sequence of characters excluding the delimiters,
            // otherwise, consider a word as a sequence of delimiters.
            var delimiters = wordDelimiters;
            var isInWord = buffer.InWord(i, wordDelimiters);

            if (isInWord)
            {
                delimiters += WhiteSpace;
            }
            else if (char.IsWhiteSpace(buffer[i]))
            {
                delimiters = " \t";
            }

            var isTextObjectChar = isInWord
                ? (Func<char, bool>)(c => delimiters.IndexOf(c) == -1)
                : c => delimiters.IndexOf(c) != -1;

            // Try to skip a second newline characters to replicate vim behaviour.
            if (buffer[i] == '\n' && i < buffer.Length - 1)
            {
                ++i;
            }

            // Skip to next non-word characters.
            while (i < buffer.Length && isTextObjectChar(buffer[i]))
            {
                ++i;
            }

            // Make sure end includes the starting position.
            return Math.Max(i, position);
        }

        /// <summary>
        /// Returns the span of text within the quotes relative to the specified position, in the corresponding logical line.
        /// If the position refers to the given start delimiter, the method returns the position immediately.
        /// If not, it first attempts to look backwards to find the start delimiter and returns its position if found.
        /// Otherwise, it look forwards to find the start delimiter and returns its position if found.
        /// Otherwise, it returns (-1, -1).
        ///
        /// If a start delimiter is found, this method then attempts to find the end delimiter within the logical line.
        /// Otherwise, it returns (-1, -1).
        /// 
        /// This method supports VI i' and i" text objects.
        /// </summary>
        public static (int Start, int End) ViFindSpanOfInnerQuotedTextObjectBoundary(this StringBuilder buffer, char delimiter, int position, int repeated = 1)
        {
            // Cursor may be past the end of the buffer when calling this method
            // this may happen if the cursor is at the beginning of a new line.

            var pos = Math.Min(position, buffer.Length - 1);

            // restrict this method to the logical line
            // corresponding to the given position

            var startOfLine = buffer.GetBeginningOfLogicalLinePos(pos);
            var endOfLine = buffer.GetEndOfLogicalLinePos(pos);

            var start = -1;
            var end = -1;

            // if on a quote we may be on a beginning or end quote
            // we need to parse the line to find out

            if (buffer[pos] == delimiter)
            {
                var count = 1;
                for (var offset = pos - 1; offset > startOfLine; offset--)
                {
                    if (buffer[offset] == delimiter)
                        count++;
                }

                // if there are an odd number of quotes up to the current position
                // the position refers to the beginning a quoted text

                if (count % 2 == 1)
                {
                    start = pos;
                }
            }

            // else look backwards

            if (start == -1)
            {
                for (var offset = pos - 1; offset > startOfLine; offset--)
                {
                    if (buffer[offset] == delimiter)
                    {
                        start = offset;
                        break;
                    }
                }
            }

            // if not found, look forwards

            if (start == -1)
            {
                for (var offset = pos; offset < endOfLine; offset++)
                {
                    if (buffer[offset] == delimiter)
                    {
                        start = offset;
                        break;
                    }
                }
            }

            // attempts to find the end quote

            if (start != -1 && start < endOfLine)
            {
                for (var offset = start + 1; offset < buffer.Length; offset++)
                {
                    if (buffer[offset] == delimiter)
                    {
                        end = offset;
                        break;
                    }
                    if (buffer[offset] == '\n')
                    {
                        break;
                    }
                }
            }

            // adjust span boundaries based upon
            // the number of repeatitions

            if (start != -1 && end != -1)
            {
                if (repeated > 1)
                {
                    end++;
                }
                else
                {
                    start++;
                }
            }

            return (start, end);
        }

        /// <summary>
        /// Returns the position of the beginning of line
        /// starting from the specified "current" position.
        /// </summary>
        /// <param name="current">The position in the current logical line.</param>
        internal static int GetBeginningOfLogicalLinePos(this StringBuilder buffer, int current)
        {
            int i = Math.Max(0, current);
            while (i > 0)
            {
                if (buffer[--i] == '\n')
                {
                    i += 1;
                    break;
                }
            }

            return i;
        }

        /// <summary>
        /// Returns the position of the end of the logical line
        /// as specified by the "current" position.
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        internal static int GetEndOfLogicalLinePos(this StringBuilder buffer, int current)
        {
            var newCurrent = current;

            for (var position = current; position < buffer.Length; position++)
            {
                if (buffer[position] == '\n')
                {
                    break;
                }

                newCurrent = position;
            }

            return newCurrent;
        }
    }
}
