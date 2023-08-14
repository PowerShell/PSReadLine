using System;
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
    }
}
