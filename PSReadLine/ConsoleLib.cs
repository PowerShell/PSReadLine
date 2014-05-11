using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace PSConsoleUtilities
{
    public static class NativeMethods
    {
        public const uint MAPVK_VK_TO_VSC   = 0x00;
        public const uint MAPVK_VSC_TO_VK   = 0x01;
        public const uint MAPVK_VK_TO_CHAR  = 0x02;
        
        public const byte VK_SHIFT          = 0x10;
        public const byte VK_CONTROL        = 0x11;
        public const byte VK_ALT            = 0x12;
        public const uint MENU_IS_ACTIVE    = 0x01;
        public const uint MENU_IS_INACTIVE  = 0x00; // windows key

        public const uint ENABLE_PROCESSED_INPUT = 0x0001;
        public const uint ENABLE_LINE_INPUT      = 0x0002;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetStdHandle(uint handleId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr hConsoleOutput, out uint dwMode);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleOutput, uint dwMode);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool ScrollConsoleScreenBuffer(IntPtr hConsoleOutput,
            ref SMALL_RECT lpScrollRectangle,
            IntPtr lpClipRectangle,
            COORD dwDestinationOrigin,
            ref CHAR_INFO lpFill);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool WriteConsole(IntPtr hConsoleOutput, string lpBuffer, uint nNumberOfCharsToWrite, out uint lpNumberOfCharsWritten, IntPtr lpReserved);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(BreakHandler handlerRoutine, bool add);

        [DllImport("KERNEL32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool WriteConsoleOutput(IntPtr consoleOutput, CHAR_INFO[] buffer, COORD bufferSize, COORD bufferCoord, ref SMALL_RECT writeRegion);

        [DllImport("KERNEL32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool ReadConsoleOutput(IntPtr consoleOutput, [Out] CHAR_INFO[] buffer, COORD bufferSize, COORD bufferCoord, ref SMALL_RECT readRegion);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicode(uint uVirtKey, uint uScanCode, byte[] lpKeyState,
           [MarshalAs(UnmanagedType.LPArray)] [Out] char[] chars, int charMaxCount, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern short VkKeyScan(char @char);
    }

    public delegate bool BreakHandler(ConsoleBreakSignal ConsoleBreakSignal);

    public enum ConsoleBreakSignal : uint
    {
        CtrlC     = 0,
        CtrlBreak = 1,
        Close     = 2,
        Logoff    = 5,
        Shutdown  = 6,
        None      = 255,
    }

    public enum StandardHandleId : uint
    {
        Error  = unchecked((uint)-12),
        Output = unchecked((uint)-11),
        Input  = unchecked((uint)-10),
    }

    public struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;

        public SMALL_RECT(int left, int top, int right, int bottom)
        {
            Left = (short)left;
            Top = (short)top;
            Right = (short)right;
            Bottom = (short)bottom;
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", Left, Top, Right, Bottom);
        }
    }

    public struct COORD
    {
        public short X;
        public short Y;

        public COORD(int x, int y)
        {
            X = (short)x;
            Y = (short)y;
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0},{1}", X, Y);
        }
    }

    public struct CHAR_INFO
    {
        public ushort UnicodeChar;
        public ushort Attributes;

        public CHAR_INFO(char c, ConsoleColor foreground, ConsoleColor background)
        {
            UnicodeChar = c;
            Attributes = (ushort)(((int)background << 4) | (int)foreground);
        }

        [ExcludeFromCodeCoverage]
        public ConsoleColor ForegroundColor
        {
            get { return (ConsoleColor)(Attributes & 0xf); }
            set { Attributes = (ushort)((Attributes & 0xfff0) | ((int)value & 0xf)); }
        }

        [ExcludeFromCodeCoverage]
        public ConsoleColor BackgroundColor
        {
            get { return (ConsoleColor)((Attributes & 0xf0) >> 4); }
            set { Attributes = (ushort)((Attributes & 0xff0f) | (((int)value & 0xf) << 4)); }
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append((char)UnicodeChar);
            if (ForegroundColor != Console.ForegroundColor)
                sb.AppendFormat(" fg: {0}", ForegroundColor);
            if (BackgroundColor != Console.BackgroundColor)
                sb.AppendFormat(" bg: {0}", BackgroundColor);
            return sb.ToString();
        }

        [ExcludeFromCodeCoverage]
        public override bool Equals(object obj)
        {
            if (!(obj is CHAR_INFO))
            {
                return false;
            }

            var other = (CHAR_INFO)obj;
            return this.UnicodeChar == other.UnicodeChar && this.Attributes == other.Attributes;
        }

        [ExcludeFromCodeCoverage]
        public override int GetHashCode()
        {
            return UnicodeChar.GetHashCode() + Attributes.GetHashCode();
        }

    }

    public static class ConsoleKeyInfoExtension 
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
}
