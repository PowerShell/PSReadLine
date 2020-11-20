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
            internal int PreviousTop;
            internal int ColumnWidth;
            internal int Rows;
            internal int Columns;

            private protected void MoveCursorDown(int cnt)
            {
                IConsole console = Singleton._console;
                while (cnt-- > 0)
                {
                    console.Write("\n");
                }
            }

            private protected void AdjustForPossibleScroll(int cnt)
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

            private protected void WriteBlankLines(int top, int count)
            {
                SaveCursor();
                Singleton._console.SetCursorPosition(0, top);
                Singleton.WriteBlankLines(count);
                RestoreCursor();
            }

            private protected static string GetItem(string item, int columnWidth)
            {
                item = HandleNewlinesForPossibleCompletions(item);
                var spacesNeeded = columnWidth - LengthInBufferCells(item);
                if (spacesNeeded > 0)
                {
                    item = item + Spaces(spacesNeeded);
                }
                else if (spacesNeeded < 0)
                {
                    item = SubstringByCells(item, columnWidth - 3) + "...";
                }

                return item;
            }
        }
    }
}
