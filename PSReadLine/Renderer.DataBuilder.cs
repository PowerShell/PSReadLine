using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;

namespace Microsoft.PowerShell;

public partial class Renderer
{
    private class DataBuilder
    {
        private readonly List<StringBuilder> _consoleBufferLines =
            new(1) {new StringBuilder(PSConsoleReadLineOptions.CommonWidestConsoleWidth)};

        private string _activeColor = string.Empty;
        private bool _afterLastToken;

        private Action<int> _appendColorAccordingSelection;
        private int _currentLogicalLine;
        private string _defaultColor = string.Empty;
        private bool _inSelectedRegion;
        private Action<int> _setTokenColorAction;
        private string _text;
        private string _tokenColor;

        public DataBuilder()
        {
            InitAppendColorAccordingSelection();
            InitSetTokenColorAction();
        }

        private void InitSetTokenColorAction()
        {
            if (_setTokenColorAction is null)
            {
                var tokenStack = new Stack<SavedTokenState>();
                tokenStack.Push(new SavedTokenState
                {
                    Tokens = _rl.Tokens,
                    Index = 0,
                    Color = _defaultColor
                });

                _setTokenColorAction = i =>
                {
                    if (!_afterLastToken)
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
                                    _afterLastToken = true;
                                    token = null;
                                    _tokenColor = _defaultColor;
                                    break;
                                }

                                state = tokenStack.Peek();

                                // It's possible that a 'StringExpandableToken' is the last available token, for example:
                                //   'begin $a\abc def', 'process $a\abc | blah' and 'end $a\abc; hello'
                                // due to the special handling of the keywords 'begin', 'process' and 'end', all the above 3 script inputs
                                // generate only 2 tokens by the parser -- A KeywordToken, and a StringExpandableToken '$a\abc'. Text after
                                // '$a\abc' is not tokenized at all.
                                // We repeat the test to see if we fall into this case ('token' is the final one in the stack).
                                continue;
                            }

                            _tokenColor = state.Color;
                            token = state.Tokens[++state.Index];
                        }

                        if (!_afterLastToken && i == token.Extent.StartOffset)
                        {
                            _tokenColor = GetTokenColor(token);

                            if (token is StringExpandableToken stringToken)
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
                                        Color = _tokenColor
                                    });

