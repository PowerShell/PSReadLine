using System;
using System.IO;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Moves the cursor to the beginning of the first line.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="arg"></param>
        public void MoveToFirstLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!LineIsMultiLine())
                Ding(key, arg);

            var number = GetCurrentLine();

            var pos = ConvertOffsetToPoint(_singleton._current);

            pos.Y -= number -1;

            var newCurrent = ConvertLineAndColumnToOffset(pos);
            var position = GetBeginningOfLinePos(newCurrent);

            _singleton.MoveCursor(position);
        }

        /// <summary>
        /// Moves the cursor to the beginning of the last line.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="arg"></param>
        public void MoveToLastLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!LineIsMultiLine())
                Ding(key, arg);

            var count = GetLineCount();
            var number = GetCurrentLine();

            var pos = ConvertOffsetToPoint(_singleton._current);

            pos.Y += (count - number);

            var newCurrent = ConvertLineAndColumnToOffset(pos);
            var position = GetBeginningOfLinePos(newCurrent);

            _singleton.MoveCursor(position);
        }
    }
}