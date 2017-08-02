/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        struct RenderedLineData
        {
            public string line;
            public int columns;
        }

        private const int COMMON_WIDEST_CONSOLE_WIDTH = 160;
        private readonly List<StringBuilder> _consoleBufferLines = new List<StringBuilder>(1) {new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH)};
        private RenderedLineData[] _previousRender;
        private int _initialX;
        private int _initialY;
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

        private void MaybeParseInput()
        {
            if (_tokens == null)
            {
                ParseInput();
            }
        }

        private string ParseInput()
        {
            var text = _buffer.ToString();
            _ast = Parser.ParseInput(text, out _tokens, out _parseErrors);
            return text;
        }

        private void ClearStatusMessage(bool render)
        {
            _statusBuffer.Clear();
            _statusLinePrompt = null;
            _statusIsErrorMessage = false;
            if (render)
            {
                Render();
            }
        }

        private void Render()
        {
            // If there are a bunch of keys queued up, skip rendering if we've rendered
            // recently.
            if (_queuedKeys.Count > 10 && (_lastRenderTime.ElapsedMilliseconds < 50))
            {
                // We won't render, but most likely the tokens will be different, so make
                // sure we don't use old tokens.
                _tokens = null;
                _ast = null;
                return;
            }

            ReallyRender();
        }

        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults")]
        private void ReallyRender()
        {
            var text = ParseInput();

            var defaultBgColor = _console.BackgroundColor;
            var defaultFgColor = _console.ForegroundColor;
            var currBgColor = defaultBgColor;
            var currFgColor = defaultFgColor;
            string currentBackgroundColorSequnce = "";
            string currentForegroundColorSequnce = "";
            bool afterLastToken = false;
            int currentLogicalLine = 0;

            void UpdateColorsIfNecessary(ConsoleColor foreground, ConsoleColor background)
            {
                var newForeground = MapColorToEscapeSequence(foreground, isBackground: false);
                var newBackground = MapColorToEscapeSequence(background, isBackground: true);

                if (!object.ReferenceEquals(newForeground, currentForegroundColorSequnce))
                {
                    _consoleBufferLines[currentLogicalLine].Append(newForeground);
                    currentForegroundColorSequnce = newForeground;
                }

                if (!object.ReferenceEquals(newBackground, currentBackgroundColorSequnce))
                {
                    _consoleBufferLines[currentLogicalLine].Append(newBackground);
                    currentBackgroundColorSequnce = newBackground;
                }
            }

            void MaybeEmphasize(int i, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
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

                UpdateColorsIfNecessary(foregroundColor, backgroundColor);
            }


            var tokenStack = new Stack<SavedTokenState>();
            tokenStack.Push(new SavedTokenState
            {
                Tokens = _tokens,
                Index = 0,
                BackgroundColor = defaultBgColor,
                ForegroundColor = defaultFgColor
            });

            foreach (var buf in _consoleBufferLines)
            {
                buf.Clear();
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (!afterLastToken)
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
                            if (tokenStack.Count == 0)
                            {
                                afterLastToken = true;
                                token = null;
                                currFgColor = defaultFgColor;
                                currBgColor = defaultBgColor;
                            }
                            else
                            {
                                state = tokenStack.Peek();
                            }
                        }

                        if (!afterLastToken)
                        {
                            currFgColor = state.ForegroundColor;
                            currBgColor = state.BackgroundColor;

                            token = state.Tokens[++state.Index];
                        }
                    }

                    if (!afterLastToken && i == token.Extent.StartOffset)
                    {
                        GetTokenColors(token, out currFgColor, out currBgColor);

                        if (token is StringExpandableToken stringToken)
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
                                    Tokens = tokens,
                                    Index = 0,
                                    BackgroundColor = currBgColor,
                                    ForegroundColor = currFgColor
                                });

                                if (i == tokens[0].Extent.StartOffset)
                                {
                                    GetTokenColors(tokens[0], out currFgColor, out currBgColor);
                                }
                            }
                        }
                    }
                }

                var charToRender = text[i];
                if (charToRender == '\n')
                {
                    currentLogicalLine += 1;
                    if (currentLogicalLine > _consoleBufferLines.Count - 1)
                    {
                        _consoleBufferLines.Add(new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH));
                    }

                    UpdateColorsIfNecessary(Options.ContinuationPromptForegroundColor, Options.ContinuationPromptBackgroundColor);
                    foreach (char c in Options.ContinuationPrompt)
                    {
                        _consoleBufferLines[currentLogicalLine].Append(c);
                    }
                }
                else
                {
                    if (char.IsControl(charToRender))
                    {
                        MaybeEmphasize(i, currFgColor, currBgColor);
                        _consoleBufferLines[currentLogicalLine].Append('^');
                        _consoleBufferLines[currentLogicalLine].Append((char)('@' + charToRender));
                    }
                    else
                    {
                        MaybeEmphasize(i, currFgColor, currBgColor);
                        _consoleBufferLines[currentLogicalLine].Append(charToRender);
                    }
                }
            }

            if (_statusLinePrompt != null)
            {
                currentLogicalLine += 1;
                if (currentLogicalLine > _consoleBufferLines.Count - 1)
                {
                    _consoleBufferLines.Add(new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH));
                }

                currFgColor = _statusIsErrorMessage ? Options.ErrorForegroundColor : defaultFgColor;
                currBgColor = _statusIsErrorMessage ? Options.ErrorBackgroundColor : defaultBgColor;
                UpdateColorsIfNecessary(currFgColor, currBgColor);

                foreach (char c in _statusLinePrompt)
                {
                    _consoleBufferLines[currentLogicalLine].Append(c);
                }

                _consoleBufferLines[currentLogicalLine].Append(_statusBuffer);
            }

            PlaceCursor(_initialX, _initialY);

            var nextRender = new RenderedLineData[currentLogicalLine + 1];
            for (var i = 0; i < currentLogicalLine + 1; i++)
            {
                var line = _consoleBufferLines[i].ToString();
                nextRender[i].line = line;
                nextRender[i].columns = LengthInBufferCells(line);
            }

            for (currentLogicalLine = 0; currentLogicalLine < nextRender.Length; currentLogicalLine++)
            {
                if (currentLogicalLine != 0)
                    _console.Write("\n");

                var line = nextRender[currentLogicalLine].line;

                _console.Write(line);
                if (currentLogicalLine < _previousRender.Length)
                {
                    var prevLen = _previousRender[currentLogicalLine].columns;
                    var curLen = nextRender[currentLogicalLine].columns;
                    if (prevLen > curLen)
                    {
                        _console.Write(new string(' ', prevLen - curLen));
                    }
                }
            }

            // Fewer lines than our last render? Clear them.
            for (; currentLogicalLine < _previousRender.Length; currentLogicalLine++)
            {
                _console.Write("\n");
                _console.Write(new string(' ', _previousRender[currentLogicalLine].columns));
            }

            var bufferCount = _consoleBufferLines.Count;
            var excessBuffers = bufferCount - nextRender.Length;
            if (excessBuffers > 5)
            {
                _consoleBufferLines.RemoveRange(nextRender.Length, excessBuffers);
            }

            _previousRender = nextRender;

            // Reset the colors after we've finished all our rendering.
            _console.Write("\x1b[0m");

            var coordinates = ConvertOffsetToCoordinates(_current);
            PlaceCursor(coordinates.X, coordinates.Y);

            // TODO: set WindowTop if necessary

            _lastRenderTime.Restart();
        }

        private int LengthInBufferCells(string str)
        {
            var sum = 0;
            var len = str.Length;
            for (var i = 0; i < len; i++)
            {
                var c = str[i];
                if (c == 0x1b && (i+1) < len && str[i+1] == '[')
                {
                    // Simple escape sequence skipping
                    i += 2;
                    while (i < len && str[i] != 'm')
                        i++;

                    continue;
                }
                sum += LengthInBufferCells(c);
            }
            return sum;
        }

        private int LengthInBufferCells(char c)
        {
            if (c < 256)
            {
                // We render ^C for Ctrl+C, so return 2 for control characters
                return Char.IsControl(c) ? 2 : 1;
            }

            // The following is based on http://www.cl.cam.ac.uk/~mgk25/c/wcwidth.c
            // which is derived from http://www.unicode.org/Public/UCD/latest/ucd/EastAsianWidth.txt

            bool isWide = c >= 0x1100 &&
                (c <= 0x115f || /* Hangul Jamo init. consonants */
                 c == 0x2329 || c == 0x232a ||
                 (c >= 0x2e80 && c <= 0xa4cf &&
                  c != 0x303f) || /* CJK ... Yi */
                 (c >= 0xac00 && c <= 0xd7a3) || /* Hangul Syllables */
                 (c >= 0xf900 && c <= 0xfaff) || /* CJK Compatibility Ideographs */
                 (c >= 0xfe10 && c <= 0xfe19) || /* Vertical forms */
                 (c >= 0xfe30 && c <= 0xfe6f) || /* CJK Compatibility Forms */
                 (c >= 0xff00 && c <= 0xff60) || /* Fullwidth Forms */
                 (c >= 0xffe0 && c <= 0xffe6));
                  // We can ignore these ranges because .Net strings use surrogate pairs
                  // for this range and we do not handle surrogage pairs.
                  // (c >= 0x20000 && c <= 0x2fffd) ||
                  // (c >= 0x30000 && c <= 0x3fffd)
            return 1 + (isWide ? 1 : 0);
        }

        private static void WriteBlankLines(int count, int top)
        {
            var console = _singleton._console;
            var blanks = new CHAR_INFO[count * console.BufferWidth];
            for (int i = 0; i < blanks.Length; i++)
            {
                blanks[i].BackgroundColor = console.BackgroundColor;
                blanks[i].ForegroundColor = console.ForegroundColor;
                blanks[i].UnicodeChar = ' ';
            }
            console.WriteBufferLines(blanks, ref top);
        }

        private static CHAR_INFO[] ReadBufferLines(int top, int count)
        {
            return _singleton._console.ReadBufferLines(top, count);
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

        private static readonly string[] BackgroundColorMap = {
            "\x1b[40m", // Black
            "\x1b[44m", // DarkBlue
            "\x1b[42m", // DarkGreen
            "\x1b[46m", // DarkCyan
            "\x1b[41m", // DarkRed
            "\x1b[45m", // DarkMagenta
            "\x1b[43m", // DarkYellow
            "\x1b[47m", // Gray
            "\x1b[100m", // DarkGray
            "\x1b[104m", // Blue
            "\x1b[102m", // Green
            "\x1b[106m", // Cyan
            "\x1b[101m", // Red
            "\x1b[105m", // Magenta
            "\x1b[103m", // Yellow
            "\x1b[107m", // White
        };

        private static readonly string[] ForegroundColorMap = {
            "\x1b[30m", // Black
            "\x1b[34m", // DarkBlue
            "\x1b[32m", // DarkGreen
            "\x1b[36m", // DarkCyan
            "\x1b[31m", // DarkRed
            "\x1b[35m", // DarkMagenta
            "\x1b[33m", // DarkYellow
            "\x1b[37m", // Gray
            "\x1b[90m", // DarkGray
            "\x1b[94m", // Blue
            "\x1b[92m", // Green
            "\x1b[96m", // Cyan
            "\x1b[91m", // Red
            "\x1b[95m", // Magenta
            "\x1b[93m", // Yellow
            "\x1b[97m", // White
        };

        private string MapColorToEscapeSequence(ConsoleColor color, bool isBackground)
        {
            return (isBackground ? BackgroundColorMap : ForegroundColorMap)[(int)color];
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

        private void PlaceCursor(int x, int y)
        {
            int statusLineCount = GetStatusLineCount();
            if ((y + statusLineCount) >= _console.BufferHeight)
            {
                _console.ScrollBuffer((y + statusLineCount) - _console.BufferHeight + 1);
                y = _console.BufferHeight - 1;
            }
            _console.SetCursorPosition(x, y);
        }

        private void MoveCursor(int newCursor)
        {
            var coordinates = ConvertOffsetToCoordinates(newCursor);
            PlaceCursor(coordinates.X, coordinates.Y);
            _current = newCursor;
        }

        private COORD ConvertOffsetToCoordinates(int offset)
        {
            int x = _initialX;
            int y = _initialY + Options.ExtraPromptLineCount;

            int bufferWidth = _console.BufferWidth;
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
                    int size = LengthInBufferCells(c);
                    x += size;
                    // Wrap?  No prompt when wrapping
                    if (x >= bufferWidth)
                    {
                        int offsize = x - bufferWidth;
                        if (offsize % size == 0)
                        {
                            x -= bufferWidth;
                        }
                        else
                        {
                            x = size;
                        }
                        y += 1;
                    }
                }
            }

            //if the next character has bigger size than the remain space on this line,
            //the cursor goes to next line where the next character is.
            if (_buffer.Length > offset)
            {
                int size = LengthInBufferCells(_buffer[offset]);
                // next one is Wrapped to next line
                if (x + size > bufferWidth && (x + size - bufferWidth) % size != 0)
                {
                    x = 0;
                    y++;
                }
            }
            
            return new COORD {X = (short)x, Y = (short)y};
        }

        private int ConvertOffsetToConsoleBufferOffset(int offset, int startIndex)
        {
            int j = startIndex;
            for (int i = 0; i < offset; i++)
            {
                var c = _buffer[i];
                if (c == '\n')
                {
                    for (int k = 0; k < Options.ContinuationPrompt.Length; k++)
                    {
                        j++;
                    }
                }
                else if (LengthInBufferCells(c) > 1)
                {
                    j += 2;
                }
                else
                {
                    j++;
                }
            }
            return j;
        }

        private int ConvertLineAndColumnToOffset(COORD coord)
        {
            int offset;
            int x = _initialX;
            int y = _initialY + Options.ExtraPromptLineCount;

            int bufferWidth = _console.BufferWidth;
            var continuationPromptLength = Options.ContinuationPrompt.Length;
            for (offset = 0; offset < _buffer.Length; offset++)
            {
                // If we are on the correct line, return when we find
                // the correct column
                if (coord.Y == y && coord.X <= x)
                {
                    return offset;
                }
                char c = _buffer[offset];
                if (c == '\n')
                {
                    // If we are about to move off of the correct line,
                    // the line was shorter than the column we wanted so return.
                    if (coord.Y == y)
                    {
                        return offset;
                    }
                    y += 1;
                    x = continuationPromptLength;
                }
                else
                {
                    int size = LengthInBufferCells(c);
                    x += size;
                    // Wrap?  No prompt when wrapping
                    if (x >= bufferWidth)
                    {
                        int offsize = x - bufferWidth;
                        if (offsize % size == 0)
                        {
                            x -= bufferWidth;
                        }
                        else
                        {
                            x = size;
                        }
                        y += 1;
                    }
                }
            }

            // Return -1 if y is out of range, otherwise the last line was shorter
            // than we wanted, but still in range so just return the last offset.
            return (coord.Y == y) ? offset : -1;
        }

        private bool LineIsMultiLine()
        {
            for (int i = 0; i < _buffer.Length; i++)
            {
                if (_buffer[i] == '\n')
                    return true;
            }
            return false;
        }

        private int GetStatusLineCount()
        {
            if (_statusLinePrompt == null)
                return 0;

            return (_statusLinePrompt.Length + _statusBuffer.Length) / _console.BufferWidth + 1;
        }

        [ExcludeFromCodeCoverage]
        void IPSConsoleReadLineMockableMethods.Ding()
        {
            switch (Options.BellStyle)
            {
            case BellStyle.None:
                break;
            case BellStyle.Audible:
                Console.Beep(Options.DingTone, Options.DingDuration);
                break;
            case BellStyle.Visual:
                // TODO: flash prompt? command line?
                break;
            }
        }

        /// <summary>
        /// Notify the user based on their preference for notification.
        /// </summary>
        public static void Ding()
        {
            _singleton._mockableMethods.Ding();
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

        #region Screen scrolling

        /// <summary>
        /// Scroll the display up one screen.
        /// </summary>
        public static void ScrollDisplayUp(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            var console = _singleton._console;
            var newTop = console.WindowTop - (numericArg * console.WindowHeight);
            if (newTop < 0)
            {
                newTop = 0;
            }
            console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display up one line.
        /// </summary>
        public static void ScrollDisplayUpLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            var console = _singleton._console;
            var newTop = console.WindowTop - numericArg;
            if (newTop < 0)
            {
                newTop = 0;
            }
            console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display down one screen.
        /// </summary>
        public static void ScrollDisplayDown(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            var console = _singleton._console;
            var newTop = console.WindowTop + (numericArg * console.WindowHeight);
            if (newTop > (console.BufferHeight - console.WindowHeight))
            {
                newTop = (console.BufferHeight - console.WindowHeight);
            }
            console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display down one line.
        /// </summary>
        public static void ScrollDisplayDownLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            TryGetArgAsInt(arg, out var numericArg, +1);
            var console = _singleton._console;
            var newTop = console.WindowTop + numericArg;
            if (newTop > (console.BufferHeight - console.WindowHeight))
            {
                newTop = (console.BufferHeight - console.WindowHeight);
            }
            console.SetWindowPosition(0, newTop);
        }

        /// <summary>
        /// Scroll the display to the top.
        /// </summary>
        public static void ScrollDisplayTop(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._console.SetWindowPosition(0, 0);
        }

        /// <summary>
        /// Scroll the display to the cursor.
        /// </summary>
        public static void ScrollDisplayToCursor(ConsoleKeyInfo? key = null, object arg = null)
        {
            // Ideally, we'll put the last input line at the bottom of the window
            var coordinates = _singleton.ConvertOffsetToCoordinates(_singleton._buffer.Length);

            var console = _singleton._console;
            var newTop = coordinates.Y - console.WindowHeight + 1;

            // If the cursor is already visible, and we're on the first
            // page-worth of the buffer, then just scroll to the top (we can't
            // scroll to before the beginning of the buffer).
            //
            // Note that we don't want to just return, because the window may
            // have been scrolled way past the end of the content, so we really
            // do need to set the new window top to 0 to bring it back into
            // view.
            if (newTop < 0)
            {
                newTop = 0;
            }

            // But if the cursor won't be visible, make sure it is.
            if (newTop > console.CursorTop)
            {
                // Add 10 for some extra context instead of putting the
                // cursor on the bottom line.
                newTop = console.CursorTop - console.WindowHeight + 10;
            }

            // But we can't go past the end of the buffer.
            if (newTop > (console.BufferHeight - console.WindowHeight))
            {
                newTop = (console.BufferHeight - console.WindowHeight);
            }
            console.SetWindowPosition(0, newTop);
        }

        #endregion Screen scrolling
    }
}
