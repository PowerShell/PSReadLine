using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        private CHAR_INFO[] _consoleBuffer;
        private int _initialX;
        private int _initialY;
        private int _bufferWidth;
        private ConsoleColor _initialBackgroundColor;
        private ConsoleColor _initialForegroundColor;
        private CHAR_INFO _space;
        private int _current;
        private int _emphasisStart;
        private int _emphasisLength;

        private class SavedTokenState
        {
            internal Token[] Tokens { get; set; }
            internal int Index { get; set; }
            internal ConsoleColor BackgroundColor { get; set; }
            internal ConsoleColor ForegroundColor { get; set; }
        }

        private string ParseInput()
        {
            var text = _buffer.ToString();
            _ast = Parser.ParseInput(text, out _tokens, out _parseErrors);
            return text;
        }

        private void Render()
        {
            // If there are a bunch of keys queued up, skip rendering if we've rendered
            // recently.
            if (_queuedKeys.Count > 10 && (DateTime.Now - _lastRenderTime).Milliseconds < 50)
            {
                return;
            }

            ReallyRender();
        }

        private void ReallyRender()
        {
            _renderForDemoNeeded = false;

            var text = ParseInput();

            int statusLineCount = GetStatusLineCount();
            int bufferLineCount = ConvertOffsetToCoordinates(text.Length).Y - _initialY + 1 + _demoWindowLineCount + statusLineCount;
            int bufferWidth = Console.BufferWidth;
            if (_consoleBuffer.Length != bufferLineCount * bufferWidth)
            {
                var newBuffer = new CHAR_INFO[bufferLineCount * bufferWidth];
                Array.Copy(_consoleBuffer, newBuffer, _initialX + (Options.ExtraPromptLineCount * _bufferWidth));
                if (_consoleBuffer.Length > bufferLineCount * bufferWidth)
                {
                    // Need to erase the extra lines that we won't draw again
                    for (int i = bufferLineCount * bufferWidth; i < _consoleBuffer.Length; i++)
                    {
                        _consoleBuffer[i] = _space;
                    }
                    WriteBufferLines(_consoleBuffer, ref _initialY);
                }
                _consoleBuffer = newBuffer;
            }

            var tokenStack = new Stack<SavedTokenState>();
            tokenStack.Push(new SavedTokenState
            {
                Tokens          = _tokens,
                Index           = 0,
                BackgroundColor = _initialBackgroundColor,
                ForegroundColor = _initialForegroundColor
            });

            int j               = _initialX + (_bufferWidth * Options.ExtraPromptLineCount);
            var backgroundColor = _initialBackgroundColor;
            var foregroundColor = _initialForegroundColor;

            for (int i = 0; i < text.Length; i++)
            {
                // Figure out the color of the character - if it's in a token,
                // use the tokens color otherwise use the initial color.
                var state = tokenStack.Peek();
                var token = state.Tokens[state.Index];
                if (i == token.Extent.EndOffset)
                {
                    if (token == state.Tokens[state.Tokens.Length - 1])
                    {
                        tokenStack.Pop();
                        state = tokenStack.Peek();
                    }
                    foregroundColor = state.ForegroundColor;
                    backgroundColor = state.BackgroundColor;

                    token = state.Tokens[++state.Index];
                }

                if (i == token.Extent.StartOffset)
                {
                    GetTokenColors(token, out foregroundColor, out backgroundColor);

                    var stringToken = token as StringExpandableToken;
                    if (stringToken != null)
                    {
                        // We might have nested tokens.
                        if (stringToken.NestedTokens != null && stringToken.NestedTokens.Any())
                        {
                            var tokens = new Token[stringToken.NestedTokens.Count + 1];
                            stringToken.NestedTokens.CopyTo(tokens, 0);
                            // NestedTokens doesn't have an "EOS" token, so we use
                            // the string literal token for that purpose.
                            tokens[tokens.Length - 1] = stringToken;

                            tokenStack.Push(new SavedTokenState
                            {
                                Tokens          = tokens,
                                Index           = 0,
                                BackgroundColor = backgroundColor,
                                ForegroundColor = foregroundColor
                            });
                        }
                    }
                }

                if (text[i] == '\n')
                {
                    while ((j % bufferWidth) != 0)
                    {
                        _consoleBuffer[j++] = _space;
                    }

                    for (int k = 0; k < Options.ContinuationPrompt.Length; k++, j++)
                    {
                        _consoleBuffer[j].UnicodeChar = Options.ContinuationPrompt[k];
                        _consoleBuffer[j].ForegroundColor = Options.ContinuationPromptForegroundColor;
                        _consoleBuffer[j].BackgroundColor = Options.ContinuationPromptBackgroundColor;
                    }
                }
                else if (char.IsControl(text[i]))
                {
                    _consoleBuffer[j].UnicodeChar = '^';
                    MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);
                    _consoleBuffer[j].UnicodeChar = (char)('@' + text[i]);
                    MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);
                }
                else
                {
                    _consoleBuffer[j].UnicodeChar = text[i];
                    MaybeEmphasize(ref _consoleBuffer[j++], i, foregroundColor, backgroundColor);
                }
            }

            for (; j < (_consoleBuffer.Length - ((statusLineCount + _demoWindowLineCount) * _bufferWidth)); j++)
            {
                _consoleBuffer[j] = _space;
            }

            if (_statusLinePrompt != null)
            {
                for (int i = 0; i < _statusLinePrompt.Length; i++, j++)
                {
                    _consoleBuffer[j].UnicodeChar = _statusLinePrompt[i];
                    _consoleBuffer[j].ForegroundColor = Console.ForegroundColor;
                    _consoleBuffer[j].BackgroundColor = Console.BackgroundColor;
                }
                for (int i = 0; i < _statusBuffer.Length; i++, j++)
                {
                    _consoleBuffer[j].UnicodeChar = _statusBuffer[i];
                    _consoleBuffer[j].ForegroundColor = Console.ForegroundColor;
                    _consoleBuffer[j].BackgroundColor = Console.BackgroundColor;
                }

                for (; j < (_consoleBuffer.Length - (_demoWindowLineCount * _bufferWidth)); j++)
                {
                    _consoleBuffer[j] = _space;
                }
            }

            if (_demoMode)
            {
                RenderDemoWindow(j);
            }

            bool rendered = false;
            if (_parseErrors.Length > 0)
            {
                int promptChar = _initialX - 1 + (_bufferWidth * Options.ExtraPromptLineCount);

                while (promptChar >= 0)
                {
                    if (char.IsSymbol((char)_consoleBuffer[promptChar].UnicodeChar))
                    {
                        ConsoleColor prevColor = _consoleBuffer[promptChar].ForegroundColor;
                        _consoleBuffer[promptChar].ForegroundColor = ConsoleColor.Red;
                        WriteBufferLines(_consoleBuffer, ref _initialY);
                        rendered = true;
                        _consoleBuffer[promptChar].ForegroundColor = prevColor;
                        break;
                    }
                    promptChar -= 1;
                }
            }

            if (!rendered)
            {
                WriteBufferLines(_consoleBuffer, ref _initialY);
            }

            PlaceCursor();

            if ((_initialY + bufferLineCount + (_demoMode ? 1 : 0)) > (Console.WindowTop + Console.WindowHeight))
            {
                Console.WindowTop = _initialY + bufferLineCount + (_demoMode ? 1 : 0) - Console.WindowHeight;
            }

            _lastRenderTime = DateTime.Now;
        }

        private static void WriteBufferLines(CHAR_INFO[] buffer, ref int top)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            int bufferWidth = Console.BufferWidth;
            int bufferLineCount = buffer.Length / bufferWidth;
            if ((top + bufferLineCount) > Console.BufferHeight)
            {
                var scrollCount = (top + bufferLineCount) - Console.BufferHeight;
                ScrollBuffer(scrollCount);
                top -= scrollCount;
            }
            var bufferSize = new COORD
            {
                X = (short) bufferWidth,
                Y = (short) bufferLineCount
            };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var writeRegion = new SMALL_RECT
            {
                Top = (short) top,
                Left = 0,
                Bottom = (short) (top + bufferLineCount - 1),
                Right = (short) bufferWidth
            };
            NativeMethods.WriteConsoleOutput(handle, buffer,
                                             bufferSize, bufferCoord, ref writeRegion);
        }

        private static CHAR_INFO[] ReadBufferLines(int top, int count)
        {
            var result = new CHAR_INFO[Console.BufferWidth * count];
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var readBufferSize = new COORD {
                X = (short)Console.BufferWidth,
                Y = (short)count};
            var readBufferCoord = new COORD {X = 0, Y = 0};
            var readRegion = new SMALL_RECT
            {
                Top = (short)top,
                Left = 0,
                Bottom = (short)(top + count),
                Right = (short)(Console.BufferWidth - 1)
            };
            NativeMethods.ReadConsoleOutput(handle, result,
                readBufferSize, readBufferCoord, ref readRegion);
            return result;
        }

        private void GetTokenColors(Token token, out ConsoleColor foregroundColor, out ConsoleColor backgroundColor)
        {
            switch (token.Kind)
            {
            case TokenKind.Comment:
                foregroundColor = _options.CommentForegroundColor;
                backgroundColor = _options.CommentBackgroundColor;
                return;

            case TokenKind.Parameter:
                foregroundColor = _options.ParameterForegroundColor;
                backgroundColor = _options.ParameterBackgroundColor;
                return;

            case TokenKind.Variable:
            case TokenKind.SplattedVariable:
                foregroundColor = _options.VariableForegroundColor;
                backgroundColor = _options.VariableBackgroundColor;
                return;

            case TokenKind.StringExpandable:
            case TokenKind.StringLiteral:
            case TokenKind.HereStringExpandable:
            case TokenKind.HereStringLiteral:
                foregroundColor = _options.StringForegroundColor;
                backgroundColor = _options.StringBackgroundColor;
                return;

            case TokenKind.Number:
                foregroundColor = _options.NumberForegroundColor;
                backgroundColor = _options.NumberBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.CommandName) != 0)
            {
                foregroundColor = _options.CommandForegroundColor;
                backgroundColor = _options.CommandBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.Keyword) != 0)
            {
                foregroundColor = _options.KeywordForegroundColor;
                backgroundColor = _options.KeywordBackgroundColor;
                return;
            }

            if ((token.TokenFlags & (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator | TokenFlags.AssignmentOperator)) != 0)
            {
                foregroundColor = _options.OperatorForegroundColor;
                backgroundColor = _options.OperatorBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.TypeName) != 0)
            {
                foregroundColor = _options.TypeForegroundColor;
                backgroundColor = _options.TypeBackgroundColor;
                return;
            }

            if ((token.TokenFlags & TokenFlags.MemberName) != 0)
            {
                foregroundColor = _options.MemberForegroundColor;
                backgroundColor = _options.MemberBackgroundColor;
                return;
            }

            foregroundColor = _options.DefaultTokenForegroundColor;
            backgroundColor = _options.DefaultTokenBackgroundColor;
        }

        private void GetRegion(out int start, out int length)
        {
            if (_mark < _current)
            {
                start = _mark;
                length = _current - start;
            }
            else
            {
                start = _current;
                length = _mark - start;
            }
        }

        private bool InRegion(int i)
        {
            int start, end;
            if (_mark > _current)
            {
                start = _current;
                end = _mark;
            }
            else
            {
                start = _mark;
                end = _current;
            }
            return i >= start && i < end;
        }

        private void MaybeEmphasize(ref CHAR_INFO charInfo, int i, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            if (i >= _emphasisStart && i < (_emphasisStart + _emphasisLength))
            {
                backgroundColor = _options.EmphasisBackgroundColor;
                foregroundColor = _options.EmphasisForegroundColor;
            }
            else if (_visualSelectionCommandCount > 0 && InRegion(i))
            {
                // We can't quite emulate real console selection because it inverts
                // based on actual screen colors, our pallete is limited.  The choice
                // to invert only the lower 3 bits to change the color is somewhat
                // but looks best with the 2 default color schemes - starting PowerShell
                // from it's shortcut or from a cmd shortcut.
                foregroundColor = (ConsoleColor)((int)foregroundColor ^ 7);
                backgroundColor = (ConsoleColor)((int)backgroundColor ^ 7);
            }

            charInfo.ForegroundColor = foregroundColor;
            charInfo.BackgroundColor = backgroundColor;
        }

        private static void ScrollBuffer(int lines)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var scrollRectangle = new SMALL_RECT
            {
                Top = (short) lines,
                Left = 0,
                Bottom = (short) (lines + Console.BufferHeight - 1),
                Right = (short)Console.BufferWidth
            };
            var destinationOrigin = new COORD {X = 0, Y = 0};
            var fillChar = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            NativeMethods.ScrollConsoleScreenBuffer(handle, ref scrollRectangle, ref scrollRectangle, destinationOrigin, ref fillChar);
        }

        private void PlaceCursor(int x, int y)
        {
            int statusLineCount = GetStatusLineCount();
            if ((y + _demoWindowLineCount + statusLineCount) >= Console.BufferHeight)
            {
                ScrollBuffer((y + _demoWindowLineCount + statusLineCount) - Console.BufferHeight + 1);
                y = Console.BufferHeight - 1;
            }
            Console.SetCursorPosition(x, y);
        }

        private void PlaceCursor()
        {
            var coordinates = ConvertOffsetToCoordinates(_current);
            PlaceCursor(coordinates.X, coordinates.Y);
        }

        private COORD ConvertOffsetToCoordinates(int offset)
        {
            int x = _initialX;
            int y = _initialY + Options.ExtraPromptLineCount;

            int bufferWidth = Console.BufferWidth;
            var continuationPromptLength = Options.ContinuationPrompt.Length;
            for (int i = 0; i < offset; i++)
            {
                char c = _buffer[i];
                if (c == '\n')
                {
                    y += 1;
                    x = continuationPromptLength;
                }
                else
                {
                    x += char.IsControl(c) ? 2 : 1;
                    // Wrap?  No prompt when wrapping
                    if (x >= bufferWidth)
                    {
                        x -= bufferWidth;
                        y += 1;
                    }
                }
            }

            return new COORD {X = (short)x, Y = (short)y};
        }

        private int GetStatusLineCount()
        {
            if (_statusLinePrompt == null)
                return 0;

            return (_statusLinePrompt.Length + _statusBuffer.Length) / Console.BufferWidth + 1;
        }

        /// <summary>
        /// Notify the user based on their preference for notification.
        /// </summary>
        public static void Ding()
        {
            switch (_singleton.Options.BellStyle)
            {
            case BellStyle.None:
                break;
            case BellStyle.Audible:
                Console.Beep(_singleton.Options.DingTone, _singleton.Options.DingDuration);
                break;
            case BellStyle.Visual:
                // TODO: flash prompt? command line?
                break;
            }
        }

        // Console.WriteLine works as expected in PowerShell but not in the unit test framework
        // so we use our own special (and limited) version as we don't need WriteLine much.
        // The unit test framework redirects stdout - so it would see Console.WriteLine calls.
        // Unfortunately, we are testing exact placement of characters on the screen, so redirection
        // doesn't work for us.
        static private void WriteLine(string s)
        {
            var buffer = new List<CHAR_INFO>(Console.BufferWidth);
            foreach (char c in s)
            {
                if (c == '\n')
                {
                    while (buffer.Count % Console.BufferWidth != 0)
                    {
                        buffer.Add(new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor));
                    }
                }
                else
                {
                    buffer.Add(new CHAR_INFO(c, Console.ForegroundColor, Console.BackgroundColor));
                }
            }
            while (buffer.Count % Console.BufferWidth != 0)
            {
                buffer.Add(new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor));
            }
            int startLine = Console.CursorTop;
            WriteBufferLines(buffer.ToArray(), ref startLine);
            _singleton.PlaceCursor(0, startLine + (buffer.Count / Console.BufferWidth));
        }


        private bool PromptYesOrNo(string s)
        {
            _statusLinePrompt = s;
            Render();

            var key = ReadKey();

            _statusLinePrompt = null;
            Render();
            return key.Key == ConsoleKey.Y;
        }

        #region Demo mode

        private readonly HistoryQueue<string> _demoStrings;
        private bool _demoMode;
        private int _demoWindowLineCount;
        private bool _renderForDemoNeeded;

        /// <summary>
        /// Turn on demo mode (display events like keys pressed)
        /// </summary>
        public static void EnableDemoMode(ConsoleKeyInfo? key = null, object arg = null)
        {
            const int windowLineCount = 4;  // 1 blank line, 2 border lines, 1 line of info
            _singleton._captureKeys = true;
            _singleton._demoMode = true;
            _singleton._demoWindowLineCount = windowLineCount;
            var newBuffer = new CHAR_INFO[_singleton._consoleBuffer.Length + (windowLineCount * _singleton._bufferWidth)];
            Array.Copy(_singleton._consoleBuffer, newBuffer,
                _singleton._initialX + (_singleton.Options.ExtraPromptLineCount * _singleton._bufferWidth));
            _singleton._consoleBuffer = newBuffer;
            _singleton.Render();
        }

        /// <summary>
        /// Turn off demo mode (display events like keys pressed)
        /// </summary>
        public static void DisableDemoMode(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._savedKeys.Clear();
            _singleton._captureKeys = false;
            _singleton._demoMode = false;
            _singleton._demoStrings.Clear();
            _singleton._demoWindowLineCount = 0;
            _singleton.ClearDemoWindow();
        }


        private void RenderDemoWindow(int windowStart)
        {
            int i;

            Action<int, char> setChar = (index, c) =>
            {
                _consoleBuffer[index].UnicodeChar = c;
                _consoleBuffer[index].ForegroundColor = ConsoleColor.DarkCyan;
                _consoleBuffer[index].BackgroundColor = ConsoleColor.White;
            };

            for (i = 0; i < _bufferWidth; i++)
            {
                _consoleBuffer[windowStart + i].UnicodeChar = ' ';
                _consoleBuffer[windowStart + i].ForegroundColor = _initialForegroundColor;
                _consoleBuffer[windowStart + i].BackgroundColor = _initialBackgroundColor;
            }
            windowStart += _bufferWidth;

            const int extraSpace = 2;
            // Draw the box
            setChar(windowStart + extraSpace, (char)9484); // upper left
            setChar(windowStart + _bufferWidth * 2 + extraSpace, (char)9492); // lower left
            setChar(windowStart + _bufferWidth - 1 - extraSpace, (char)9488); // upper right
            setChar(windowStart + _bufferWidth * 3 - 1 - extraSpace, (char)9496); // lower right
            setChar(windowStart + _bufferWidth + extraSpace, (char)9474); // side
            setChar(windowStart + _bufferWidth * 2 - 1 - extraSpace, (char)9474); // side

            for (i = 1 + extraSpace; i < _bufferWidth - 1 - extraSpace; i++)
            {
                setChar(windowStart + i, (char)9472);
                setChar(windowStart + i + 2 * _bufferWidth, (char)9472);
            }

            while (_savedKeys.Count > 0)
            {
                var key = _savedKeys.Dequeue();
                _demoStrings.Enqueue(key.ToGestureString());
            }

            int charsToDisplay = _bufferWidth - 2 - (2 * extraSpace);
            i = windowStart + _bufferWidth + 1 + extraSpace;
            bool first = true;
            for (int j = _demoStrings.Count; j > 0; j--)
            {
                string eventString = _demoStrings[j - 1];
                if ((eventString.Length + (first ? 0 : 1)) > charsToDisplay)
                    break;

                if (!first)
                {
                    setChar(i++, ' ');
                    charsToDisplay--;
                }

                foreach (char c in eventString)
                {
                    setChar(i, c);
                    if (first)
                    {
                        // Invert the first word to highlight it
                        var color = _consoleBuffer[i].ForegroundColor;
                        _consoleBuffer[i].ForegroundColor = _consoleBuffer[i].BackgroundColor;
                        _consoleBuffer[i].BackgroundColor = color;
                    }
                    i++;
                    charsToDisplay--;
                }

                first = false;
            }
            while (charsToDisplay-- > 0)
            {
                setChar(i++, ' ');
            }
        }

        private void ClearDemoWindow()
        {
            int bufferWidth = Console.BufferWidth;
            var charInfoBuffer = new CHAR_INFO[bufferWidth * 3];

            for (int i = 0; i < charInfoBuffer.Length; i++)
            {
                charInfoBuffer[i].UnicodeChar = ' ';
                charInfoBuffer[i].ForegroundColor = _initialForegroundColor;
                charInfoBuffer[i].BackgroundColor= _initialBackgroundColor;
            }

            int bufferLineCount = ConvertOffsetToCoordinates(_buffer.Length).Y - _initialY + 1;
            int y = _initialY + bufferLineCount + 1;
            WriteBufferLines(charInfoBuffer, ref y);
        }

        #endregion Demo mode
    }
}
