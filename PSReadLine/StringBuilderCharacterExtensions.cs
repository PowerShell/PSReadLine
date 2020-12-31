using System.Text;

namespace Microsoft.PowerShell
{
    internal static partial class StringBuilderExtensions
    {
        /// <summary>
        /// Returns true if the character at the specified position is a visible whitespace character.
        /// A blank character is defined as a SPACE or a TAB.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        public static bool IsVisibleBlank(this StringBuilder buffer, int i)
        {
            var c = buffer[i];

            // [:blank:] of vim's pattern matching behavior
            // defines blanks as SPACE and TAB characters.

            return c == ' ' || c == '\t';
        }

        /// <summary>
        /// Returns true if the character at the specified position is
        /// not present in a list of word-delimiter characters.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="i"></param>
        /// <param name="wordDelimiters"></param>
        /// <returns></returns>
        public static bool InWord(this StringBuilder buffer, int i, string wordDelimiters)
        {
            return Character.IsInWord(buffer[i], wordDelimiters);
        }

        /// <summary>
        /// Returns true if the character at the specified position is 
        /// at the end of the buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        public static bool IsAtEndOfBuffer(this StringBuilder buffer, int i)
        {
            return i >= (buffer.Length - 1);
        }

        /// <summary>
        /// Returns true if the character at the specified position is
        /// a unicode whitespace character.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        public static bool IsWhiteSpace(this StringBuilder buffer, int i)
        { 
            // Treat just beyond the end of buffer as whitespace because
            // it looks like whitespace to the user even though they haven't
            // entered a character yet.
            return i >= buffer.Length || char.IsWhiteSpace(buffer[i]);
        }
    }

    public static class Character
    {
        /// <summary>
        /// Returns true if the character not present in a list of word-delimiter characters.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="wordDelimiters"></param>
        /// <returns></returns>
        public static bool IsInWord(char c, string wordDelimiters)
        {
            return !char.IsWhiteSpace(c) && wordDelimiters.IndexOf(c) < 0;
        }
    }
}