                                    if (i == tokens[0].Extent.StartOffset) _tokenColor = GetTokenColor(tokens[0]);
                                }
                        }
                    }
                };
            }
        }

        private void InitAppendColorAccordingSelection()
        {
            if (_appendColorAccordingSelection is null)
            {
                var selectionStart = -1;
                var selectionEnd = -1;
                if (_rl._visualSelectionCommandCount > 0)
                {
                    _renderer.GetRegion(out var regionStart, out var regionLength);
                    if (regionLength > 0)
                    {
                        selectionStart = regionStart;
                        selectionEnd = selectionStart + regionLength;
                    }
                }

                _appendColorAccordingSelection = i =>
                {
                    if (i == selectionStart)
                    {
                        _consoleBufferLines[_currentLogicalLine].Append(_rl.Options.SelectionColor);
                        _inSelectedRegion = true;
                    }
                    else if (i == selectionEnd)
                    {
                        _consoleBufferLines[_currentLogicalLine].Append(VTColorUtils.AnsiReset);
                        _consoleBufferLines[_currentLogicalLine].Append(_activeColor);
                        _inSelectedRegion = false;
                    }
                };
            }
        }


        private string GetTokenColor(Token token)
        {
            if ((token.TokenFlags & TokenFlags.CommandName) != 0) return _rl.Options._commandColor;

            switch (token.Kind)
            {
                case TokenKind.Comment:
                    return _rl.Options._commentColor;

                case TokenKind.Parameter:
                case TokenKind.Generic when token is StringLiteralToken slt && slt.Text.StartsWith("--"):
                    return _rl.Options._parameterColor;

                case TokenKind.Variable:
                case TokenKind.SplattedVariable:
                    return _rl.Options._variableColor;

                case TokenKind.StringExpandable:
                case TokenKind.StringLiteral:
                case TokenKind.HereStringExpandable:
                case TokenKind.HereStringLiteral:
                    return _rl.Options._stringColor;

                case TokenKind.Number:
                    return _rl.Options._numberColor;
            }

            if ((token.TokenFlags & TokenFlags.Keyword) != 0) return _rl.Options._keywordColor;

            if (token.Kind != TokenKind.Generic && (token.TokenFlags &
                                                    (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator |
                                                     TokenFlags.AssignmentOperator)) != 0)
                return _rl.Options._operatorColor;

            if ((token.TokenFlags & TokenFlags.TypeName) != 0) return _rl.Options._typeColor;

            if ((token.TokenFlags & TokenFlags.MemberName) != 0) return _rl.Options._memberColor;

            return _rl.Options._defaultTokenColor;
        }

        private void GenerateOneChar(int i)
        {
            var charToRender = _text[i];
            if (charToRender == '\n')
            {
                HandleLF(i);
            }
            else
            {
                ColorChar(i);

                if (char.IsControl(charToRender))
                    AppendControlChar(i);
                else
                    _consoleBufferLines[_currentLogicalLine].Append(charToRender);
            }
        }

        private void AppendControlChar(int i)
        {
            _consoleBufferLines[_currentLogicalLine].Append('^');
            _consoleBufferLines[_currentLogicalLine].Append((char) ('@' + _text[i]));
        }

        private void ColorChar(int i)
        {
            UpdateColorsIfNecessary(EP.ToEmphasize(i) ? _rl.Options._emphasisColor : _tokenColor);
        }

        private void HandleLF(int i)
        {
            if (_inSelectedRegion)
                // Turn off inverse before end of line, turn on after continuation prompt
                _consoleBufferLines[_currentLogicalLine].Append(VTColorUtils.AnsiReset);

            _currentLogicalLine += 1;
            if (_currentLogicalLine == _consoleBufferLines.Count)
                _consoleBufferLines.Add(new StringBuilder(PSConsoleReadLineOptions.CommonWidestConsoleWidth));

            // Reset the color for continuation prompt so the color sequence will always be explicitly
            // specified for continuation prompt in the generated render strings.
            // This is necessary because we will likely not rewrite all texts during rendering, and thus
            // we cannot assume the continuation prompt can continue to use the active color setting from
            // the previous rendering string.
            _activeColor = string.Empty;

            if (_rl.Options.ContinuationPrompt.Length > 0)
            {
                UpdateColorsIfNecessary(_rl.Options._continuationPromptColor);
                _consoleBufferLines[_currentLogicalLine].Append(_rl.Options.ContinuationPrompt);
            }

            if (_inSelectedRegion)
                // Turn off inverse before end of line, turn on after continuation prompt
                _consoleBufferLines[_currentLogicalLine].Append(_rl.Options.SelectionColor);
        }

        private void UpdateColorsIfNecessary(string newColor)
        {
            if (!ReferenceEquals(newColor, _activeColor))
            {
                if (!_inSelectedRegion)
                    _consoleBufferLines[_currentLogicalLine]
                        .Append(VTColorUtils.AnsiReset)
                        .Append(newColor);

                _activeColor = newColor;
            }
        }


        public RenderData Generate(string defaultColor)
        {
            _text = _rl.buffer.ToString();
            _rl._Prediction.QueryForSuggestion(_text);
            _defaultColor = defaultColor;
            _tokenColor = defaultColor;


            for (var i = 0; i < _text.Length; i++)
            {
                _appendColorAccordingSelection.Invoke(i);

                _setTokenColorAction(i);

                GenerateOneChar(i);
            }

            if (_inSelectedRegion)
            {
                _consoleBufferLines[_currentLogicalLine].Append(VTColorUtils.AnsiReset);
                _inSelectedRegion = false;
            }

            _rl._Prediction.ActiveView.RenderSuggestion(_consoleBufferLines, ref _currentLogicalLine);
            _activeColor = string.Empty;

            if (!string.IsNullOrEmpty(_renderer.StatusLinePrompt))
            {
                _currentLogicalLine += 1;
                if (_currentLogicalLine > _consoleBufferLines.Count - 1)
                    _consoleBufferLines.Add(new StringBuilder(PSConsoleReadLineOptions.CommonWidestConsoleWidth));

                _tokenColor = _rl._statusIsErrorMessage ? _rl.Options._errorColor : defaultColor;
                UpdateColorsIfNecessary(_tokenColor);

                foreach (var c in _renderer.StatusLinePrompt) _consoleBufferLines[_currentLogicalLine].Append(c);

                _consoleBufferLines[_currentLogicalLine].Append(_renderer.StatusBuffer);
            }

            return SolidifyData(_consoleBufferLines);
        }

        private static RenderData SolidifyData(List<StringBuilder> bufferLines)
        {
            var logicalLineCount = bufferLines.Count;

            // Now write that out (and remember what we did so we can clear previous renders
            // and minimize writing more than necessary on the next render.)

            var renderLines = new RenderedLineData[logicalLineCount];
            var renderData = new RenderData {lines = renderLines};
            for (var i = 0; i < logicalLineCount; i++)
            {
                var line = bufferLines[i].ToString();
                renderLines[i] = new RenderedLineData(line, i == 0);
            }

            return renderData;
        }
    }
}