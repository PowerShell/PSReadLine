/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell.Internal
{
    internal class ConhostConsole : IConsole
    {
        public ConsoleKeyInfo ReadKey()
        {
            return Console.ReadKey(true);
        }

        public bool KeyAvailable => Console.KeyAvailable;

        public int CursorLeft
        {
            get => Console.CursorLeft;
            set => Console.CursorLeft = value;
        }

        public int CursorTop
        {
            get => Console.CursorTop;
            set => Console.CursorTop = value;
        }

        // .NET doesn't implement this API, so we fake it with a commonly supported escape sequence.
        private int _unixCursorSize = 25;
        public int CursorSize
        {
            get => PlatformWindows.IsConsoleApiAvailable(input: false, output: true) ? Console.CursorSize : _unixCursorSize;
            set
            {
                if (PlatformWindows.IsConsoleApiAvailable(input: false, output: true))
                {
                    Console.CursorSize = value;
                }
                else
                {
                    _unixCursorSize = value;
                    if (value > 50)
                    {
                        // Solid blinking block
                        Write("\x1b[2 q");
                    }
                    else
                    {
                        // Blinking vertical bar
                        Write("\x1b[5 q");
                    }
                }
            }
        }

        public bool CursorVisible
        {
            get => Console.CursorVisible;
            set => Console.CursorVisible = value;
        }

        public int BufferWidth
        {
            get => Console.BufferWidth;
            set => Console.BufferWidth = value;
        }

        public int BufferHeight
        {
            get => Console.BufferHeight;
            set => Console.BufferHeight = value;
        }

        public int WindowWidth
        {
            get => Console.WindowWidth;
            set => Console.WindowWidth = value;
        }

        public int WindowHeight
        {
            get => Console.WindowHeight;
            set => Console.WindowHeight = value;
        }

        public int WindowTop
        {
            get => Console.WindowTop;
            set => Console.WindowTop = value;
        }

        public ConsoleColor BackgroundColor
        {
            get => Console.BackgroundColor;
            set => Console.BackgroundColor = value;
        }

        public ConsoleColor ForegroundColor
        {
            get => Console.ForegroundColor;
            set => Console.ForegroundColor = value;
        }

        public void SetWindowPosition(int left, int top)
        {
            Console.SetWindowPosition(left, top);
        }

        public void SetCursorPosition(int left, int top)
        {
            Console.SetCursorPosition(left, top);
        }

        public void Write(string value)
        {
            Console.Write(value);
        }

        public void WriteLine(string value)
        {
            Console.WriteLine(value);
        }

        public void ScrollBuffer(int lines)
        {
            Console.Write("\x1b[" + lines + "S");
        }

        private int _savedX, _savedY;

        public void SaveCursor()
        {
            _savedX = Console.CursorLeft;
            _savedY = Console.CursorTop;
        }

        public void RestoreCursor()
        {
            Console.SetCursorPosition(_savedX, _savedY);
        }
    }
}
