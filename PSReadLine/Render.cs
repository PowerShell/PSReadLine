/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;

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

    internal class RenderedLineData
    {
        public readonly string Line;
        private readonly bool _isFirstLogicalLine;

        private int _physicalLineCount, _lengthOfLastPhsicalLine;
        private int _bufferWidth, _initialX;

        public RenderedLineData(string line, bool isFirstLogicalLine)
        {
            Line = line;
            _isFirstLogicalLine = isFirstLogicalLine;
        }

        public int PhysicalLineCount(int bufferWidth, int initialX, out int lenLastPhysicalLine)
        {
            bool useCachedValues = bufferWidth == _bufferWidth && (!_isFirstLogicalLine || initialX == _initialX);
            if (useCachedValues)
            {
                lenLastPhysicalLine = _lengthOfLastPhsicalLine;
                return _physicalLineCount;
            }

            _bufferWidth = bufferWidth;
            _initialX = initialX;

            // The first logical line has the user prompt.
            int x = _isFirstLogicalLine ? initialX : 0;
            int y = 1;
            lenLastPhysicalLine = 0;

            for (int i = 0; i < Line.Length; i++)
            {
                var c = Line[i];

                // Simple escape sequence skipping.
                if (c == 0x1b && (i + 1) < Line.Length && Line[i + 1] == '[')
                {
                    i += 2;
                    while (i < Line.Length && Line[i] != 'm')
                    {
                        i++;
                    }

                    continue;
                }

                int size = PSConsoleReadLine.LengthInBufferCells(c);
                if (x == 0 && lenLastPhysicalLine > 0)
                {
                    y++;
                    lenLastPhysicalLine = 0;
                }

                x += size;
                lenLastPhysicalLine += size;

                if (x == bufferWidth)
                {
                    x = 0;
                }
                else if (x > bufferWidth)
                {
                    // It could wrap to the next line in case of a multi-cell character.
                    // If character didn't fit on current line, it will move entirely to the next line.
                    x = size;
                    y++;
                    lenLastPhysicalLine = size;
                }
            }

            _lengthOfLastPhsicalLine = lenLastPhysicalLine;
            _physicalLineCount = y;

            return y;
        }
    }

    internal class RenderData
    {
        public int bufferWidth;
        public int bufferHeight;
        public int cursorLeft;
        public int cursorTop;
        public int initialY;
        public bool errorPrompt;
        public RenderedLineData[] lines;

        public void UpdateConsoleInfo(IConsole console)
        {
            bufferWidth = console.BufferWidth;
            bufferHeight = console.BufferHeight;
            cursorLeft = console.CursorLeft;
            cursorTop = console.CursorTop;
        }
    }

    internal readonly struct RenderDataOffset
    {
        public RenderDataOffset(int logicalLineIndex, int visibleCharIndex)
        {
            LogicalLineIndex = logicalLineIndex;
            VisibleCharIndex = visibleCharIndex;
        }

        public readonly int LogicalLineIndex;
        public readonly int VisibleCharIndex;
    }

    public partial class PSConsoleReadLine
    {
        struct LineInfoForRendering
        {
            public int CurrentLogicalLineIndex;
            public int CurrentPhysicalLineCount;
            public int PreviousLogicalLineIndex;
            public int PreviousPhysicalLineCount;
            public int PseudoPhysicalLineOffset;
        }

        private const int COMMON_WIDEST_CONSOLE_WIDTH = 160;
        private readonly List<StringBuilder> _consoleBufferLines = new(1) {new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH)};
        private static readonly string[] _spaces = new string[80];
        private RenderData _previousRender;
        private static readonly RenderData _initialPrevRender = new()
        {
            lines = new[] { new RenderedLineData(line: "", isFirstLogicalLine: true) },
            errorPrompt = false
        };
        private int _initialX;
        private int _initialY;
        private bool _waitingToRender;
        private bool _handlePotentialResizing;

        private ConsoleColor _initialForeground;
        private ConsoleColor _initialBackground;
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
                RenderWithPredictionQueryPaused();
            }
        }

        private void RenderWithPredictionQueryPaused()
        {
            // Sometimes we need to re-render the buffer to show status line, or to clear
            // the visual selection, or to clear the visual emphasis.
            // In those cases, the buffer text is unchanged, and thus we can skip querying
            // for prediction during the rendering, but instead, use the existing results.
            using var _ = _prediction.PauseQuery();
            Render();
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
                _waitingToRender = true;
                return;
            }

            ForceRender();
        }

        private void ForceRender()
        {
            var defaultColor = VTColorUtils.DefaultColor;

            // Geneate a sequence of logical lines with escape sequences for coloring.
            int logicalLineCount = GenerateRender(defaultColor);

            // Now write that out (and remember what we did so we can clear previous renders
            // and minimize writing more than necessary on the next render.)

            var renderLines = new RenderedLineData[logicalLineCount];
            var renderData = new RenderData {lines = renderLines};
            for (var i = 0; i < logicalLineCount; i++)
            {
                var line = _consoleBufferLines[i].ToString();
                renderLines[i] = new RenderedLineData(line, isFirstLogicalLine: i == 0);
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
            _prediction.QueryForSuggestion(text);

            string color = defaultColor;
            string activeColor = string.Empty;
            bool afterLastToken = false;
            int currentLogicalLine = 0;
            bool inSelectedRegion = false;

            void UpdateColorsIfNecessary(string newColor)
            {
                if (!object.ReferenceEquals(newColor, activeColor))
                {
                    if (!inSelectedRegion)
                    {
                        _consoleBufferLines[currentLogicalLine]
                            .Append(VTColorUtils.AnsiReset)
                            .Append(newColor);
                    }
                    activeColor = newColor;
                }
            }

            void RenderOneChar(char charToRender, bool toEmphasize)
            {
                if (charToRender == '\n')
                {
                    if (inSelectedRegion)
                    {
                        // Turn off inverse before end of line, turn on after continuation prompt
                        _consoleBufferLines[currentLogicalLine].Append(VTColorUtils.AnsiReset);
                    }

                    currentLogicalLine += 1;
                    if (currentLogicalLine == _consoleBufferLines.Count)
                    {
                        _consoleBufferLines.Add(new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH));
                    }

                    // Reset the color for continuation prompt so the color sequence will always be explicitly
                    // specified for continuation prompt in the generated render strings.
                    // This is necessary because we will likely not rewrite all texts during rendering, and thus
                    // we cannot assume the continuation prompt can continue to use the active color setting from
                    // the previous rendering string.
                    activeColor = string.Empty;

                    if (Options.ContinuationPrompt.Length > 0)
                    {
                        UpdateColorsIfNecessary(Options._continuationPromptColor);
                        _consoleBufferLines[currentLogicalLine].Append(Options.ContinuationPrompt);
                    }

                    if (inSelectedRegion)
                    {
                        // Turn off inverse before end of line, turn on after continuation prompt
                        _consoleBufferLines[currentLogicalLine].Append(Options.SelectionColor);
                    }

                    return;
                }

                UpdateColorsIfNecessary(toEmphasize ? _options._emphasisColor : color);

                if (char.IsControl(charToRender))
                {
                    _consoleBufferLines[currentLogicalLine].Append('^');
                    _consoleBufferLines[currentLogicalLine].Append((char)('@' + charToRender));
                }
                else
                {
                    _consoleBufferLines[currentLogicalLine].Append(charToRender);
                }
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

            int selectionStart = -1;
            int selectionEnd = -1;
            if (_visualSelectionCommandCount > 0)
            {
                GetRegion(out int regionStart, out int regionLength);
                if (regionLength > 0)
                {
                    selectionStart = regionStart;
                    selectionEnd = selectionStart + regionLength;
                }
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (i == selectionStart)
                {
                    _consoleBufferLines[currentLogicalLine].Append(Options.SelectionColor);
                    inSelectedRegion = true;
                }
                else if (i == selectionEnd)
                {
                    _consoleBufferLines[currentLogicalLine].Append(VTColorUtils.AnsiReset);
                    _consoleBufferLines[currentLogicalLine].Append(activeColor);
                    inSelectedRegion = false;
                }

                if (!afterLastToken)
                {
                    // Figure out the color of the character - if it's in a token,
                    // use the tokens color otherwise use the initial color.
                    var state = tokenStack.Peek();
                    var token = state.Tokens[state.Index];
                    while (i == token.Extent.EndOffset)
                    {
                        if (state.Index == state.Tokens.Length - 1)
                        {
                            tokenStack.Pop();
                            if (tokenStack.Count == 0)
                            {
                                afterLastToken = true;
                                token = null;
                                color = defaultColor;
                                break;
                            }
                            else
                            {
                                state = tokenStack.Peek();

                                // It's possible that a 'StringExpandableToken' is the last available token, for example:
                                //   'begin $a\abc def', 'process $a\abc | blah' and 'end $a\abc; hello'
                                // due to the special handling of the keywords 'begin', 'process' and 'end', all the above 3 script inputs
                                // generate only 2 tokens by the parser -- A KeywordToken, and a StringExpandableToken '$a\abc'. Text after
                                // '$a\abc' is not tokenized at all.
                                // We repeat the test to see if we fall into this case ('token' is the final one in the stack).
                                continue;
                            }
                        }

                        color = state.Color;
                        token = state.Tokens[++state.Index];
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
                var toEmphasize = i >= _emphasisStart && i < (_emphasisStart + _emphasisLength);

                RenderOneChar(charToRender, toEmphasize);
            }

            if (inSelectedRegion)
            {
                _consoleBufferLines[currentLogicalLine].Append(VTColorUtils.AnsiReset);
                inSelectedRegion = false;
            }

            _prediction.ActiveView.RenderSuggestion(_consoleBufferLines, ref currentLogicalLine);
            activeColor = string.Empty;

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

        /// <summary>
        /// Flip the color on the prompt if the error state changed.
        /// </summary>
        /// <returns>
        /// A bool value indicating whether we need to flip the color,
        /// namely whether we moved cursor to the initial position.
        /// </returns>
        private bool RenderErrorPrompt(RenderData renderData, string defaultColor)
        {
            if (_initialY < 0
                || _options.PromptText == null
                || _options.PromptText.Length == 0
                || String.IsNullOrEmpty(_options.PromptText[0]))
            {
                // No need to flip the prompt color if either the error prompt is not defined
                // or the initial cursor point has already been scrolled off the buffer.
                return false;
            }

            // We may need to flip the color on the prompt if the error state changed.

            renderData.errorPrompt = (_parseErrors != null && _parseErrors.Length > 0);
            if (renderData.errorPrompt == _previousRender.errorPrompt)
            {
                // No need to flip the prompt color if the error state didn't change.
                return false;
            }

            // We need to update the prompt
            _console.SetCursorPosition(_initialX, _initialY);

            string promptText =
                (renderData.errorPrompt && _options.PromptText.Length == 2)
                    ? _options.PromptText[1]
                    : _options.PromptText[0];

            // promptBufferCells is the number of visible characters in the prompt
            int promptBufferCells = LengthInBufferCells(promptText);
            bool renderErrorPrompt = false;
            int bufferWidth = _console.BufferWidth;

            if (_console.CursorLeft >= promptBufferCells)
            {
                renderErrorPrompt = true;
                _console.CursorLeft -= promptBufferCells;
            }
            else
            {
                // The 'CursorLeft' could be less than error-prompt-cell-length in one of the following 3 cases:
                //   1. console buffer was resized, which causes the initial cursor to appear on the next line;
                //   2. prompt string gets longer (e.g. by 'cd' into nested folders), which causes the line to be wrapped to the next line;
                //   3. the prompt function was changed, which causes the new prompt string is shorter than the error prompt.
                // Here, we always assume it's the case 1 or 2, and wrap back to the previous line to change the error prompt color.
                // In case of case 3, the rendering would be off, but it's more of a user error because the prompt is changed without
                // updating 'PromptText' with 'Set-PSReadLineOption'.

                int diffs = promptBufferCells - _console.CursorLeft;
                int newX = bufferWidth - diffs % bufferWidth;
                int newY = _initialY - diffs / bufferWidth - 1;

                // newY could be less than 0 if 'PromptText' is manually set to be a long string.
                if (newY >= 0)
                {
                    renderErrorPrompt = true;
                    _console.SetCursorPosition(newX, newY);
                }
            }

            if (renderErrorPrompt)
            {
                if (!promptText.Contains('\x1b'))
                {
                    string color = renderData.errorPrompt ? _options._errorColor : defaultColor;
                    _console.Write(color);
                    _console.Write(promptText);
                    _console.Write(VTColorUtils.AnsiReset);
                }
                else
                {
                    _console.Write(promptText);
                }
            }

            return true;
        }

        /// <summary>
        /// We avoid re-rendering everything while editing if it's possible.
        /// This method attempts to find the first changed logical line and move the cursor to the right position for the subsequent rendering.
        /// </summary>
        private void CalculateWhereAndWhatToRender(bool cursorMovedToInitialPos, RenderData renderData, out LineInfoForRendering lineInfoForRendering)
        {
            int bufferWidth = _console.BufferWidth;
            int bufferHeight = _console.BufferHeight;

            RenderedLineData[] previousRenderLines = _previousRender.lines;
            int previousLogicalLine = 0;
            int previousPhysicalLine = 0;

            RenderedLineData[] renderLines = renderData.lines;
            int logicalLine = 0;
            int physicalLine = 0;
            int pseudoPhysicalLineOffset = 0;

            bool hasToWriteAll = true;

            if (renderLines.Length > 1)
            {
                // There are multiple logical lines, so it's possible the first N logical lines are not affected by the user's editing,
                // in which case, we can skip rendering until reaching the first changed logical line.

                int minLinesLength = previousRenderLines.Length;
                int linesToCheck = -1;

                if (renderLines.Length < previousRenderLines.Length)
                {
                    minLinesLength = renderLines.Length;

                    // When the initial cursor position has been scrolled off the buffer, it's possible the editing deletes some texts and
                    // potentially causes the final cursor position to be off the buffer as well. In this case, we should start rendering
                    // from the logical line where the cursor is supposed to be moved to eventually.
                    // Here we check for this situation, and calculate the physical line count to check later if we are in this situation.

                    if (_initialY < 0)
                    {
                        int y = ConvertOffsetToPoint(_current).Y;
                        if (y < 0)
                        {
                            // Number of physical lines from the initial row to the row where the cursor is supposed to be set at.
                            linesToCheck = y - _initialY + 1;
                        }
                    }
                }

                // Find the first logical line that was changed.
                for (; logicalLine < minLinesLength; logicalLine++)
                {
                    // Found the first different logical line? Break out the loop.
                    if (renderLines[logicalLine].Line != previousRenderLines[logicalLine].Line) { break; }

                    int count = renderLines[logicalLine].PhysicalLineCount(bufferWidth, _initialX, out _);
                    physicalLine += count;

                    if (linesToCheck < 0)
                    {
                        continue;
                    }
                    else if (physicalLine >= linesToCheck)
                    {
                        physicalLine -= count;
                        break;
                    }
                }

                if (logicalLine > 0)
                {
                    // Some logical lines at the top were not affected by the editing.
                    // We only need to write starting from the first changed logical line.
                    hasToWriteAll = false;
                    previousLogicalLine = logicalLine;
                    previousPhysicalLine = physicalLine;

                    var newTop = _initialY + physicalLine;
                    if (newTop == bufferHeight)
                    {
                        if (logicalLine < renderLines.Length)
                        {
                            // This could happen when adding a new line in the end of the very last line.
                            // In this case, we scroll up by writing out a new line.
                            _console.SetCursorPosition(left: bufferWidth - 1, top: bufferHeight - 1);
                            _console.Write("\n");
                        }

                        // It might happen that 'logicalLine == renderLines.Length'. This means the current
                        // logical lines to be rendered are exactly the same the the previous logical lines.
                        // No need to do anything in this case, as we don't need to render anything.
                    }
                    else
                    {
                        // For the logical line that we will start to re-render from, it's possible that
                        //   1. the whole logical line had already been scrolled up-off the buffer. This could happen when you backward delete characters
                        //      on the first line in buffer and cause the current line to be folded to the previous line.
                        //   2. the logical line spans on multiple physical lines and the top a few physical lines had already been scrolled off the buffer.
                        //      This could happen when you edit on the top a few physical lines in the buffer, which belong to a longer logical line.
                        // Either of them will cause 'newTop' to be less than 0.
                        if (newTop < 0)
                        {
                            // In this case, we will render the whole logical line starting from the upper-left-most point of the window.
                            // By doing this, we are essentially adding a few pseudo physical lines (the physical lines that belong to the logical line but
                            // had been scrolled off the buffer would be re-rendered). So, update 'physicalLine'.
                            pseudoPhysicalLineOffset = 0 - newTop;
                            physicalLine += pseudoPhysicalLineOffset;
                            newTop = 0;
                        }

                        _console.SetCursorPosition(left: 0, top: newTop);
                    }
                }
            }

            if (hasToWriteAll && !cursorMovedToInitialPos)
            {
                // The editing was in the first logical line. We have to write everything in this case.
                // Move the cursor to the initial position if we haven't done so.
                if (_initialY < 0)
                {
                    // The prompt had been scrolled up-off the buffer. Now we are about to render from the very
                    // beginning, so we clear the screen and invoke/print the prompt line.
                    _console.Write("\x1b[2J");
                    _console.SetCursorPosition(0, _console.WindowTop);

                    string newPrompt = GetPrompt();
                    if (!string.IsNullOrEmpty(newPrompt))
                    {
                        _console.Write(newPrompt);
                    }

                    _initialX = _console.CursorLeft;
                    _initialY = _console.CursorTop;
                    _previousRender = _initialPrevRender;
                }
                else
                {
                    _console.SetCursorPosition(_initialX, _initialY);
                }
            }

            lineInfoForRendering = default;
            lineInfoForRendering.CurrentLogicalLineIndex = logicalLine;
            lineInfoForRendering.CurrentPhysicalLineCount = physicalLine;
            lineInfoForRendering.PreviousLogicalLineIndex = previousLogicalLine;
            lineInfoForRendering.PreviousPhysicalLineCount = previousPhysicalLine;
            lineInfoForRendering.PseudoPhysicalLineOffset = pseudoPhysicalLineOffset;
        }

        private void ReallyRender(RenderData renderData, string defaultColor)
        {
            string activeColor = "";
            int bufferWidth = _console.BufferWidth;
            int bufferHeight = _console.BufferHeight;

            void UpdateColorsIfNecessary(string newColor)
            {
                if (!object.ReferenceEquals(newColor, activeColor))
                {
                    _console.Write(newColor);
                    activeColor = newColor;
                }
            }

            // In case the buffer was resized
            RecomputeInitialCoords(isTextBufferUnchanged: false);

            // Make cursor invisible while we're rendering.
            _console.CursorVisible = false;

            // Change the prompt color if the parsing error state changed.
            bool cursorMovedToInitialPos = RenderErrorPrompt(renderData, defaultColor);

            // Calculate what to render and where to start the rendering.
            CalculateWhereAndWhatToRender(cursorMovedToInitialPos, renderData, out LineInfoForRendering lineInfoForRendering);

            RenderedLineData[] previousRenderLines = _previousRender.lines;
            int previousLogicalLine = lineInfoForRendering.PreviousLogicalLineIndex;
            int previousPhysicalLine = lineInfoForRendering.PreviousPhysicalLineCount;

            RenderedLineData[] renderLines = renderData.lines;
            int logicalLine = lineInfoForRendering.CurrentLogicalLineIndex;
            int physicalLine = lineInfoForRendering.CurrentPhysicalLineCount;
            int pseudoPhysicalLineOffset = lineInfoForRendering.PseudoPhysicalLineOffset;

            int lenPrevLastLine = 0;
            int logicalLineStartIndex = logicalLine;
            int physicalLineStartCount = physicalLine;

            for (; logicalLine < renderLines.Length; logicalLine++)
            {
                if (logicalLine != logicalLineStartIndex) _console.Write("\n");

                var lineData = renderLines[logicalLine];
                _console.Write(lineData.Line);

                physicalLine += lineData.PhysicalLineCount(bufferWidth, _initialX, out int lenLastLine);

                // Find the previous logical line (if any) that would have rendered
                // the current physical line because we may need to clear it.
                // We don't clear it unconditionally to allow things like a prompt
                // on the right side of the line.

                while (physicalLine > previousPhysicalLine
                    && previousLogicalLine < previousRenderLines.Length)
                {
                    previousPhysicalLine += previousRenderLines[previousLogicalLine].PhysicalLineCount(bufferWidth, _initialX, out lenPrevLastLine);
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
                    if (lenLastLine < bufferWidth)
                    {
                        lenToClear = bufferWidth - lenLastLine;
                        if (physicalLine == 1)
                            lenToClear -= _initialX;
                    }
                }

                if (lenToClear > 0)
                {
                    UpdateColorsIfNecessary(defaultColor);
                    _console.Write(Spaces(lenToClear));
                }
            }

            UpdateColorsIfNecessary(defaultColor);

            // The last logical line is shorter than our previous render? Clear them.
            for (int currentLines = physicalLine; currentLines < previousPhysicalLine;)
            {
                _console.SetCursorPosition(0, _initialY + currentLines);

                currentLines++;
                if (currentLines == previousPhysicalLine)
                {
                    _console.Write(Spaces(lenPrevLastLine));
                }
                else
                {
                    _console.BlankRestOfLine();
                }
            }

            // Fewer logical lines than our previous render? Clear them.
            for (int line = previousLogicalLine; line < previousRenderLines.Length; line++)
            {
                if (line > previousLogicalLine || logicalLineStartIndex < renderLines.Length)
                {
                    // For the first of the remaining previous logical lines, if we didn't actually
                    // render anything for the current logical lines, then the cursor is already at
                    // the beginning of the right physical line that should be cleared, and thus no
                    // need to write a new line in such case.
                    // In other cases, we need to write a new line to get the cursor to the correct
                    // physical line.

                    _console.Write("\n");
                }

                // No need to write new line if all we need is to clear the extra previous render.
                int lineCount = previousRenderLines[line].PhysicalLineCount(bufferWidth, _initialX, out _);
                WriteBlankLines(lineCount);
            }

            // Preserve the current render data.
            _previousRender = renderData;

            // If we counted pseudo physical lines, deduct them to get the real physical line counts
            // before updating '_initialY'.
            physicalLine -= pseudoPhysicalLineOffset;

            // Reset the colors after we've finished all our rendering.
            _console.Write(VTColorUtils.AnsiReset);

            if (_initialY + physicalLine > bufferHeight)
            {
                // We had to scroll to render everything, update _initialY
                _initialY = bufferHeight - physicalLine;
            }
            else if (pseudoPhysicalLineOffset > 0)
            {
                // When we rewrote a logical line (or part of a logical line) that had previously been scrolled up-off
                // the buffer (fully or partially), we need to adjust '_initialY' if the changes to that logical line
                // don't result in the same number of physical lines to be scrolled up-off the buffer.

                // Calculate the total number of physical lines starting from the logical line we re-wrote.
                int physicalLinesStartingFromTheRewrittenLogicalLine =
                    physicalLine - (physicalLineStartCount - pseudoPhysicalLineOffset);

                Debug.Assert(
                    bufferHeight + pseudoPhysicalLineOffset >= physicalLinesStartingFromTheRewrittenLogicalLine,
                    "number of physical lines starting from the first changed logical line should be no more than the buffer height plus the pseudo lines we added.");

                int offset = physicalLinesStartingFromTheRewrittenLogicalLine > bufferHeight
                    ? pseudoPhysicalLineOffset - (physicalLinesStartingFromTheRewrittenLogicalLine - bufferHeight)
                    : pseudoPhysicalLineOffset;

                _initialY += offset;
            }

            // Calculate the coord to place the cursor for the next input.
            var point = ConvertOffsetToPoint(_current);

            if (point.Y == bufferHeight)
            {
                // The cursor top exceeds the buffer height, so we need to
                // scroll up the buffer by 1 line.
                _console.Write("\n");

                // Adjust the initial cursor position and the to-be-set cursor position
                // after scrolling up the buffer.
                _initialY -= 1;
                point.Y -= 1;
            }
            else if (point.Y < 0)
            {
                // This could happen in at least 3 cases:
                //
                //   1. when you are adding characters to the first line in the buffer (top = 0) to make the logical line
                //      wrap to one extra physical line. This would cause the buffer to scroll up and push the line being
                //      edited up-off the buffer.
                //   2. when you are deleting characters (Backspace) from the first line in the buffer without changing the
                //      number of physical lines (either editing the same logical line or causing the current logical line
                //      to merge in the previous but still span to the current physical line). The cursor is supposed to
                //      appear in the previous line (which is off the buffer).
                //   3. Both 'bck-i-search' and 'fwd-i-search' may find a history command with multi-line text, and the
                //      matching string in the text, where the cursor is supposed to be moved to, will be scrolled up-off
                //      the buffer after rendering.
                //
                // In these case, we move the cursor to the left-most position of the first line, where it's closest to
                // the real position it should be in the ideal world.

                // First update '_current' to the index of the first character that appears on the line 0,
                // then we call 'ConvertOffsetToPoint' again to get the right cursor position to use.
                point.X = point.Y = 0;
                _current = ConvertLineAndColumnToOffset(point);
                point = ConvertOffsetToPoint(_current);
            }

            _console.SetCursorPosition(point.X, point.Y);
            _console.CursorVisible = true;

            _previousRender.UpdateConsoleInfo(_console);
            _previousRender.initialY = _initialY;

            // TODO: set WindowTop if necessary

            _lastRenderTime.Restart();
            _waitingToRender = false;
        }

        private string GetTokenColor(Token token)
        {
            if ((token.TokenFlags & TokenFlags.CommandName) != 0)
            {
                return _options._commandColor;
            }

            switch (token.Kind)
            {
            case TokenKind.Comment:
                return _options._commentColor;

            case TokenKind.Parameter:
            case TokenKind.Generic when token is StringLiteralToken slt && slt.Text.StartsWith("--"):
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

            if ((token.TokenFlags & TokenFlags.Keyword) != 0)
            {
                return _options._keywordColor;
            }

            if (token.Kind != TokenKind.Generic && (token.TokenFlags & (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator | TokenFlags.AssignmentOperator)) != 0)
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

        private void RecomputeInitialCoords(bool isTextBufferUnchanged)
        {
            if (!_handlePotentialResizing)
            {
                return;
            }

            // We attempt to handle window resizing only once per a keybinding processing, because we assume the
            // window resizing cannot and shouldn't happen within the processing of a given keybinding.
            // This is, in particular, to avoid unneeded checks while we are in the 'MenuComplete' or a similar
            // function that handles some keystroke inputs directly within the function, does rendering multiple
            // times, and changes cursor position directly by '_console.SetCursorPosition'.
            // For 'MenuComplete', we will not attempt to handle resizing while the menu is displayed, because
            // that's simply a wrong thing to do.
            _handlePotentialResizing = false;

            // Operations like menu completion and inline dynamic help may cause the screen buffer to scroll up,
            // and '_initialY' would have been adjusted accordingly.
            // In that case, we need to adjust the old cursor position accordingly too.
            int preInitialY = _previousRender.initialY;
            if (preInitialY != _initialY)
            {
                _previousRender.cursorTop -= preInitialY - _initialY;
                _previousRender.initialY = _initialY;
            }

            if (_previousRender.bufferWidth == _console.BufferWidth &&
                _previousRender.bufferHeight == _console.BufferHeight)
            {
                int left = _console.CursorLeft;
                int top = _console.CursorTop;

                int preLeft = _previousRender.cursorLeft;
                int preTop = _previousRender.cursorTop;

                if (preLeft == left && preTop > top)
                {
                    // Try to handle a special scenario: the max-size terminal windows gets restored to
                    // the normal size, and then is immediately changed to max size again.
                    _initialY -= preTop - top;
                }
                else if (left == 0 && top == 0 && preLeft != 0 && preTop != 0)
                {
                    // Try to handle a special scenario: a Terminal User Interface (TUI) utility is used
                    // with a custom key-binding to get rich editing experience, for example:
                    //   Set-PSReadlineKeyHandler -Chord "Shift+Tab" -ScriptBlock {
                    //       $s = fzf.exe
                    //       [Microsoft.PowerShell.PSConsoleReadLine]::Insert($s)
                    //   }
                    // The TUI utility will likely erase the screen buffer, so we try writing out prompt
                    // and start afresh in this case.
                    string newPrompt = GetPrompt();
                    if (!string.IsNullOrEmpty(newPrompt))
                    {
                        _console.Write(newPrompt);
                    }

                    _initialX = _console.CursorLeft;
                    _initialY = _console.CursorTop;
                    _previousRender = _initialPrevRender;
                }

                return;
            }

            // If the console buffer width or height changed, our initial coordinates may have as well.
            if (isTextBufferUnchanged)
            {
                // The '_buffer' and '_current' still reflects what has been rendered on the screen,
                // so we can use them to re-calculate the initial coordinates in this case.

                // Recompute X from the buffer width:
                _initialX %= _console.BufferWidth;

                // Recompute Y from the cursor
                _initialY = 0;
                // Calculate the new cursor position when assuming '_initialY' is at line 0.
                var pt = ConvertOffsetToPoint(_current);
                // Update '_initialY' based on the difference from the actual current cursor position after the resize.
                _initialY = _console.CursorTop - pt.Y;
            }
            else
            {
                // The '_buffer' and '_current' have changed since the last rendering, so we cannot rely on them
                // for the re-calculation. A typical example would be the user clears the input with `Escape` after
                // a resize. That will cause the '_buffer' to be empty and '_current' to be 0 when we reach here.
                //
                // Instead, we will use the saved previous cursor position to re-calculate the initial coordinates,
                // based on the previous rendering data.
                // First, calculate the offset in the previous rendering data based on the old initial coordinates,
                // old buffer width, and the old cursor position.
                RenderDataOffset offset = ConvertPointToRenderDataOffset(_initialX, _initialY, _previousRender);
                if (offset.LogicalLineIndex == -1)
                {
                    // This should never happen unless it's a bug in 'ConvertPointToRenderDataOffset'.
                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        PSReadLineResources.FailedToConvertPointToRenderDataOffset,
                        _initialX,
                        _initialY,
                        _previousRender.bufferWidth,
                        _previousRender.bufferHeight,
                        _previousRender.cursorLeft,
                        _previousRender.cursorTop);
                    throw new InvalidOperationException(message);
                }

                // Recompute X from the buffer width:
                _initialX %= _console.BufferWidth;

                // Recompute Y from the cursor
                _initialY = 0;
                // Now, use the new initial coordinates, new buffer width, and the rendering data offset to calculate
                // the new cursor position when assuming '_initialY' is at line 0.
                Point pt = ConvertRenderDataOffsetToPoint(_initialX, _initialY, _console.BufferWidth, _previousRender, offset);
                // Update '_initialY' based on the difference from the actual current cursor position after the resize.
                // This is based on the assumption that the cursor is still pointing to the same character after resizing,
                // or at least pointing to the physical line where the same character is located after resizing.
                // However, that assumption is not always guaranteed in Windows Terminal, see the issue:
                //    https://github.com/microsoft/terminal/issues/10848, and
                //    https://github.com/microsoft/terminal/issues/10868
                _initialY = _console.CursorTop - pt.Y;
            }
        }

        private void MoveCursor(int newCursor)
        {
            // Only update screen cursor if the buffer is fully rendered.
            if (!_waitingToRender)
            {
                // In case the buffer was resized
                RecomputeInitialCoords(isTextBufferUnchanged: true);

                var point = ConvertOffsetToPoint(newCursor);
                if (point.Y < 0)
                {
                    Ding();
                    return;
                }

                if (point.Y == _console.BufferHeight)
                {
                    // The cursor top exceeds the buffer height. This may happen when moving cursor to the end of line,
                    // while the end of line is actually the end of buffer. In this case, we adjust the initial cursor
                    // position and the to-be-set cursor position for scrolling up the buffer.
                    _initialY -= 1;
                    point.Y -= 1;

                    // Insure the cursor is on the last line of the buffer prior
                    // to issuing a newline to scroll the buffer.
                    _console.SetCursorPosition(point.X, point.Y);

                    // Scroll up the buffer by 1 line.
                    _console.Write("\n");
                }
                else
                {
                    _console.SetCursorPosition(point.X, point.Y);
                }

                _previousRender.UpdateConsoleInfo(_console);
                _previousRender.initialY = _initialY;
            }

            // While waiting to render, and a keybinding has occurred that is moving the cursor,
            // converting offset to point could potentially result in an invalid screen position,
            // but the insertion point should reflect the move.
            _current = newCursor;
        }

        internal Point ConvertOffsetToPoint(int offset)
        {
            int x = _initialX;
            int y = _initialY;

            int bufferWidth = _console.BufferWidth;
            var continuationPromptLength = LengthInBufferCells(Options.ContinuationPrompt);

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
                        // If character didn't fit on current line, it will move entirely to the next line.
                        x = ((x == bufferWidth) ? 0 : size);

                        // If cursor is at column 0 and the next character is newline, let the next loop
                        // iteration increment y.
                        if (x != 0 || !(i + 1 < offset && _buffer[i + 1] == '\n'))
                        {
                            y += 1;
                        }
                    }
                }
            }

            // If next character actually exists, and isn't newline, check if wider than the space left on the current line.
            if (_buffer.Length > offset && _buffer[offset] != '\n')
            {
                int size = LengthInBufferCells(_buffer[offset]);
                if (x + size > bufferWidth)
                {
                    // Character was wider than remaining space, so character, and cursor, appear on next line.
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
            var continuationPromptLength = LengthInBufferCells(Options.ContinuationPrompt);
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
                        // If character didn't fit on current line, it will move entirely to the next line.
                        x = ((x == bufferWidth) ? 0 : size);

                        // If cursor is at column 0 and the next character is newline, let the next loop
                        // iteration increment y.
                        if (x != 0 || !(offset + 1 < _buffer.Length && _buffer[offset + 1] == '\n'))
                        {
                            y += 1;
                        }
                    }
                }
            }

            // Return -1 if y is out of range, otherwise the last line was shorter
            // than we wanted, but still in range so just return the last offset.
            return (point.Y == y) ? offset : -1;
        }

        internal Point ConvertRenderDataOffsetToPoint(int initialX, int initialY, int bufferWidth, RenderData renderData, RenderDataOffset offset)
        {
            if (offset.LogicalLineIndex == 0 && offset.VisibleCharIndex == -1)
            {
                // (0, -1) means the cursor should be right at the initial coordinate.
                return new Point { X = initialX, Y = initialY };
            }

            int x = initialX;
            int y = initialY;

            int lengthOfLastPhysicalLine = -1;
            int limit = offset.LogicalLineIndex;
            if (offset.VisibleCharIndex == int.MaxValue)
            {
                limit++;
            }

            // Make sure 'x' and 'y' are pointing to the end of the last processed logical line after this loop ends.
            for (int i = 0; i < limit; i++)
            {
                if (i > 0)
                {
                    // Move 'x' and 'y' to the start position where the next logical line would be rendered from.
                    // If 'x == 0 && lengthOfLastPhysicalLine == 0', it's a special case we need to handle:
                    //  * the last logical line starts from the beginning of a physical line (x == 0), AND
                    //  * the last logical line contains no visible character.
                    if (x > 0 || lengthOfLastPhysicalLine == 0)
                    {
                        x = 0;
                        y++;
                    }
                }

                RenderedLineData lineData = renderData.lines[i];
                int physicalLineCount = lineData.PhysicalLineCount(bufferWidth, initialX, out lengthOfLastPhysicalLine);
                y += physicalLineCount - 1;

                if (y == initialY)
                {
                    x += lengthOfLastPhysicalLine;
                }
                else
                {
                    x = lengthOfLastPhysicalLine;
                }

                if (x == bufferWidth)
                {
                    // In the case that the length of last physical line takes the whole buffer width,
                    // the cursor would be pushed to the start of the next line.
                    x = 0;
                    y++;
                }
            }

            if (offset.VisibleCharIndex == int.MaxValue)
            {
                // The cursor is right at the end of the logical line.
                return new Point { X = x, Y = y };
            }

            if (limit > 0)
            {
                // The logical line we are going to scan character by character is not the first logical line,
                // so we need to move 'x' and 'y' to the start of the next physical line.
                if (x > 0 || lengthOfLastPhysicalLine == 0)
                {
                    x = 0;
                    y++;
                }
            }

            int visibleCharIndex = -1, size = 0;
            string line = renderData.lines[limit].Line;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                // Simple escape sequence skipping.
                if (c == 0x1b && (i + 1) < line.Length && line[i + 1] == '[')
                {
                    i += 2;
                    while (i < line.Length && line[i] != 'm')
                    {
                        i++;
                    }

                    continue;
                }

                visibleCharIndex++;
                size = LengthInBufferCells(c);

                if (visibleCharIndex == offset.VisibleCharIndex)
                {
                    break;
                }

                x += size;
                if (x == bufferWidth)
                {
                    x = 0;
                    y++;
                }
                else if (x > bufferWidth)
                {
                    // It could wrap to the next line in case of a multi-cell character.
                    // If character didn't fit on current line, it will move entirely to the next line.
                    x = size;
                    y++;
                }
            }

            // If the offset is pointing to a double-cell character that happens to be wrapped to the next physical line,
            // then we move 'x' and 'y' to the start of the next physical line, so the new cursor continues to point to
            // that specific character.
            if (x + size > bufferWidth)
            {
                x = 0;
                y++;
            }

            return new Point { X = x, Y = y };
        }

        internal RenderDataOffset ConvertPointToRenderDataOffset(int initialX, int initialY, RenderData renderData)
        {
            int x = initialX;
            int y = initialY;
            var point = new Point { X = renderData.cursorLeft, Y = renderData.cursorTop };

            if (point.Y == y && point.X == x)
            {
                // The given cursor is the same as the initial coordinate, return (0, -1) in this case.
                return new RenderDataOffset(logicalLineIndex: 0, visibleCharIndex: -1);
            }

            if (point.Y < y || (point.Y == y && point.X < x))
            {
                // The given cursor is out of range, return (-1, -1).
                return new RenderDataOffset(-1, -1);
            }

            int prevX = 0, prevY = 0;
            int logicalLineIndex = 0;
            int bufferWidth = renderData.bufferWidth;

            for (; logicalLineIndex < renderData.lines.Length; logicalLineIndex++)
            {
                // Make 'prevX' and 'prevY' point to the start position where the current logical line would be rendered from.
                prevX = x;
                prevY = y;

                RenderedLineData lineData = renderData.lines[logicalLineIndex];
                int physicalLineCount = lineData.PhysicalLineCount(bufferWidth, initialX, out int lengthOfLastPhysicalLine);
                y += physicalLineCount - 1;

                if (y == initialY)
                {
                    x += lengthOfLastPhysicalLine;
                }
                else
                {
                    x = lengthOfLastPhysicalLine;
                }

                if (x == bufferWidth)
                {
                    // In the case that the length of last physical line takes the whole buffer width,
                    // the cursor would be pushed to the start of the next line.
                    x = 0;
                    y++;
                }

                if (point.Y == y && point.X == x)
                {
                    // The cursor is right at the end of the logical line.
                    // We use 'int.MaxValue' as the character index to indicate that the whole logical line is included.
                    return new RenderDataOffset(logicalLineIndex, visibleCharIndex: int.MaxValue);
                }

                if (point.Y < y || (point.Y == y && point.X < x))
                {
                    // The current logical line covers where the cursor is pointing at, so we will look for the character
                    // index within the logical line next.
                    break;
                }

                // Now move 'x' and 'y' to the start position where the next logical line would be rendered from.
                //  - when 'x > 0', move to the start of the next line;
                //  - when 'x == 0', we either already did this from above, or it's a special case:
                //     * the current logical line starts from the beginning of a physical line (x == 0), AND
                //     * the current logical line contains no visible character.
                if (x > 0 || lengthOfLastPhysicalLine == 0)
                {
                    x = 0;
                    y++;
                }
            }

            // If we didn't find the given cursor within the range of the rendered lines, return (-1, -1).
            if (logicalLineIndex == renderData.lines.Length || point.Y < prevY)
            {
                // - logicalLineIndex == renderData.lines.Length
                //   This could happen when the cursor was somehow pointing to a screen buffer position
                //   beyond the ending coordinate of the last logical line.
                //
                // - point.Y < prevY
                //   This could happen when the cursor was somehow pointing to a screen buffer position
                //   on the same last physical line of a logical line, but was beyond the ending X of
                //   the logical line. For example, the end of the logical line is at (x:3, y:2), and the
                //   cursor was pointing to (x:7, y:2).
                //
                // Both should never happen practically because we should never move cursor to an invalid
                // position like that. But we need to handle the extreme situation where either of them
                // just happened due to a bug in our code.
                return new RenderDataOffset(-1, -1);
            }

            // Now we have found the logical line that contains the visible character that the cursor was pointing at.
            // Move 'x' and 'y' back to the start point where that logical line would be rendered from.
            x = prevX;
            y = prevY;

            // If it's right at where the cursor was pointing to, then we are done.
            if (point.Y == y && point.X == x)
            {
                return new RenderDataOffset(logicalLineIndex, 0);
            }

            // Now we will scan the current logical line to find which character the cursor was pointing at.
            int visibleCharIndex = 0;
            string line = renderData.lines[logicalLineIndex].Line;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                // Simple escape sequence skipping.
                if (c == 0x1b && (i + 1) < line.Length && line[i + 1] == '[')
                {
                    i += 2;
                    while (i < line.Length && line[i] != 'm')
                    {
                        i++;
                    }

                    continue;
                }

                int size = LengthInBufferCells(c);
                x += size;

                if (x == bufferWidth)
                {
                    x = 0;
                    y++;
                }
                else if (x > bufferWidth)
                {
                    // It could wrap to the next line in case of a multi-cell character.
                    // If character didn't fit on current line, it will move entirely to the next line.
                    x = size;
                    y++;
                }

                if (point.Y == y)
                {
                    if (point.X < x)
                    {
                        // This could happen when the cursor was pointing to a double-cell character
                        // that was wrapped to the next physical line -- because there was only one
                        // cell space left at the end of the previous physical line.
                        return new RenderDataOffset(logicalLineIndex, visibleCharIndex);
                    }
                    else if (point.X == x)
                    {
                        // 'x' is pointing to where the next visible character would be rendered.
                        return new RenderDataOffset(logicalLineIndex, visibleCharIndex + 1);
                    }
                }

                visibleCharIndex++;
            }

            // We should never reach here in theory.
            return new RenderDataOffset(-1, -1);
        }

        /// <summary>
        /// Returns the logical line number under the cursor in a multi-line buffer.
        /// When rendering, a logical line may span multiple physical lines.
        /// </summary>
        private int GetLogicalLineNumber()
        {
            var current = _current;
            var lineNumber = 1;

            for (int i = 0; i < current; i++)
            {
                if (_buffer[i] == '\n')
                {
                    lineNumber++;
                }
            }
            return lineNumber;

        }

        /// <summary>
        /// Returns the number of logical lines in a multi-line buffer.
        /// When rendering, a logical line may span multiple physical lines.
        /// </summary>
        private int GetLogicalLineCount()
        {
            var count = 1;

            for (int i = 0; i < _buffer.Length; i++)
            {
                if (_buffer[i] == '\n')
                {
                    count++;
                }
            }
            return count;
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
                if (Options.DingDuration > 0)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Console.Beep(Options.DingTone, Options.DingDuration);
                    }
                    else
                    {
                        Console.Beep();
                    }
                }
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
            return key.KeyStr.Equals("y", StringComparison.OrdinalIgnoreCase);
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
