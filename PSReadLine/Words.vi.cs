using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        private int ViFindNextWordPoint(string wordDelimiters)
        {
            return ViFindNextWordPoint(_current, wordDelimiters);
        }

        private int ViFindNextWordPoint(int i, string wordDelimiters)
        {
            if (IsAtEndOfLine(i))
            {
                return i;
            }
            if (InWord(i, wordDelimiters))
            {
                return ViFindNextWordFromWord(i, wordDelimiters);
            }
            if (IsDelimiter(i, wordDelimiters))
            {
                return ViFindNextWordFromDelimiter(i, wordDelimiters);
            }
            return ViFindNextWordFromWhiteSpace(i, wordDelimiters);
        }

        private int ViFindNextWordFromWhiteSpace(int i, string wordDelimiters)
        {
            while (!IsAtEndOfLine(i) && IsWhiteSpace(i))
            {
                i++;
            }
            return i;
        }

        private int ViFindNextWordFromDelimiter(int i, string wordDelimiters)
        {
            while (!IsAtEndOfLine(i) && IsDelimiter(i, wordDelimiters))
            {
                i++;
            }
            if (IsAtEndOfLine(i))
            {
                return i;
            }
            while (!IsAtEndOfLine(i) && IsWhiteSpace(i))
            {
                i++;
            }
            return i;
        }

        private bool IsAtEndOfLine(int i)
        {
            return i >= (_buffer.Length - 1);
        }

        private int ViFindNextWordFromWord(int i, string wordDelimiters)
        {
            while (!IsAtEndOfLine(i) && InWord(i, wordDelimiters))
            {
                i++;
            }
            if (IsAtEndOfLine(i))
            {
                return i;
            }
            if (IsDelimiter(i, wordDelimiters))
            {
                return i;
            }
            while (!IsAtEndOfLine(i) && IsWhiteSpace(i))
            {
                i++;
            }
            return i;
        }

        private bool IsWhiteSpace(int i)
        {
            return char.IsWhiteSpace(_buffer[i]);
        }

        /// <summary>
        /// Returns the beginning of the current/next word as defined by wordDelimiters and whitespace.
        /// </summary>
        private int ViFindPreviousWordPoint(string wordDelimiters)
        {
            return ViFindPreviousWordPoint(_current, wordDelimiters);
        }

        /// <summary>
        /// Returns the beginning of the current/next word as defined by wordDelimiters and whitespace.
        /// </summary>
        /// <param name="i">Current cursor location.</param>
        /// <param name="wordDelimiters">Characters used to deliminate words.</param>
        /// <returns>Location of the beginning of the previous word.</returns>
        private int ViFindPreviousWordPoint(int i, string wordDelimiters)
        {
            if (i == 0)
            {
                return i;
            }

            if (IsWhiteSpace(i)) 
            {
                return FindPreviousWordFromWhiteSpace(i, wordDelimiters);
            }
            else if (InWord(i, wordDelimiters))
            {
                return FindPreviousWordFromWord(i, wordDelimiters);
            }
            return FindPreviousWordFromDelimiter(i, wordDelimiters);
        }

        /// <summary>
        /// Knowing that you're starting with a word, find the previous start of the next word.
        /// </summary>
        private int FindPreviousWordFromWord(int i, string wordDelimiters)
        {
            i--;
            if (InWord(i, wordDelimiters))
            {
                while (i > 0 && InWord(i, wordDelimiters))
                {
                    i--;
                }
                if (i == 0 && InWord(i, wordDelimiters))
                {
                    return i;
                }
                return i + 1;
            }
            if (IsWhiteSpace(i))
            {
                while (i > 0 && IsWhiteSpace(i))
                {
                    i--;
                }
                if (i == 0)
                {
                    return i;
                }
                if (InWord(i, wordDelimiters) && InWord(i-1, wordDelimiters))
                {
                    return FindPreviousWordFromWord(i, wordDelimiters);
                }
                if (IsDelimiter(i - 1, wordDelimiters))
                {
                    FindPreviousWordFromDelimiter(i, wordDelimiters);
                }
                return i;
            }
            while (i > 0 && IsDelimiter(i, wordDelimiters))
            {
                i--;
            }
            if (i == 0 && IsDelimiter(i, wordDelimiters))
            {
                return i;
            }
            return i + 1;
        }

        /// <summary>
        /// Returns true if the cursor is on a word delimiter
        /// </summary>
        private bool IsDelimiter(int i, string wordDelimiters)
        {
            return wordDelimiters.IndexOf(_buffer[i]) >= 0;
        }

        /// <summary>
        /// Returns the cursor position of the beginning of the previous word when starting on a delimiter
        /// </summary>
        private int FindPreviousWordFromDelimiter(int i, string wordDelimiters)
        {
            i--;
            if (IsDelimiter(i, wordDelimiters))
            {
                while (i > 0 && IsDelimiter(i, wordDelimiters))
                {
                    i--;
                }
                if (i == 0 && !IsDelimiter(i, wordDelimiters))
                {
                    return i + 1;
                }
                return i;
            }
            return ViFindPreviousWordPoint(i, wordDelimiters);
        }


        /// <summary>
        /// Returns the cursor position of the beginning of the previous word when starting on white space
        /// </summary>
        private int FindPreviousWordFromWhiteSpace(int i, string wordDelimiters)
        {
            while (IsWhiteSpace(i) && i > 0)
            {
                i--;
            }
            int j = i - 1;
            if (j < 0 || !InWord(i, wordDelimiters) || char.IsWhiteSpace(_buffer[j]))
            {
                return i;
            }
            return (ViFindPreviousWordPoint(i, wordDelimiters));
        }

        /// <summary>
        /// Returns the cursor position of the previous word, ignoring all delimiters other what white space
        /// </summary>
        private int ViFindPreviousWord()
        {
            int i = _current;
            if (i == 0)
            {
                return 0;
            }
            i--;

            return ViFindPreviousWord(i);
        }

        private int ViFindPreviousWord(int i)
        {
            if (!IsWhiteSpace(i))
            {
                while (i > 0 && !IsWhiteSpace(i))
                {
                    i--;
                }
                if (!IsWhiteSpace(i))
                {
                    return i;
                }
                return i + 1;
            }
            while (i > 0 && IsWhiteSpace(i))
            {
                i--;
            }
            if (i == 0)
            {
                return i;
            }
            return ViFindPreviousWord(i);
        }

        private int ViFindNextWord()
        {
            int i = _current;
            while (!IsAtEndOfLine(i) && !IsWhiteSpace(i))
            {
                i++;
            }
            while (!IsAtEndOfLine(i) && IsWhiteSpace(i))
            {
                i++;
            }
            return i;
        }

        private int ViFindEndOfWord()
        {
            return ViFindEndOfWord(_current);
        }

        /// <summary>
        /// Find the end of the current/next word as defined by wordDelimiters and whitespace.
        /// </summary>
        private int FindNextWordEnd(string wordDelimiters)
        {
            int i = _current;

            return FindNextWordEnd(i, wordDelimiters);
        }

        /// <summary>
        /// Find the end of the current/next word as defined by wordDelimiters and whitespace.
        /// </summary>
        private int FindNextWordEnd(int i, string wordDelimiters)
        {
            if (IsAtEndOfLine(i))
            {
                return i;
            }

            if (IsDelimiter(i, wordDelimiters) && !IsDelimiter(i + 1, wordDelimiters))
            {
                i++;
                if (IsAtEndOfLine(i))
                {
                    return i;
                }
            }
            else if (InWord(i, wordDelimiters) && !InWord(i + 1, wordDelimiters))
            {
                i++;
                if (IsAtEndOfLine(i))
                {
                    return i;
                }
            }

            while (!IsAtEndOfLine(i) && IsWhiteSpace(i))
            {
                i++;
            }

            if (IsAtEndOfLine(i))
            {
                return i;
            }

            if (IsDelimiter(i, wordDelimiters))
            {
                while (!IsAtEndOfLine(i) && IsDelimiter(i, wordDelimiters))
                {
                    i++;
                }
                if (!IsDelimiter(i, wordDelimiters))
                {
                    return i - 1;
                }
            }
            else
            {
                while (!IsAtEndOfLine(i) && InWord(i, wordDelimiters))
                {
                    i++;
                }
                if (!InWord(i, wordDelimiters))
                {
                    return i - 1;
                }
            }

            return i;
        }

        /// <summary>
        /// Return the last character in a white space defined word after skipping contiguous white space.
        /// </summary>
        private int ViFindEndOfWord(int i)
        {
            if (IsAtEndOfLine(i))
            {
                return i;
            }
            i++;
            if (IsAtEndOfLine(i))
            {
                return i;
            }
            while (!IsAtEndOfLine(i) && IsWhiteSpace(i))
            {
                i++;
            }
            if (IsAtEndOfLine(i))
            {
                return i;
            }
            while (!IsAtEndOfLine(i) && !IsWhiteSpace(i))
            {
                i++;
            }
            if (IsWhiteSpace(i))
            {
                return i - 1;
            }
            return i;
        }

        private int ViFindEndOfPreviousWord()
        {
            int i = _current;

            return ViFindEndOfPreviousWord(i);
        }

        private int ViFindEndOfPreviousWord(int i)
        {
            if (IsWhiteSpace(i))
            {
                while (i > 0 && IsWhiteSpace(i))
                {
                    i--;
                }
                return i;
            }

            while (i > 0 && !IsWhiteSpace(i))
            {
                i--;
            }
            return ViFindEndOfPreviousWord(i);
        }
    }
}
