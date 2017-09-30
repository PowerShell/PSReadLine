/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    internal struct Point
    {
        public int X;
        public int Y;

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0},{1}", X, Y);
        }
    }

    public partial class PSConsoleReadLine
    {
        struct RenderedLineData
        {
            public string line;
            public int columns;
        }

        class RenderData
        {
            public bool errorPrompt;
            public RenderedLineData[] lines;
        }

        private const int COMMON_WIDEST_CONSOLE_WIDTH = 160;
        private readonly List<StringBuilder> _consoleBufferLines = new List<StringBuilder>(1) {new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH)};
        private static readonly string[] _spaces = new string[80];
        private RenderData _previousRender;
        private static readonly RenderData _initialPrevRender = new RenderData
        {
            lines = new[] { new RenderedLineData{ columns = 0, line = ""}}
        };
        private int _initialX;
        private int _initialY;
        private int _current;
        private int _emphasisStart;
        private int _emphasisLength;

        private class SavedTokenState
        {
            internal Token[] Tokens { get; set; }
            internal int Index { get; set; }
            internal string Color { get; set; }
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
                // sure we don't use old tokens, also allow garbage to get collected.
                _tokens = null;
                _ast = null;
                _parseErrors = null;
                return;
            }

            ForceRender();
        }

        private void ForceRender()
        {
            var defaultColor = VTColorUtils.MapColorToEscapeSequence(_console.ForegroundColor, isBackground: false) +
                               VTColorUtils.MapColorToEscapeSequence(_console.BackgroundColor, isBackground: true);

            // Geneate a sequence of logical lines with escape sequences for coloring.
            int logicalLineCount = GenerateRender(defaultColor);

            // Now write that out (and remember what we did so we can clear previous renders
            // and minimize writing more than necessary on the next render.)

            var renderLines = new RenderedLineData[logicalLineCount];
            var renderData = new RenderData {lines = renderLines};
            for (var i = 0; i < logicalLineCount; i++)
            {
                var line = _consoleBufferLines[i].ToString();
                renderLines[i].line = line;
                renderLines[i].columns = LengthInBufferCells(line);
            }

            // And then do the real work of writing to the screen.
            // Rendering data is in reused
            ReallyRender(renderData, defaultColor);

            // Cleanup some excess buffers, saving a few because we know we'll use them.
            var bufferCount = _consoleBufferLines.Count;
            var excessBuffers = bufferCount - renderLines.Length;
            if (excessBuffers > 5)
            {
                _consoleBufferLines.RemoveRange(renderLines.Length, excessBuffers);
            }
        }

        private int GenerateRender(string defaultColor)
        {
            var text = ParseInput();

            string color = defaultColor;
            string activeColor = "";
            bool afterLastToken = false;
            int currentLogicalLine = 0;

            void UpdateColorsIfNecessary(string newColor)
            {
                if (!object.ReferenceEquals(newColor, activeColor))
                {
                    _consoleBufferLines[currentLogicalLine].Append(newColor);
                    activeColor = newColor;
                }
            }

            void MaybeEmphasize(int i, string currColor)
            {
                if (i >= _emphasisStart && i < (_emphasisStart + _emphasisLength))
                {
                    currColor = _options._emphasisColor;
                }
                UpdateColorsIfNecessary(currColor);
            }

            foreach (var buf in _consoleBufferLines)
            {
                buf.Clear();
            }

            var tokenStack = new Stack<SavedTokenState>();
            tokenStack.Push(new SavedTokenState
            {
                Tokens = _tokens,
                Index = 0,
                Color = defaultColor
            });

            bool selectionNeedsTerminating = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (_visualSelectionCommandCount > 0)
                {
                    GetRegion(out int start, out int length);
                    if (i == start)
                    {
                        _consoleBufferLines[currentLogicalLine].Append("\x1b[7m");
                        selectionNeedsTerminating = true;
                    }
                    else if (i == start + length)
                    {
                        _consoleBufferLines[currentLogicalLine].Append("\x1b[27m");
                        selectionNeedsTerminating = false;
                    }
                }
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
                                color = defaultColor;
                            }
                            else
                            {
                                state = tokenStack.Peek();
                            }
                        }

                        if (!afterLastToken)
                        {
                            color = state.Color;
                            token = state.Tokens[++state.Index];
                        }
                    }

                    if (!afterLastToken && i == token.Extent.StartOffset)
                    {
                        color = GetTokenColor(token);

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
                                    Color = color
                                });

                                if (i == tokens[0].Extent.StartOffset)
                                {
                                    color = GetTokenColor(tokens[0]);
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

                    UpdateColorsIfNecessary(Options._continuationPromptColor);
                    foreach (char c in Options.ContinuationPrompt)
                    {
                        _consoleBufferLines[currentLogicalLine].Append(c);
                    }
                }
                else
                {
                    if (char.IsControl(charToRender))
                    {
                        MaybeEmphasize(i, color);
                        _consoleBufferLines[currentLogicalLine].Append('^');
                        _consoleBufferLines[currentLogicalLine].Append((char)('@' + charToRender));
                    }
                    else
                    {
                        MaybeEmphasize(i, color);
                        _consoleBufferLines[currentLogicalLine].Append(charToRender);
                    }
                }
            }

            if (selectionNeedsTerminating)
            {
                _consoleBufferLines[currentLogicalLine].Append("\x1b[27m");
            }

            if (_statusLinePrompt != null)
            {
                currentLogicalLine += 1;
                if (currentLogicalLine > _consoleBufferLines.Count - 1)
                {
                    _consoleBufferLines.Add(new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH));
                }

                color = _statusIsErrorMessage ? Options._errorColor : defaultColor;
                UpdateColorsIfNecessary(color);

                foreach (char c in _statusLinePrompt)
                {
                    _consoleBufferLines[currentLogicalLine].Append(c);
                }

                _consoleBufferLines[currentLogicalLine].Append(_statusBuffer);
            }

            return currentLogicalLine + 1;
        }

        private void ReallyRender(RenderData renderData, string defaultColor)
        {
            string activeColor = "";

            void UpdateColorsIfNecessary(string newColor)
            {
                if (!object.ReferenceEquals(newColor, activeColor))
                {
                    _console.Write(newColor);
                    activeColor = newColor;
                }
            }

            // TODO: avoid writing everything.

            // Move the cursor to where we started, but make cursor invisible while we're rendering.
            _console.CursorVisible = false;
            PlaceCursor(_initialX, _initialY);

            // Possibly need to flip the color on the prompt if the error state changed.
            if (!string.IsNullOrEmpty(_options.PromptText))
            {
                renderData.errorPrompt = (_parseErrors != null && _parseErrors.Length > 0);
                if (renderData.errorPrompt != _previousRender.errorPrompt)
                {
                    // We need to update the prompt
                    _console.CursorLeft -= _options.PromptText.Length;
                    UpdateColorsIfNecessary(renderData.errorPrompt ? _options._errorColor : defaultColor);
                    _console.Write(_options.PromptText);
                    _console.Write("\x1b[0m");
                }
            }

            var bufferWidth = _console.BufferWidth;

            int PhysicalLineCount(int columns, bool isFirstLogicalLine, out int lenLastPhysicalLine)
            {
                int cnt = 1;
                if (isFirstLogicalLine)
                {
                    // The first logical line has the user prompt that we don't touch
                    // (except where we turn part to red, but we've finished that
                    // before getting here.)
                    var maxFirstLine = bufferWidth - _initialX;
                    if (columns > maxFirstLine)
                    {
                        cnt += 1;
                        columns -= maxFirstLine;
                    }
                    else
                    {
                        lenLastPhysicalLine = columns;
                        return 1;
                    }
                }

                lenLastPhysicalLine = columns % bufferWidth;
                return cnt + columns / bufferWidth;
            }

            var previousRenderLines = _previousRender.lines;
            var previousLogicalLine = 0;
            var previousPhysicalLine = 0;

            var renderLines = renderData.lines;
            var logicalLine = 0;
            var physicalLine = 0;
            var lenPrevLastLine = 0;

            for (; logicalLine < renderLines.Length; logicalLine++)
            {
                if (logicalLine != 0) _console.Write("\n");

                var lineData = renderLines[logicalLine];
                _console.Write(lineData.line);

                int lenLastLine;
                physicalLine += PhysicalLineCount(lineData.columns, logicalLine == 0, out lenLastLine);

                // Find the previous logical line (if any) that would have rendered
                // the current physical line because we may need to clear it.
                // We don't clear it unconditionally to allow things like a prompt
                // on the right side of the line.

                while (physicalLine > previousPhysicalLine
                    && previousLogicalLine < previousRenderLines.Length)
                {
                    previousPhysicalLine += PhysicalLineCount(previousRenderLines[previousLogicalLine].columns,
                                                              previousLogicalLine == 0,
                                                              out lenPrevLastLine);
                    previousLogicalLine += 1;
                }

                // Our current physical line might be in the middle of the
                // previous logical line, in which case we need to blank
                // the rest of the line, otherwise we blank just the end
                // of what was written.
                int lenToClear = 0;
                if (physicalLine == previousPhysicalLine)
                {
                    // We're on the end of the previous logical line, so we
                    // only need to clear any extra.

                    if (lenPrevLastLine > lenLastLine)
                        lenToClear = lenPrevLastLine - lenLastLine;
                }
                else if (physicalLine < previousPhysicalLine)
                {
                    // We're in the middle of a previous logical line, we
                    // need to clear to the end of the line.
                    lenToClear = bufferWidth - (lenLastLine % bufferWidth);
                    if (physicalLine == 1)
                        lenToClear -= _initialX;
                }

                if (lenToClear > 0)
                {
                    UpdateColorsIfNecessary(defaultColor);
                    _console.Write(Spaces(lenToClear));
                }
            }

            UpdateColorsIfNecessary(defaultColor);

            while (previousPhysicalLine > physicalLine)
            {
                _console.Write("\n");
                physicalLine += 1;
                var lenToClear = physicalLine == previousPhysicalLine ? lenPrevLastLine : bufferWidth;
                if (lenToClear > 0)
                {
                    _console.Write(Spaces(lenToClear));
                }
            }

            // Fewer lines than our last render? Clear them.
            for (; previousLogicalLine < previousRenderLines.Length; previousLogicalLine++)
            {
                _console.Write("\n");
                _console.Write(Spaces(previousRenderLines[previousLogicalLine].columns));
            }

            _previousRender = renderData;

            // Reset the colors after we've finished all our rendering.
            _console.Write("\x1b[0m");

            if (_initialY + physicalLine >= _console.BufferHeight)
            {
                // We had to scroll to render everything, update _initialY
                _initialY = _console.BufferHeight - physicalLine;
            }

            var point = ConvertOffsetToPoint(_current);
            PlaceCursor(point.X, point.Y);
            _console.CursorVisible = true;

            // TODO: set WindowTop if necessary

            _lastRenderTime.Restart();
        }

        private static string Spaces(int cnt)
        {
            return cnt < _spaces.Length
                ? (_spaces[cnt] ?? (_spaces[cnt] = new string(' ', cnt)))
                : new string(' ', cnt);
        }

        private static int LengthInBufferCells(string str)
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

        private static int LengthInBufferCells(char c)
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

        private string GetTokenColor(Token token)
        {
            switch (token.Kind)
            {
            case TokenKind.Comment:
                return _options._commentColor;

            case TokenKind.Parameter:
                return _options._parameterColor;

            case TokenKind.Variable:
            case TokenKind.SplattedVariable:
                return _options._variableColor;

            case TokenKind.StringExpandable:
            case TokenKind.StringLiteral:
            case TokenKind.HereStringExpandable:
            case TokenKind.HereStringLiteral:
                return _options._stringColor;

            case TokenKind.Number:
                return _options._numberColor;
            }

            if ((token.TokenFlags & TokenFlags.CommandName) != 0)
            {
                return _options._commandColor;
            }

            if ((token.TokenFlags & TokenFlags.Keyword) != 0)
            {
                return _options._keywordColor;
            }

            if ((token.TokenFlags & (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator | TokenFlags.AssignmentOperator)) != 0)
            {
                return _options._operatorColor;
            }

            if ((token.TokenFlags & TokenFlags.TypeName) != 0)
            {
                return _options._typeColor;
            }

            if ((token.TokenFlags & TokenFlags.MemberName) != 0)
            {
                return _options._memberColor;
            }

            return _options._defaultTokenColor;
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

        private void PlaceCursor(int x, int y)
        {
            int statusLineCount = GetStatusLineCount();
            if ((y + statusLineCount) >= _console.BufferHeight)
            {
                var scrollCount = y + statusLineCount - _console.BufferHeight + 1;
                _console.ScrollBuffer(scrollCount);
                _initialY -= scrollCount;
                y -= scrollCount;
            }
            _console.SetCursorPosition(x, y);
        }

        private void MoveCursor(int newCursor)
        {
            var point = ConvertOffsetToPoint(newCursor);
            PlaceCursor(point.X, point.Y);
            _current = newCursor;
        }

        internal Point ConvertOffsetToPoint(int offset)
        {
            int x = _initialX;
            int y = _initialY;

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

            return new Point {X = x, Y = y};
        }

        private int ConvertLineAndColumnToOffset(Point point)
        {
            int offset;
            int x = _initialX;
            int y = _initialY;

            int bufferWidth = _console.BufferWidth;
            var continuationPromptLength = Options.ContinuationPrompt.Length;
            for (offset = 0; offset < _buffer.Length; offset++)
            {
                // If we are on the correct line, return when we find
                // the correct column
                if (point.Y == y && point.X <= x)
                {
                    return offset;
                }
                char c = _buffer[offset];
                if (c == '\n')
                {
                    // If we are about to move off of the correct line,
                    // the line was shorter than the column we wanted so return.
                    if (point.Y == y)
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
            return (point.Y == y) ? offset : -1;
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
            return key.IsUnmodifiedChar('y') || key.IsUnmodifiedChar('Y');
        }

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
            var point = _singleton.ConvertOffsetToPoint(_singleton._buffer.Length);

            var console = _singleton._console;
            var newTop = point.Y - console.WindowHeight + 1;

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
    }
}
