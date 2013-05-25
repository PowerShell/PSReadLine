using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace PSConsoleUtilities
{
    public static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetStdHandle(uint handleId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool ScrollConsoleScreenBuffer(IntPtr hConsoleOutput,
            ref SMALL_RECT lpScrollRectangle,
            ref SMALL_RECT lpClipRectangle,
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
            if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append("Shift");
            }
            if (sb.Length > 0)
                sb.Append("+");
            if (key.Key >= ConsoleKey.A && key.Key <= ConsoleKey.Z)
            {
                sb.Append((char)('A' + key.Key - ConsoleKey.A));
            }
            else if (key.Key >= ConsoleKey.D0 && key.Key <= ConsoleKey.D9)
            {
                sb.Append((char)('0' + key.Key - ConsoleKey.D0));
            }
            else switch (key.Key)
            {
            case ConsoleKey.Escape:      sb.Append("Esc"); break;
            case ConsoleKey.PageUp:      sb.Append("PgUp"); break;
            case ConsoleKey.PageDown:    sb.Append("PgDn"); break;
            case ConsoleKey.Delete:      sb.Append("Del"); break;
            case ConsoleKey.Insert:      sb.Append("Ins"); break;
            case ConsoleKey.PrintScreen: sb.Append("PrtSc"); break;
            case ConsoleKey.Backspace:   sb.Append("BS"); break;
            case ConsoleKey.Oem1:        sb.Append("Semicolon"); break;
            case ConsoleKey.OemPlus:     sb.Append("Plus"); break;
            case ConsoleKey.OemComma:    sb.Append("Comma"); break;
            case ConsoleKey.OemMinus:    sb.Append("Minus"); break;
            case ConsoleKey.OemPeriod:   sb.Append("Period"); break;
            case ConsoleKey.Oem2:        sb.Append("Question"); break;
            case ConsoleKey.Oem3:        sb.Append("Tilde"); break;
            case ConsoleKey.Oem4:        sb.Append("Openbrackets"); break;
            case ConsoleKey.Oem5:        sb.Append("Pipe"); break;
            case ConsoleKey.Oem6:        sb.Append("Closebrackets"); break;
            case ConsoleKey.Oem7:        sb.Append("Quotes"); break;
            case ConsoleKey.Oem102:      sb.Append("Backslash"); break;
            default:
                sb.Append(key.Key);
                break;
            }
            return sb.ToString();
        }
    }
}
