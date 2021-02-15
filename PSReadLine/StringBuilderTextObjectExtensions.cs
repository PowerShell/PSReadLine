using System;
using System.Text;

namespace Microsoft.PowerShell
{
    internal static partial class StringBuilderTextObjectExtensions
    {
        /// <summary>
        /// Returns the position of the beginning of the current word as delimited by white space and delimiters
        /// This method differs from <see cref="ViFindPreviousWordPoint(string)" />:
        /// When the cursor location is on the first character of a word, <see cref="ViFindPreviousWordPoint(string)" />
        /// returns the position of the previous word, whereas this method returns the cursor location.
        ///
        /// When the cursor location is in a word, both methods return the same result.
        ///
        /// This method supports VI "iw" text object.
        /// </summary>
        public static int ViFindBeginningOfWordObjectBoundary(this StringBuilder buffer, int position, string wordDelimiters)
        {
            // cursor may be past the end of the buffer when calling this method
            // this may happen if the cursor is at the beginning of a new line

            var i = Math.Min(position, buffer.Length - 1);

            // if starting on a word consider a text object as a sequence of characters excluding the delimiters
            // otherwise, consider a word as a sequence of delimiters
            // for the purpose of this method, a newline (\n) character is considered a delimiter.

            var ws = " \n\t";

            var delimiters = wordDelimiters;

            if (buffer.InWord(i, wordDelimiters))
            {
                delimiters += ws;
            }
            if ((wordDelimiters + '\n').IndexOf(buffer[i]) == -1 && buffer.IsWhiteSpace(i))
            {
                delimiters = ws;
            }
            else
            {
                delimiters += '\n';
            }

            var isTextObjectChar = buffer.InWord(i, wordDelimiters)
                    ? (Func<char, bool>)(c => delimiters.IndexOf(c) == -1)
                    : c => delimiters.IndexOf(c) != -1
                ;

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
            // cursor may be past the end of the buffer when calling this method
            // this may happen if the cursor is at the beginning of a new line

            var i = Math.Min(position, buffer.Length - 1);

            // always skip the first newline character

            if (buffer[i] == '\n' && i < buffer.Length - 1)
            {
                // try to skip a second newline characters
                // to replicate vim behaviour

                ++i;
            }

            // if starting on a word consider a text object as a sequence of characters excluding the delimiters
            // otherwise, consider a word as a sequence of delimiters

            var delimiters = wordDelimiters;

            if (buffer.InWord(i, wordDelimiters))
            {
                delimiters += " \t\n";
            }
            if (buffer.IsWhiteSpace(i))
            {
                delimiters = " \t";
            }

            var isTextObjectChar = buffer.InWord(i, wordDelimiters)
                    ? (Func<char, bool>)(c => delimiters.IndexOf(c) == -1)
                    : c => delimiters.IndexOf(c) != -1
                ;

            // try to skip a second newline characters
            // to replicate vim behaviour

            if (buffer[i] == '\n' && i < buffer.Length - 1)
            {
                ++i;
            }

            // skip to next non word characters

            while (i < buffer.Length && isTextObjectChar(buffer[i]))
            {
                ++i;
            }

            // make sure end includes the starting position

            return Math.Max(i, position);
        }
    }
}
