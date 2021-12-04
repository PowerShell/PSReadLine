
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

        protected readonly CHAR_INFO[] buffer;
        protected readonly int _bufferWidth;
        protected readonly int _windowWidth;
        protected readonly int _bufferHeight;
        protected readonly int _windowHeight;
        protected dynamic _keyboardLayout;

        private bool _ignoreNextNewline;
        private bool _negative;
        private ConsoleColor _foregroundColor;
        private ConsoleColor _backgroundColor;

        /// <summary>
        /// Use big enough window/buffer to avoid the need to implement scrolling.
        /// </summary>
        internal TestConsole(dynamic keyboardLayout)
            : this(width: 60, height: 1000, mimicScrolling: false)
        {
            _keyboardLayout = keyboardLayout;
        }

        /// <summary>
        /// Use specific window width and height without scrolling capability.
        /// </summary>
        internal TestConsole(dynamic keyboardLayout, int width, int height)
            : this(width, height, mimicScrolling: false)
        {
            _keyboardLayout = keyboardLayout;
        }

        protected TestConsole(int width, int height, bool mimicScrolling)
        {
            BackgroundColor = ReadLine.BackgroundColors[0];
            ForegroundColor = ReadLine.Colors[0];
            CursorLeft = 0;
            CursorTop = 0;
            _bufferWidth = _windowWidth = width;
            _bufferHeight = _windowHeight = height;

            // Use a big enough buffer when we are mimicing scrolling.
            int bufferSize = mimicScrolling ? BufferWidth * 1000 : BufferWidth * BufferHeight;
            buffer = new CHAR_INFO[bufferSize];
            ClearBuffer();
        }

        internal void Init(object[] items)
        {
            this.index = 0;
            this.inputOrValidateItems = items;
            this.validationFailure = null;
        }

        public virtual ConsoleKeyInfo ReadKey()
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

        public virtual bool KeyAvailable => index < inputOrValidateItems.Length && inputOrValidateItems[index] is ConsoleKeyInfo;

        public virtual int CursorLeft { get; set; }
        public virtual int CursorTop { get; set; }

        public virtual int CursorSize { get; set; }
        public virtual bool CursorVisible { get; set; }

        public virtual int BufferWidth
        {
            get => _bufferWidth;
            set => throw new NotImplementedException();
        }

        public virtual int BufferHeight
        {
            get => _bufferHeight;
            set => throw new NotImplementedException();
        }

        public virtual int WindowWidth
        {
            get => _windowWidth;
            set => throw new NotImplementedException();
        }

        public virtual int WindowHeight
        {
            get => _windowHeight;
            set => throw new NotImplementedException();
        }

        public virtual int WindowTop { get; set; }

        public virtual ConsoleColor BackgroundColor
        {
            get => _backgroundColor;
            set => _backgroundColor = _negative ? (ConsoleColor)((int)value ^ 7) : value;
        }

        public virtual ConsoleColor ForegroundColor
        {
            get => _foregroundColor;
            set => _foregroundColor = _negative ? (ConsoleColor)((int)value ^ 7) : value;
        }

        public virtual Encoding OutputEncoding
        {
            get => Encoding.Default;
            set { }
        }

        public virtual void SetWindowPosition(int left, int top)
        {
        }

        public virtual void SetCursorPosition(int left, int top)
        {
            if (left != CursorLeft || top != CursorTop)
            {
                _ignoreNextNewline = false;
            }

            CursorLeft = left;
            CursorTop = top;
        }

        public virtual void WriteLine(string s)
        {
            // Crappy code here - no checks for a string that's too long, no scrolling.
            Write(s);
            CursorLeft = 0;
            CursorTop += 1;
        }

        public virtual void Write(string s)
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

        public virtual void BlankRestOfLine()
        {
            var writePos = CursorTop * BufferWidth + CursorLeft;
            for (int i = 0; i < BufferWidth - CursorLeft; i++)
            {
                buffer[writePos + i].UnicodeChar = ' ';
                buffer[writePos + i].BackgroundColor = BackgroundColor;
                buffer[writePos + i].ForegroundColor = ForegroundColor;
            }
        }

        public virtual void Clear()
        {
            SetCursorPosition(0, 0);
            ClearBuffer();
        }

        protected void ClearBuffer()
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

        protected static readonly char[] endEscapeChars = { 'm', 'J' };
        private static readonly ConsoleColor DefaultForeground = ReadLine.Colors[0];
        private static readonly ConsoleColor DefaultBackground = ReadLine.BackgroundColors[0];

        private static void ToggleNegative(TestConsole c, bool b)
        {
            c._negative = false;
            c.ForegroundColor = (ConsoleColor)((int)c.ForegroundColor ^ 7);
            c.BackgroundColor = (ConsoleColor)((int)c.BackgroundColor ^ 7);
            c._negative = b;
        }
        protected static readonly Dictionary<string, Action<TestConsole>> EscapeSequenceActions = new()
        {
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
            {"49", c => c.BackgroundColor = DefaultBackground},
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
            {"39", c => c.ForegroundColor = DefaultForeground},
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

    internal class BasicScrollingConsole : TestConsole
    {
        private int _offset;

        internal BasicScrollingConsole(dynamic keyboardLayout, int width, int height)
            : base(width, height, mimicScrolling: true)
        {
            _keyboardLayout = keyboardLayout;
            _offset = 0;
        }

        private void AdjustCursorWhenNeeded()
        {
            // If the last character written out happened to be in the last cell
            // of a physical line, we need to adjust the cursor.
            if (CursorLeft == BufferWidth)
            {
                CursorLeft = 0;
                CursorTop++;
            }

            // After adjusting the cursor, we may need to handle scrolling in case
            // that the cursor top went beyond the buffer height.
            if (CursorTop == BufferHeight)
            {
                _offset++;
                CursorTop--;
            }
        }

        public override void SetCursorPosition(int left, int top)
        {
            if (left < 0 || left >= _bufferWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(left), $"Value should be >= 0 and < BufferWidth({_bufferWidth}), but it's {left}.");
            }

            if (top < 0 || top >= _bufferHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(top), $"Value should be >= 0 and < BufferHeight({_bufferHeight}), but it's {top}.");
            }

            CursorLeft = left;
            CursorTop = top;
        }

        public override void WriteLine(string s)
        {
            Write(s);
            CursorLeft = 0;
            CursorTop += 1;

            AdjustCursorWhenNeeded();
        }

        public override void Write(string s)
        {
            // Crappy code here - no checks for a string that's too long, basic scrolling handling.
            var writePos = (_offset + CursorTop) * BufferWidth + CursorLeft;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == (char)0x1b)
                {
                    // Escape sequence - limited support here, and assumed to be well formed.
                    if (s[i + 1] != '[') throw new ArgumentException("Unexpected escape sequence", nameof(s));

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
                }
                else if (s[i] == '\n')
                {
                    CursorTop += 1;
                    CursorLeft = 0;

                    // Explicitly writing a new-line may trigger scrolling.
                    AdjustCursorWhenNeeded();
                    writePos = (_offset + CursorTop) * BufferWidth;
                }
                else
                {
                    AdjustCursorWhenNeeded();
                    CursorLeft += 1;

                    // When 'CursorLeft == BufferWidth', it means the current character will take up the last cell in the current physical line.
                    // Assuming the current physical line is 'Y'.
                    // In such a case, Windows Terminal will set the cursor position to be the following after writing out the character in the
                    // last cell of the current physical line:
                    //   CursorLeft: BufferWidth - 1
                    //   CursorTop: Y
                    // It doesn't directly set the cursor to be (0, Y+1) in this case, but when there are more visible characters (non-control chars)
                    // to be written, it will automatically adjust the cursor position to be (2, Y+1) after writing out the next visible character.
                    //
                    // This behavior makes handling the new-line character '\n' much easier -- you can simply set the cursor to be (0, top+1), and
                    // then take care of scrolling if needed.
                    // So, we mimic that behavior here: we allow 'CursorLeft' to be 'BufferWidth' after 'CursorLeft += 1' above, and we adjust the
                    // cursor when we are about to write a new visible character.

                    buffer[writePos].UnicodeChar = s[i];
                    buffer[writePos].BackgroundColor = BackgroundColor;
                    buffer[writePos].ForegroundColor = ForegroundColor;
                    writePos += 1;
                }
            }
        }

        public override void BlankRestOfLine()
        {
            var writePos = (_offset + CursorTop) * BufferWidth + CursorLeft;
            for (int i = 0; i < BufferWidth - CursorLeft; i++)
            {
                buffer[writePos + i].UnicodeChar = ' ';
                buffer[writePos + i].BackgroundColor = BackgroundColor;
                buffer[writePos + i].ForegroundColor = ForegroundColor;
            }
        }

        public override void Clear()
        {
            _offset = 0;
            base.Clear();
        }
    }
}
