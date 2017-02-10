/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation.Language;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private List<RenderInst> _prevRenderInst = new List<RenderInst>(100);
        private List<RenderInst> _renderInst = new List<RenderInst>(100);
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
        private void ClearStatusMessage()
        {
            _statusBuffer.Clear();
            _statusLinePrompt = null;
            _statusIsErrorMessage = false;
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

        enum RenderOp : byte
        {
            Text,
            BgColorChange,
            FgColorChange,
            Flush,
            SaveColors,
            RestoreColors,
            ToggleInverse
        }

        struct RenderInst
        {
            public RenderOp op;
            public char text;
            public ConsoleColor color;
            const ConsoleColor NoColor = (ConsoleColor)(-1);

            public static RenderInst Char(char c)
            {
                return new RenderInst {op = RenderOp.Text, text = c, color = NoColor};
            }

            public static RenderInst ChangeBgColor(ConsoleColor bgColor)
            {
                return new RenderInst {op = RenderOp.BgColorChange, color = bgColor};
            }

            public static RenderInst ChangeFgColor(ConsoleColor fgColor)
            {
                return new RenderInst {op = RenderOp.FgColorChange, color = fgColor};
            }

            public static RenderInst ToggleInverse()
            {
                return new RenderInst {op = RenderOp.ToggleInverse, color = NoColor};
            }

            public static RenderInst SaveColors()
            {
                return new RenderInst {op = RenderOp.SaveColors};
            }

            public static RenderInst RestoreColors()
            {
                return new RenderInst {op = RenderOp.RestoreColors};
            }

            public override string ToString()
            {
                switch (op)
                {
                    case RenderOp.Text:
                        return "txt " + text;
                    case RenderOp.BgColorChange:
                        return "bg " + color;
                    case RenderOp.FgColorChange:
                        return "fg " + color;
                    case RenderOp.ToggleInverse:
                        return "inv";
                    case RenderOp.SaveColors:
                        return "save";
                    case RenderOp.RestoreColors:
                        return "restore";
                    case RenderOp.Flush:
                        return "flush";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public static void MaybeFlush(List<RenderInst> renderInst, ref bool flushed)
            {
                if (!flushed)
                {
                    renderInst.Add(new RenderInst {op = RenderOp.Flush, color = NoColor});
                    flushed = true;
                }
            }
        }

        private void Dump(List<RenderInst> instrs)
        {
            var fg = _console.ForegroundColor;
            var bg = _console.BackgroundColor;

            int x = _initialX;
            int y = _initialY;
            var sb = new StringBuilder();

            var savedBgColor = bg;
            var savedFgColor = fg;
            var nextBgColor = bg;
            var nextFgColor = fg;
            var savedInverse = false;

            var idxPrevInsts = 0;
            var charsOnLineCurrRender = 0;

            var matchesPrev = true;
            var inverse = false;
            for (int i = 0; i < instrs.Count; i++)
            {
                var inst = instrs[i];

                if (matchesPrev)
                {
                    if (i < _prevRenderInst.Count)
                    {
                        var previnst = _prevRenderInst[i];
                        switch (inst.op)
                        {
                            case RenderOp.FgColorChange:
                            case RenderOp.BgColorChange:
                                matchesPrev = inst.color == previnst.color;
                                break;
                            case RenderOp.Text:
                                matchesPrev = inst.text == previnst.text;
                                break;
                        }
                    }
                    else
                    {
                        matchesPrev = false;
                    }

                    if (!matchesPrev)
                    {
                        var bufWidth = _console.BufferWidth;
                        y += x / bufWidth;
                        x = x % bufWidth;
                        _console.SetCursorPosition(x, y);
                    }
                }

                switch (inst.op)
                {
                    case RenderOp.Flush:
                        FlushBuffer(sb, nextFgColor, nextBgColor);
                        break;

                    case RenderOp.Text:
                        var c = inst.text;
                        if (c == '\n')
                        {
                            idxPrevInsts = HandleEOLRendering(idxPrevInsts, charsOnLineCurrRender, sb, eob: false);
                            charsOnLineCurrRender = 0;
                            x = 0;
                            y += 1;
                        }
                        else
                        {
                            var len = _console.LengthInBufferCells(c);
                            x += len;
                            charsOnLineCurrRender += len;
                        }
                        if (!matchesPrev)
                        {
                            sb.Append(c);
                        }
                        break;

                    case RenderOp.BgColorChange:
                        nextBgColor = inverse ? (ConsoleColor)((int)inst.color ^ 7) : inst.color;
                        break;

                    case RenderOp.FgColorChange:
                        nextFgColor = inverse ? (ConsoleColor)((int)inst.color ^ 7) : inst.color;
                        break;

                    case RenderOp.RestoreColors:
                        nextBgColor = savedBgColor;
                        nextFgColor = savedFgColor;
                        inverse     = savedInverse;
                        break;

                    case RenderOp.SaveColors:
                        savedBgColor = nextBgColor;
                        savedFgColor = nextFgColor;
                        savedInverse = inverse;
                        break;

                    case RenderOp.ToggleInverse:
                        nextBgColor = (ConsoleColor)((int)nextBgColor ^ 7);
                        nextFgColor = (ConsoleColor)((int)nextFgColor ^ 7);
                        inverse     = !inverse;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Erase any artifacts from the previous render
            HandleEOLRendering(idxPrevInsts, charsOnLineCurrRender, sb, eob: true);

            if (sb.Length > 0)
            {
                if (matchesPrev)
                {
                    var bufWidth = _console.BufferWidth;
                    y += x / bufWidth;
                    x = x % bufWidth;
                    _console.SetCursorPosition(x, y);
                }
                FlushBuffer(sb, nextFgColor, nextBgColor);
            }

            PlaceCursor();

            _console.BackgroundColor = bg;
            _console.ForegroundColor = fg;
        }

        private void FlushBuffer(StringBuilder sb, ConsoleColor fgColor, ConsoleColor bgColor)
        {
            if (sb.Length > 0)
            {
                if (bgColor != Console.BackgroundColor)
                {
                    _console.BackgroundColor = bgColor;
                }
                if (fgColor != Console.ForegroundColor)
                {
                    _console.ForegroundColor = fgColor;
                }
                _console.Write(sb.ToString());
                sb.Clear();
            }
        }

        private int HandleEOLRendering(int idxPrevInsts, int charsOnLineCurrRender, StringBuilder sb, bool eob)
        {
            int charsOnLinePrevRender = 0;
            for (; idxPrevInsts < _prevRenderInst.Count; idxPrevInsts++)
            {
                var prevInst = _prevRenderInst[idxPrevInsts];
                if (prevInst.op == RenderOp.Text)
                {
                    var prevC = prevInst.text;
                    if (prevC == '\n')
                    {
                        if (!eob)
                        {
                            idxPrevInsts += 1;
                        }
                        break;
                    }
                    charsOnLinePrevRender += _console.LengthInBufferCells(prevC);
                }
            }

            if (charsOnLinePrevRender > charsOnLineCurrRender)
            {
                sb.Append(' ', charsOnLinePrevRender - charsOnLineCurrRender);
            }

            if (eob)
            {
                charsOnLinePrevRender = 0;
                for (; idxPrevInsts < _prevRenderInst.Count; idxPrevInsts++)
                {
                    var prevInst = _prevRenderInst[idxPrevInsts];
                    if (prevInst.op == RenderOp.Text)
                    {
                        var prevC = prevInst.text;
                        if (prevC == '\n')
                        {
                            sb.Append(' ', charsOnLinePrevRender);
                            sb.Append('\n');
                            charsOnLinePrevRender = 0;
                        }
                        else
                        {
                            charsOnLinePrevRender += _console.LengthInBufferCells(prevC);
                        }
                    }
                }

                if (charsOnLinePrevRender > 0)
                {
                    sb.Append(' ', charsOnLinePrevRender);
                }
            }

            return idxPrevInsts;
        }

        private void ReallyRender()
        {
            string text = null;
            ParseError[] prevParseErrors = _parseErrors;
            try
            {
                _console.StartRender();
                var tmp = _renderInst;
                _renderInst = _prevRenderInst;
                _renderInst.Clear();
                _prevRenderInst = tmp;

                text = ParseInput();

                var consoleBgColor = Console.BackgroundColor;
                var consoleFgColor = Console.ForegroundColor;

                var tokenStack = new Stack<SavedTokenState>();
                tokenStack.Push(new SavedTokenState
                {
                    Tokens = _tokens, Index = 0, BackgroundColor = consoleBgColor, ForegroundColor = consoleFgColor
                });

                var afterEmphasisBgColor = consoleBgColor;
                var afterEmphasisFgColor = consoleFgColor;
                var curBgColor = consoleBgColor;
                var curFgColor = consoleFgColor;
                var prevBgColor = curBgColor;
                var prevFgColor = curFgColor;
                var inEmphasis = false;
                var inInverse = false;
                var flushed = false;

                bool afterLastToken = false;
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
                                    curBgColor = consoleBgColor;
                                    curFgColor = consoleFgColor;
                                }
                                else
                                {
                                    state = tokenStack.Peek();
                                }
                            }

                            if (!afterLastToken)
                            {
                                curBgColor = state.BackgroundColor;
                                curFgColor = state.ForegroundColor;

                                token = state.Tokens[++state.Index];
                            }
                        }

                        if (!afterLastToken && i == token.Extent.StartOffset)
                        {
                            GetTokenColors(token, out curFgColor, out curBgColor);

                            // We might have nested tokens.
                            var stringToken = token as StringExpandableToken;
                            if (stringToken != null && stringToken.NestedTokens != null && stringToken.NestedTokens.Count > 0)
                            {
                                var tokens = new Token[stringToken.NestedTokens.Count + 1];
                                stringToken.NestedTokens.CopyTo(tokens, 0);
                                // NestedTokens doesn't have an "EOS" token, so we use
                                // the string literal token for that purpose.
                                tokens[tokens.Length - 1] = stringToken;

                                tokenStack.Push(new SavedTokenState
                                {
                                    Tokens = tokens, Index = 0, BackgroundColor = curBgColor, ForegroundColor = curFgColor
                                });

                                if (i == tokens[0].Extent.StartOffset)
                                {
                                    GetTokenColors(tokens[0], out curFgColor, out curBgColor);
                                }
                            }
                        }
                    }

                    if (i >= _emphasisStart && i < (_emphasisStart + _emphasisLength))
                    {
                        if (!inEmphasis)
                        {
                            afterEmphasisBgColor = curBgColor;
                            afterEmphasisFgColor = curFgColor;
                            inEmphasis = true;
                        }
                        curBgColor = _options.EmphasisBackgroundColor;
                        curFgColor = _options.EmphasisForegroundColor;
                    }
                    else if (inEmphasis)
                    {
                        curBgColor = afterEmphasisBgColor;
                        curFgColor = afterEmphasisFgColor;
                        inEmphasis = false;
                    }

                    if (prevBgColor != curBgColor)
                    {
                        RenderInst.MaybeFlush(_renderInst, ref flushed);
                        _renderInst.Add(RenderInst.ChangeBgColor(curBgColor));
                        prevBgColor = curBgColor;
                    }

                    if (prevFgColor != curFgColor)
                    {
                        RenderInst.MaybeFlush(_renderInst, ref flushed);
                        _renderInst.Add(RenderInst.ChangeFgColor(curFgColor));
                        prevFgColor = curFgColor;
                    }

                    if (_visualSelectionCommandCount > 0 && InRegion(i))
                    {
                        if (!inInverse)
                        {
                            RenderInst.MaybeFlush(_renderInst, ref flushed);
                            _renderInst.Add(RenderInst.ToggleInverse());
                            inInverse = true;
                        }
                    }
                    else if (inInverse)
                    {
                        RenderInst.MaybeFlush(_renderInst, ref flushed);
                        _renderInst.Add(RenderInst.ToggleInverse());
                        inInverse = false;
                    }

                    var charToRender = text[i];
                    if (charToRender == '\n')
                    {
                        bool colorChangeNeeded = _options.ContinuationPromptBackgroundColor != curBgColor ||
                                                 _options.ContinuationPromptForegroundColor != curFgColor || inInverse;
                        if (colorChangeNeeded)
                        {
                            RenderInst.MaybeFlush(_renderInst, ref flushed);
                            _renderInst.Add(RenderInst.SaveColors());

                            if (inInverse)
                                _renderInst.Add(RenderInst.ToggleInverse());
                            if (_options.ContinuationPromptBackgroundColor != curBgColor)
                                _renderInst.Add(RenderInst.ChangeBgColor(_options.ContinuationPromptBackgroundColor));
                            if (_options.ContinuationPromptForegroundColor != curFgColor)
                                _renderInst.Add(RenderInst.ChangeFgColor(_options.ContinuationPromptForegroundColor));
                        }

                        _renderInst.Add(RenderInst.Char('\n'));
                        foreach (var c in _options.ContinuationPrompt)
                        {
                            _renderInst.Add(RenderInst.Char(c));
                        }

                        if (colorChangeNeeded)
                        {
                            flushed = false;
                            RenderInst.MaybeFlush(_renderInst, ref flushed);
                            _renderInst.Add(RenderInst.RestoreColors());
                        }
                    }
                    else
                    {
                        _renderInst.Add(RenderInst.Char(charToRender));
                    }
                }

                if (_statusLinePrompt != null)
                {
                    curBgColor = _statusIsErrorMessage ? Options.ErrorBackgroundColor : _console.BackgroundColor;
                    curFgColor = _statusIsErrorMessage ? Options.ErrorForegroundColor : _console.ForegroundColor;

                    _renderInst.Add(RenderInst.Char('\n'));
                    flushed = false;
                    if (prevBgColor != curBgColor)
                    {
                        RenderInst.MaybeFlush(_renderInst, ref flushed);
                        _renderInst.Add(RenderInst.ChangeBgColor(curBgColor));
                        prevBgColor = curBgColor;
                    }

                    if (prevFgColor != curFgColor)
                    {
                        RenderInst.MaybeFlush(_renderInst, ref flushed);
                        _renderInst.Add(RenderInst.ChangeFgColor(curFgColor));
                        prevFgColor = curFgColor;
                    }

                    for (int i = 0; i < _statusLinePrompt.Length; i++)
                    {
                        _renderInst.Add(RenderInst.Char(_statusLinePrompt[i]));
                    }
                    for (int i = 0; i < _statusBuffer.Length; i++)
                    {
                        _renderInst.Add(RenderInst.Char(_statusBuffer[i]));
                    }
                }
            }
            finally
            {
                _console.EndRender();
            }

            Dump(_renderInst);

            //Render prompt
            RenderPrompt(text, _parseErrors, prevParseErrors);
            _lastRenderTime.Restart();
        }

        private void RenderPrompt(string currtxt, ParseError[] parseErrors, ParseError[] prevParseErrors)
        {
            var newFgColor = _options.DefaultTokenForegroundColor;
            var prevFgColor = Options.DefaultTokenForegroundColor;
            if(_parseErrors.Length > 0)
            {
                newFgColor = _options.ErrorForegroundColor;
            }
            if(null !=prevParseErrors && prevParseErrors.Length > 0)
            {
                prevFgColor = _options.ErrorForegroundColor;
            }
            if(newFgColor != prevFgColor)
            {
                // Render
                string prompt = _prompt;
                int i = prompt.LastIndexOf(Environment.NewLine);
                if(i > -1)
                {
                    prompt = prompt.Substring(i+ Environment.NewLine.Length);
                }

                int x = prompt.Length - 1 ;
                int y = _singleton._console.CursorTop;
                while (x >= 0)
                {

                    char c = prompt[x];
                    if (char.IsWhiteSpace(c))
                    {
                        x -= 1;
                        continue;
                    }
                    
                    int oldX = _singleton._console.CursorLeft;
                    int oldY = _singleton._console.CursorTop;
                    
                    _singleton._console.SetCursorPosition(x, y);
                    var consoleOldFGColor = _console.ForegroundColor;
                    _console.ForegroundColor = newFgColor;
                    _console.Write(c.ToString());
                    _singleton._console.SetCursorPosition(oldX, oldY);
                    _singleton._console.ForegroundColor = consoleOldFGColor;
                    break;
                }
            }
        }
        private int LengthInBufferCells(char c)
        {
            int length = Char.IsControl(c) ? 1 : 0;
            if (c < 256)
            {
                return length + 1;
            }
            return _console.LengthInBufferCells(c);
        }
        private static void WriteBlankLines(int count)
        {
            var console = _singleton._console;
            var line = new string(' ', console.BufferWidth);
            while (count-- >= 0)
            {
                console.Write(line);
            }
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

        private void ClearRenderInstList()
        {
            _renderInst.Clear();
        }
        private void PlaceCursor(int x, ref int y)
        {
            int statusLineCount = GetStatusLineCount();
            if ((y + statusLineCount) >= _console.BufferHeight)
            {
                _console.ScrollBuffer((y + statusLineCount) - _console.BufferHeight + 1);
                y = _console.BufferHeight - 1;
            }
            _console.SetCursorPosition(x, y);
        }

        private void PlaceCursor()
        {
            var coordinates = ConvertOffsetToCoordinates(_current);
            int y = coordinates.Y;
            PlaceCursor(coordinates.X, ref y);
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
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
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
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
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
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
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
            int numericArg;
            TryGetArgAsInt(arg, out numericArg, +1);
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
