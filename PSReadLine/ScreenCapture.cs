/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    internal static class ScreenCapture
    {
        internal struct COORD
        {
            public short X;
            public short Y;

            public override string ToString()
            {
                return String.Format(CultureInfo.InvariantCulture, "{0},{1}", X, Y);
            }
        }

        internal struct CHAR_INFO
        {
            public ushort UnicodeChar;
            public ushort Attributes;

            public CHAR_INFO(char c, ConsoleColor foreground, ConsoleColor background)
            {
                UnicodeChar = c;
                Attributes = (ushort)(((int)background << 4) | (int)foreground);
            }

            public ConsoleColor ForegroundColor
            {
                get => (ConsoleColor)(Attributes & 0xf);
                set => Attributes = (ushort)((Attributes & 0xfff0) | ((int)value & 0xf));
            }

            public ConsoleColor BackgroundColor
            {
                get => (ConsoleColor)((Attributes & 0xf0) >> 4);
                set => Attributes = (ushort)((Attributes & 0xff0f) | (((int)value & 0xf) << 4));
            }

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

            public override bool Equals(object obj)
            {
                if (!(obj is CHAR_INFO))
                {
                    return false;
                }

                var other = (CHAR_INFO)obj;
                return this.UnicodeChar == other.UnicodeChar && this.Attributes == other.Attributes;
            }

            public override int GetHashCode()
            {
                return UnicodeChar.GetHashCode() + Attributes.GetHashCode();
            }
        }

        [DllImport("KERNEL32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WriteConsoleOutput(IntPtr consoleOutput, CHAR_INFO[] buffer, COORD bufferSize, COORD bufferCoord, ref SMALL_RECT writeRegion);

        [DllImport("KERNEL32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool ReadConsoleOutput(IntPtr consoleOutput, [Out] CHAR_INFO[] buffer, COORD bufferSize, COORD bufferCoord, ref SMALL_RECT readRegion);

        [StructLayout(LayoutKind.Sequential)]
        struct COLORREF
        {
            internal uint ColorDWORD;

            internal uint R => ColorDWORD & 0xff;
            internal uint G => (ColorDWORD >> 8) & 0xff;
            internal uint B => (ColorDWORD >> 16) & 0xff;
        }

        public struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;

            public override string ToString()
            {
                return String.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", Left, Top, Right, Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CONSOLE_SCREEN_BUFFER_INFO_EX
        {
            internal int cbSize;
            internal COORD dwSize;
            internal COORD dwCursorPosition;
            internal ushort wAttributes;
            internal SMALL_RECT srWindow;
            internal COORD dwMaximumWindowSize;
            internal ushort wPopupAttributes;
            internal bool bFullscreenSupported;
            internal COLORREF Black;
            internal COLORREF DarkBlue;
            internal COLORREF DarkGreen;
            internal COLORREF DarkCyan;
            internal COLORREF DarkRed;
            internal COLORREF DarkMagenta;
            internal COLORREF DarkYellow;
            internal COLORREF Gray;
            internal COLORREF DarkGray;
            internal COLORREF Blue;
            internal COLORREF Green;
            internal COLORREF Cyan;
            internal COLORREF Red;
            internal COLORREF Magenta;
            internal COLORREF Yellow;
            internal COLORREF White;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleScreenBufferInfoEx(IntPtr hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO_EX csbe);

        internal static CHAR_INFO[] ReadBufferLines(int top, int count, int bufferWidth)
        {
            var result = new CHAR_INFO[bufferWidth * count];
            var handle = PlatformWindows.GetStdHandle((uint) PlatformWindows.StandardHandleId.Output);

            var readBufferSize = new COORD {
                X = (short)bufferWidth,
                Y = (short)count};
            var readBufferCoord = new COORD {X = 0, Y = 0};
            var readRegion = new SMALL_RECT
            {
                Top = (short)top,
                Left = 0,
                Bottom = (short)(top + count),
                Right = (short)(bufferWidth - 1)
            };
            ReadConsoleOutput(handle, result, readBufferSize, readBufferCoord, ref readRegion);
            return result;
        }

        internal static void WriteBufferLines(CHAR_INFO[] buffer, int top, IConsole console)
        {
            var handle = PlatformWindows.GetStdHandle((uint) PlatformWindows.StandardHandleId.Output);

            int bufferWidth = Console.BufferWidth;
            int bufferLineCount = buffer.Length / bufferWidth;
            var bufferSize = new COORD
            {
                X = (short) bufferWidth,
                Y = (short) bufferLineCount
            };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var bottom = top + bufferLineCount - 1;
            var writeRegion = new SMALL_RECT
            {
                Top = (short) top,
                Left = 0,
                Bottom = (short) bottom,
                Right = (short) (bufferWidth - 1)
            };
            WriteConsoleOutput(handle, buffer,
                bufferSize, bufferCoord, ref writeRegion);
        }

        internal static void InvertLines(int start, int count, IConsole console)
        {
            var buffer = ReadBufferLines(start, count, console.BufferWidth);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i].ForegroundColor = (ConsoleColor)((int)buffer[i].ForegroundColor ^ 7);
                buffer[i].BackgroundColor = (ConsoleColor)((int)buffer[i].BackgroundColor ^ 7);
            }
            WriteBufferLines(buffer, start, console);
        }

        internal const string CmdColorTable = @"
\red0\green0\blue0;
\red0\green0\blue128;
\red0\green128\blue0;
\red0\green128\blue128;
\red128\green0\blue0;
\red128\green0\blue128;
\red128\green128\blue0;
\red192\green192\blue192;
\red128\green128\blue128;
\red0\green0\blue255;
\red0\green255\blue0;
\red0\green255\blue255;
\red255\green0\blue0;
\red255\green0\blue255;
\red255\green255\blue0;
\red255\green255\blue255;
";

        internal const string PowerShellColorTable = @"
\red1\green36\blue86;
\red0\green0\blue128;
\red0\green128\blue0;
\red0\green128\blue128;
\red128\green0\blue0;
\red1\green36\blue86;
\red238\green237\blue240;
\red192\green192\blue192;
\red128\green128\blue128;
\red0\green0\blue255;
\red0\green255\blue0;
\red0\green255\blue255;
\red255\green0\blue0;
\red255\green0\blue255;
\red255\green255\blue0;
\red255\green255\blue255;
";

        static string GetRTFColorFromColorRef(COLORREF colorref)
        {
            return String.Concat("\\red", colorref.R.ToString("D"),
                "\\green", colorref.G.ToString("D"),
                "\\blue", colorref.B.ToString("D"), ";");
        }

        internal static string GetColorTable(IConsole console)
        {
            var handle = PlatformWindows.GetStdHandle((uint) PlatformWindows.StandardHandleId.Output);
            var csbe = new CONSOLE_SCREEN_BUFFER_INFO_EX
            {
                cbSize = Marshal.SizeOf(typeof(CONSOLE_SCREEN_BUFFER_INFO_EX))
            };
            if (GetConsoleScreenBufferInfoEx(handle, ref csbe))
            {
                return GetRTFColorFromColorRef(csbe.Black) + GetRTFColorFromColorRef(csbe.DarkBlue) + GetRTFColorFromColorRef(csbe.DarkGreen) + GetRTFColorFromColorRef(csbe.DarkCyan) + GetRTFColorFromColorRef(csbe.DarkRed) + GetRTFColorFromColorRef(csbe.DarkMagenta) + GetRTFColorFromColorRef(csbe.DarkYellow) + GetRTFColorFromColorRef(csbe.Gray) + GetRTFColorFromColorRef(csbe.DarkGray) + GetRTFColorFromColorRef(csbe.Blue) + GetRTFColorFromColorRef(csbe.Green) + GetRTFColorFromColorRef(csbe.Cyan) + GetRTFColorFromColorRef(csbe.Red) + GetRTFColorFromColorRef(csbe.Magenta) + GetRTFColorFromColorRef(csbe.Yellow) + GetRTFColorFromColorRef(csbe.White);
            }

            // A bit of a hack if the above failed - assume PowerShell's color scheme if the
            // background color is Magenta, otherwise we assume the default scheme.
            return console.BackgroundColor == ConsoleColor.DarkMagenta
                ? PowerShellColorTable
                : CmdColorTable;
        }

        internal static void DumpScreenToClipboard(int top, int count, IConsole console)
        {
            var buffer = ReadBufferLines(top, count, console.BufferWidth);
            var bufferWidth = console.BufferWidth;

            var textBuffer = new StringBuilder(buffer.Length + count);

            var rtfBuffer = new StringBuilder();
            rtfBuffer.Append(@"{\rtf\ansi{\fonttbl{\f0 Consolas;}}");

            var colorTable = GetColorTable(console);
            rtfBuffer.AppendFormat(@"{{\colortbl;{0}}}{1}", colorTable, Environment.NewLine);
            rtfBuffer.Append(@"\f0 \fs18 ");

            var charInfo = buffer[0];
            var fgColor = (int)charInfo.ForegroundColor;
            var bgColor = (int)charInfo.BackgroundColor;
            rtfBuffer.AppendFormat(@"{{\cf{0}\chshdng0\chcbpat{1} ", fgColor + 1, bgColor + 1);
            for (int i = 0; i < count; i++)
            {
                var spaces = 0;
                var rtfSpaces = 0;
                for (int j = 0; j < bufferWidth; j++)
                {
                    charInfo = buffer[i * bufferWidth + j];
                    if ((int)charInfo.ForegroundColor != fgColor || (int)charInfo.BackgroundColor != bgColor)
                    {
                        if (rtfSpaces > 0)
                        {
                            rtfBuffer.Append(' ', rtfSpaces);
                            rtfSpaces = 0;
                        }
                        fgColor = (int)charInfo.ForegroundColor;
                        bgColor = (int)charInfo.BackgroundColor;
                        rtfBuffer.AppendFormat(@"}}{{\cf{0}\chshdng0\chcbpat{1} ", fgColor + 1, bgColor + 1);
                    }

                    var c = (char)charInfo.UnicodeChar;
                    if (c == ' ')
                    {
                        // Trailing spaces are skipped, we'll add them back if we find a non-space
                        // before the end of line
                        ++spaces;
                        ++rtfSpaces;
                    }
                    else
                    {
                        if (spaces > 0)
                        {
                            textBuffer.Append(' ', spaces);
                            spaces = 0;
                        }
                        if (rtfSpaces > 0)
                        {
                            rtfBuffer.Append(' ', rtfSpaces);
                            rtfSpaces = 0;
                        }

                        textBuffer.Append(c);
                        switch (c)
                        {
                            case '\\': rtfBuffer.Append(@"\\"); break;
                            case '\t': rtfBuffer.Append(@"\tab"); break;
                            case '{':  rtfBuffer.Append(@"\{"); break;
                            case '}':  rtfBuffer.Append(@"\}"); break;
                            default:   rtfBuffer.Append(c); break;
                        }
                    }
                }
                rtfBuffer.AppendFormat(@"\shading0 \cbpat{0} \par{1}", bgColor + 1, Environment.NewLine);
                textBuffer.Append(Environment.NewLine);
            }
            rtfBuffer.Append("}}");

            Clipboard.SetRtf(textBuffer.ToString(), rtfBuffer.ToString());
        }
    }

    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Start interactive screen capture - up/down arrows select lines, enter copies
        /// selected text to clipboard as text and html.
        /// </summary>
        public static void CaptureScreen(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Ding();
                return;
            }

            PlatformWindows.CallUsingOurInputMode(CaptureScreenImpl);
        }

        internal static void CaptureScreenImpl()
        {
            int selectionTop = _singleton._console.CursorTop;
            int selectionHeight = 1;
            int currentY = selectionTop;
            IConsole console = _singleton._console;

            // We'll keep the current selection line (currentY) at least 4 lines
            // away from the top or bottom of the window.
            const int margin = 5;
            bool TooCloseToTop() => (currentY - console.WindowTop) < margin;
            bool TooCloseToBottom() => ((console.WindowTop + console.WindowHeight) - currentY) < margin;

            void UpdateSelection()
            {
                // Shift not pressed - unselect current selection
                ScreenCapture.InvertLines(selectionTop, selectionHeight, console);
                selectionTop = currentY;
                selectionHeight = 1;
                ScreenCapture.InvertLines(selectionTop, selectionHeight, console);
            }

            // Current lines starts out selected
            ScreenCapture.InvertLines(selectionTop, selectionHeight, console);
            bool done = false;
            while (!done)
            {
                var k = ReadKey();
                if (k == Keys.K || k == Keys.ucK || k == Keys.UpArrow || k == Keys.ShiftUpArrow)
                {
                    if (TooCloseToTop())
                    {
                        ScrollDisplayUpLine();
                    }

                    if (currentY > 0)
                    {
                        currentY -= 1;
                        if (k.Shift)
                        {
                            if (currentY < selectionTop)
                            {
                                // Extend selection up, only invert newly selected line.
                                ScreenCapture.InvertLines(currentY, 1, console);
                                selectionTop = currentY;
                                selectionHeight += 1;
                            }
                            else if (currentY >= selectionTop)
                            {
                                // Selection shortend 1 line, invert unselected line.
                                ScreenCapture.InvertLines(currentY + 1, 1, console);
                                selectionHeight -= 1;
                            }
                        }
                        else
                        {
                            UpdateSelection();
                        }
                    }
                }
                else if (k == Keys.J || k == Keys.ucJ || k == Keys.DownArrow || k == Keys.ShiftDownArrow)
                {
                    if (TooCloseToBottom())
                        ScrollDisplayDownLine();

                    if (currentY < (console.BufferHeight - 1))
                    {
                        currentY += 1;
                        if (k.Shift)
                        {
                            if (currentY == (selectionTop + selectionHeight))
                            {
                                // Extend selection down, only invert newly selected line.
                                ScreenCapture.InvertLines(selectionTop + selectionHeight, 1, console);
                                selectionHeight += 1;
                            }
                            else if (currentY == (selectionTop + 1))
                            {
                                // Selection shortend 1 line, invert unselected line.
                                ScreenCapture.InvertLines(selectionTop, 1, console);
                                selectionTop = currentY;
                                selectionHeight -= 1;
                            }
                        }
                        else
                        {
                            UpdateSelection();
                        }
                    }
                }
                else if (k == Keys.Enter)
                {
                    ScreenCapture.InvertLines(selectionTop, selectionHeight, console);
                    ScreenCapture.DumpScreenToClipboard(selectionTop, selectionHeight, console);
                    ScrollDisplayToCursor();
                    return;
                }
                else if (k == Keys.Escape || k == Keys.CtrlC || k == Keys.CtrlG)
                {
                    done = true;
                }
                else
                {
                    Ding();
                }
            }
            ScreenCapture.InvertLines(selectionTop, selectionHeight, console);
            ScrollDisplayToCursor();
        }
    }
}
