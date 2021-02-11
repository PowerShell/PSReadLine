/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private class DisplayBlockBase
        {
            internal PSConsoleReadLine Singleton;
            internal int Top;

            protected void MoveCursorDown(int cnt)
            {
                IConsole console = Singleton._console;
                while (cnt-- > 0)
                {
                    console.Write("\n");
                }
            }

            protected void AdjustForPossibleScroll(int cnt)
            {
                IConsole console = Singleton._console;
                var scrollCnt = console.CursorTop + cnt + 1 - console.BufferHeight;
                if (scrollCnt > 0)
                {
                    Top -= scrollCnt;
                    _singleton._initialY -= scrollCnt;
                    _savedCursorTop -= scrollCnt;
                }
            }

            private int _savedCursorLeft;
            private int _savedCursorTop;

            public void SaveCursor()
            {
                IConsole console = Singleton._console;
                _savedCursorLeft = console.CursorLeft;
                _savedCursorTop = console.CursorTop;
            }

            public void RestoreCursor() => Singleton._console.SetCursorPosition(_savedCursorLeft, _savedCursorTop);
        }
    }
}
