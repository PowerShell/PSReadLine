/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    private class DisplayBlockBase
    {
        private int _savedCursorLeft;
        private int _savedCursorTop;
        internal PSConsoleReadLine Singleton;
        internal int Top;

        protected void MoveCursorDown(int cnt)
        {
            var console = Singleton._console;
            while (cnt-- > 0) console.Write("\n");
        }

        protected void AdjustForActualScroll(int scrollCnt)
        {
            if (scrollCnt > 0)
            {
                Top -= scrollCnt;
                _singleton._initialY -= scrollCnt;
                _savedCursorTop -= scrollCnt;
            }
        }

        protected void AdjustForPossibleScroll(int cnt)
        {
            var console = Singleton._console;
            var scrollCnt = console.CursorTop + cnt + 1 - console.BufferHeight;
            if (scrollCnt > 0)
            {
                Top -= scrollCnt;
                _singleton._initialY -= scrollCnt;
                _savedCursorTop -= scrollCnt;
            }
        }

        protected void MoveCursorToStartDrawingPosition(IConsole console)
        {
            // Calculate the coord to place the cursor at the end of current input.
            var bufferEndPoint = Singleton.ConvertOffsetToPoint(Singleton._buffer.Length);
            // Top must be initialized before any possible adjustion by 'AdjustForPossibleScroll' or 'AdjustForActualScroll',
            // otherwise its value would be corrupted and cause rendering issue.
            Top = bufferEndPoint.Y + 1;

            if (bufferEndPoint.Y == console.BufferHeight)
            {
                // The input happens to end at the very last cell of the current buffer, so 'bufferEndPoint' is pointing to
                // the first cell at one line below the current buffer, and thus we need to scroll up the buffer.
                console.SetCursorPosition(console.BufferWidth - 1, console.BufferHeight - 1);
                // We scroll the buffer by 2 lines here, so the cursor is placed at the start of the first line after 'bufferEndPoint'.
                MoveCursorDown(2);
                bufferEndPoint.Y -= 2;
                AdjustForActualScroll(2);
            }
            else
            {
                // Move the cursor to the end of our input.
                console.SetCursorPosition(bufferEndPoint.X, bufferEndPoint.Y);
                // Move cursor to the start of the first line after our input (after 'bufferEndPoint').
                AdjustForPossibleScroll(1);
                MoveCursorDown(1);
            }
        }

        public void SaveCursor()
        {
            var console = Singleton._console;
            _savedCursorLeft = console.CursorLeft;
            _savedCursorTop = console.CursorTop;
        }

        public void RestoreCursor()
        {
            Singleton._console.SetCursorPosition(_savedCursorLeft, _savedCursorTop);
        }
    }
}