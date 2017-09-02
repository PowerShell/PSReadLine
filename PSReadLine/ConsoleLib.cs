/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PowerShell.Internal
{
#pragma warning disable 1591

    internal static class NativeMethods
    {
        public const uint MAPVK_VK_TO_VSC   = 0x00;
        public const uint MAPVK_VSC_TO_VK   = 0x01;
        public const uint MAPVK_VK_TO_CHAR  = 0x02;

        public const byte VK_SHIFT          = 0x10;
        public const byte VK_CONTROL        = 0x11;
        public const byte VK_ALT            = 0x12;
        public const uint MENU_IS_ACTIVE    = 0x01;
        public const uint MENU_IS_INACTIVE  = 0x00; // windows key

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicode(uint uVirtKey, uint uScanCode, byte[] lpKeyState,
           [MarshalAs(UnmanagedType.LPArray)] [Out] char[] chars, int charMaxCount, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern short VkKeyScan(char @char);
    }

    internal static class ConsoleKeyInfoExtension
    {
        public static string ToGestureString(this ConsoleKeyInfo key)
        {
            var mods = key.Modifiers;

            var sb = new StringBuilder();
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                sb.Append("Ctrl");
            }
            if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append("Alt");
            }

            char c = ConsoleKeyChordConverter.GetCharFromConsoleKey(key.Key,
                (mods & ConsoleModifiers.Shift) != 0 ? ConsoleModifiers.Shift : 0);
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                {
                    if (sb.Length > 0)
                        sb.Append("+");
                    sb.Append("Shift");
                }
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append(key.Key);
            }
            else
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

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

        public int CursorSize
        {
            get => Console.CursorSize;
            set => Console.CursorSize = value;
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
            /*
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var scrollRectangle = new SMALL_RECT
            {
                Top = (short) lines,
                Left = 0,
                Bottom = (short)(Console.BufferHeight - 1),
                Right = (short)Console.BufferWidth
            };
            var destinationOrigin = new Point {X = 0, Y = 0};
            var fillChar = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            NativeMethods.ScrollConsoleScreenBuffer(handle, ref scrollRectangle, IntPtr.Zero, destinationOrigin, ref fillChar);
            */
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

#pragma warning restore 1591
}
