
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Test
{
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


    internal class TestConsole : IConsole
    {
        internal int index;
        internal object[] inputOrValidateItems;
        internal Exception validationFailure;
        private readonly CHAR_INFO[] buffer;
        private readonly int _bufferWidth;
        private readonly int _bufferHeight;
        private readonly int _windowWidth;
        private readonly int _windowHeight;
        private bool _ignoreNextNewline;
        private dynamic _keyboardLayout;

        internal TestConsole(dynamic keyboardLayout)
        {
            _keyboardLayout = keyboardLayout;
            BackgroundColor = ReadLine.BackgroundColors[0];
            ForegroundColor = ReadLine.Colors[0];
            CursorLeft = 0;
            CursorTop = 0;
            _bufferWidth = _windowWidth = 60;
            _bufferHeight = _windowHeight = 1000; // big enough to avoid the need to implement scrolling
            buffer = new CHAR_INFO[BufferWidth * BufferHeight];
            ClearBuffer();
        }

        internal void Init(object[] items)
        {
            this.index = 0;
            this.inputOrValidateItems = items;
            this.validationFailure = null;
        }

        public ConsoleKeyInfo ReadKey()
        {
            while (index < inputOrValidateItems.Length)
            {
                var item = inputOrValidateItems[index++];
                if (item is ConsoleKeyInfo consoleKeyInfo)
                {
                    return consoleKeyInfo;
                }
                try
                {
                    ((Action)item)();
                }
                catch (Exception e)
                {
                    // Just remember the first exception
                    if (validationFailure == null)
                    {
                        validationFailure = e;
                    }
                    // In the hopes of avoiding additional failures, try cancelling via Ctrl+C.
                    return _keyboardLayout.Ctrl_c;
                }
            }

            validationFailure = new Exception("Shouldn't call ReadKey when there are no more keys");
            return _keyboardLayout.Ctrl_c;
        }

        public bool KeyAvailable => index < inputOrValidateItems.Length && inputOrValidateItems[index] is ConsoleKeyInfo;

        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }

        public int CursorSize { get; set; }
        public bool CursorVisible { get; set; }

        public int BufferWidth
        {
            get => _bufferWidth;
            set => throw new NotImplementedException();
        }

        public int BufferHeight
        {
            get => _bufferHeight;
            set => throw new NotImplementedException();
        }

        public int WindowWidth
        {
            get => _windowWidth;
            set => throw new NotImplementedException();
        }

        public int WindowHeight
        {
            get => _windowHeight;
            set => throw new NotImplementedException();
        }

        public int WindowTop { get; set; }

        public ConsoleColor BackgroundColor
        {
            get => _backgroundColor;
            set => _backgroundColor = Negative ? (ConsoleColor)((int)value ^ 7) : value;
        }
        private ConsoleColor _backgroundColor;

        public ConsoleColor ForegroundColor
        {
            get => _foregroundColor;
            set => _foregroundColor = Negative ? (ConsoleColor)((int)value ^ 7) : value;
        }
        private ConsoleColor _foregroundColor;

        public Encoding OutputEncoding
        {
            get => Encoding.Default;
            set { }
        }

        private bool Negative;

        public void SetWindowPosition(int left, int top)
        {
        }

        public void SetCursorPosition(int left, int top)
        {
            CursorLeft = left;
            CursorTop = top;
        }

        public void WriteLine(string s)
        {
            // Crappy code here - no checks for a string that's too long, no scrolling.
            Write(s);
            CursorLeft = 0;
            CursorTop += 1;
        }

        static readonly char[] endEscapeChars = { 'm', 'J' };
        public void Write(string s)
        {
            // Crappy code here - no checks for a string that's too long, no scrolling.
            var writePos = CursorTop * BufferWidth + CursorLeft;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == (char) 0x1b)
                {
                    // Escape sequence - limited support here, and assumed to be well formed.
                    if (s[i+1] != '[') throw new ArgumentException("Unexpected escape sequence", nameof(s));

                    var endSequence = s.IndexOfAny(endEscapeChars, i);
                    var len = endSequence - i - (s[endSequence] != 'm' ? 1 : 2);
                    var escapeSequence = s.Substring(i + 2, len);
                    foreach (var subsequence in escapeSequence.Split(';'))
                    {
                        EscapeSequenceActions[subsequence](this);
                    }
                    i = endSequence;
                    continue;
                }

                if (s[i] == '\b')
                {
                    CursorLeft -= 1;
                    if (CursorLeft < 0)
                    {
                        CursorTop -= 1;
                        CursorLeft = BufferWidth - 1;
                    }

                    _ignoreNextNewline = false;
                }
                else if (s[i] == '\n')
                {
                    if (!_ignoreNextNewline)
                    {
                        CursorTop += 1;
                        CursorLeft = 0;
                        writePos = CursorTop * BufferWidth;
                    }

                    _ignoreNextNewline = false;
                }
                else
                {
                    _ignoreNextNewline = false;

                    CursorLeft += 1;
                    if (CursorLeft == BufferWidth)
                    {
                        CursorLeft = 0;
                        CursorTop += 1;
                        _ignoreNextNewline = true;
                    }

                    buffer[writePos].UnicodeChar = s[i];
                    buffer[writePos].BackgroundColor = BackgroundColor;
                    buffer[writePos].ForegroundColor = ForegroundColor;
                    writePos += 1;
                }
            }
        }

        public void BlankRestOfLine()
        {
            var writePos = CursorTop * BufferWidth + CursorLeft;
            for (int i = 0; i < BufferWidth - CursorLeft; i++)
            {
                buffer[writePos + i].UnicodeChar = ' ';
                buffer[writePos + i].BackgroundColor = BackgroundColor;
                buffer[writePos + i].ForegroundColor = ForegroundColor;
            }
        }

        public void Clear()
        {
            SetCursorPosition(0, 0);
            ClearBuffer();
        }

        void ClearBuffer()
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = new CHAR_INFO(' ', ForegroundColor, BackgroundColor);
            }
        }

        public CHAR_INFO[] ReadBufferLines(int top, int count)
        {
            var toCopy = BufferWidth * count;
            var result = new CHAR_INFO[toCopy];
            Array.Copy(buffer, top * BufferWidth, result, 0, toCopy);
            return result;
        }

        private static readonly ConsoleColor DefaultForeground = ReadLine.Colors[0];
        private static readonly ConsoleColor DefaultBackground = ReadLine.BackgroundColors[0];

        private static void ToggleNegative(TestConsole c, bool b)
        {
            c.Negative = false;
            c.ForegroundColor = (ConsoleColor)((int)c.ForegroundColor ^ 7);
            c.BackgroundColor = (ConsoleColor)((int)c.BackgroundColor ^ 7);
            c.Negative = b;
        }
        private static readonly Dictionary<string, Action<TestConsole>> EscapeSequenceActions = new Dictionary<string, Action<TestConsole>> {
            {"7", c => ToggleNegative(c, true) },
            {"27", c => ToggleNegative(c, false) },
            {"40", c => c.BackgroundColor = ConsoleColor.Black},
            {"44", c => c.BackgroundColor = ConsoleColor.DarkBlue },
            {"42", c => c.BackgroundColor = ConsoleColor.DarkGreen},
            {"46", c => c.BackgroundColor = ConsoleColor.DarkCyan},
            {"41", c => c.BackgroundColor = ConsoleColor.DarkRed},
            {"45", c => c.BackgroundColor = ConsoleColor.DarkMagenta},
            {"43", c => c.BackgroundColor = ConsoleColor.DarkYellow},
            {"47", c => c.BackgroundColor = ConsoleColor.Gray},
            {"100", c => c.BackgroundColor = ConsoleColor.DarkGray},
            {"104", c => c.BackgroundColor = ConsoleColor.Blue},
            {"102", c => c.BackgroundColor = ConsoleColor.Green},
            {"106", c => c.BackgroundColor = ConsoleColor.Cyan},
            {"101", c => c.BackgroundColor = ConsoleColor.Red},
            {"105", c => c.BackgroundColor = ConsoleColor.Magenta},
            {"103", c => c.BackgroundColor = ConsoleColor.Yellow},
            {"107", c => c.BackgroundColor = ConsoleColor.White},
            {"30", c => c.ForegroundColor = ConsoleColor.Black},
            {"34", c => c.ForegroundColor = ConsoleColor.DarkBlue},
            {"32", c => c.ForegroundColor = ConsoleColor.DarkGreen},
            {"36", c => c.ForegroundColor = ConsoleColor.DarkCyan},
            {"31", c => c.ForegroundColor = ConsoleColor.DarkRed},
            {"35", c => c.ForegroundColor = ConsoleColor.DarkMagenta},
            {"33", c => c.ForegroundColor = ConsoleColor.DarkYellow},
            {"37", c => c.ForegroundColor = ConsoleColor.Gray},
            {"90", c => c.ForegroundColor = ConsoleColor.DarkGray},
            {"94", c => c.ForegroundColor = ConsoleColor.Blue},
            {"92", c => c.ForegroundColor = ConsoleColor.Green},
            {"96", c => c.ForegroundColor = ConsoleColor.Cyan},
            {"91", c => c.ForegroundColor = ConsoleColor.Red},
            {"95", c => c.ForegroundColor = ConsoleColor.Magenta},
            {"93", c => c.ForegroundColor = ConsoleColor.Yellow},
            {"97", c => c.ForegroundColor = ConsoleColor.White},
            {"0", c => {
                c.ForegroundColor = DefaultForeground;
                c.BackgroundColor = DefaultBackground;
            }},
            {"2J", c => c.SetCursorPosition(0, 0) }
        };
    }
}

