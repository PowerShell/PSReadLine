using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Returns the position of the beginning of line
        /// starting from the specified "current" position.
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
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
    }
}