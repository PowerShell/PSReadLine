using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Moves the cursor to the beginning of the first logical line
        /// of a multi-line buffer.
        /// </summary>
        /// <param name="key" />
        /// <param name="arg" />
        public void MoveToFirstLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!LineIsMultiLine())
            {
                Ding(key, arg);
                return;
            }

            var currentLine =  GetLogicalLineNumber(); 

            var pos = ConvertOffsetToPoint(_singleton._current);

            pos.Y -= currentLine -1;

            var newCurrent = ConvertLineAndColumnToOffset(pos);
            var position = GetBeginningOfLinePos(newCurrent);

            _singleton.MoveCursor(position);
        }

        /// <summary>
        /// Moves the cursor to the beginning of the last logical logical line.
        /// of a multi-line buffer.
        /// </summary>
        /// <param name="key" />
        /// <param name="arg" />
        public void MoveToLastLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!LineIsMultiLine())
            {
                Ding(key, arg);
                return;
            }

            var count = GetLogicalLineCount();
            var currentLine = GetLogicalLineNumber();

            var pos = ConvertOffsetToPoint(_singleton._current);

            pos.Y += (count - currentLine);

            var newCurrent = ConvertLineAndColumnToOffset(pos);
            var position = GetBeginningOfLinePos(newCurrent);

            _singleton.MoveCursor(position);
        }
    }
}