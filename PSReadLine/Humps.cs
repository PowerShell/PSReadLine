/********************************************************************++
Copyright (c) Darkstar Developments GmbH 2023. All rights reserved.
--********************************************************************/

using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private enum FindHumpMode
        {
            CurrentOrNext,
            Next,
            Previous,
        }

        /// <summary>
        /// Find the end of the current/next camel hump.
        /// </summary>
        public int FindForwardHumpPoint(string wordDelimiters)
        {
            int i = _current;
            if (i == _buffer.Length)
                return i;

            if (!InWord(i, wordDelimiters))
            {
                // Scan to end of current non-word region
                while (i < _buffer.Length)
                {
                    if (InWord(i, wordDelimiters))
                        break;

                    i += 1;
                }
            }
            else
            {
                if (AtStartOfHump(i, wordDelimiters))
                    i += 1;

                // scan to the end of this hump
                while (i < _buffer.Length)
                {
                    if (!InHump(i, wordDelimiters))
                        break;

                    i += 1;
                }
            }

            return i;
        }


        /// <summary>
        /// Find the beginning of this camel hump.
        /// </summary>
        public int FindBackwardHumpPoint(string wordDelimiters)
        {
            int i = _current - 1;
            if (i < 0)
                return 0;

            if (!InWord(i, wordDelimiters))
            {
                // scan backwards until we are at the end of the previous word.
                while (i > 0)
                {
                    if (InWord(i, wordDelimiters))
                    {
                        // set back one step because we want to be behind the last letter of the word / hump
                        i += 1;
                        break;
                    }

                    i -= 1;
                }
            }
            else
            {
                // scan backwards until we are at the end of the previous word.
                while (i > 0)
                {
                    if (!InHump(i, wordDelimiters))
                        break;

                    i -= 1;
                }
            }

            return i;
        }

        private bool AtStartOfHump(int index, string wordDelimiters)
        {
            char c = _buffer[index];
            return AtStartOfHump(c, wordDelimiters);
        }

        private bool AtStartOfHump(char c, string wordDelimiters)
        {
            return !char.IsWhiteSpace(c) && wordDelimiters.IndexOf(c) < 0 && char.IsUpper(c);
        }

        private bool InHump(int index, string wordDelimiters)
        {
            char c = _buffer[index];
            return InHump(c, wordDelimiters);
        }

        private bool InHump(char c, string wordDelimiters)
        {
            return !char.IsWhiteSpace(c) && wordDelimiters.IndexOf(c) < 0 && char.IsLower(c);
        }
    }
}