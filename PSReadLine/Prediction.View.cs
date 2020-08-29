/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private abstract class PredictionViewBase
        {
            internal const string TextSelectedBg = "\x1b[48;5;238m";
            internal const string TextMetadataFg = "\x1b[33m";
            internal const string DefaultFg = "\x1b[39m";
            internal const string AnsiReset = "\x1b[0m";

            protected readonly PSConsoleReadLine _singleton;
            protected string _inputText;

            protected PredictionViewBase(PSConsoleReadLine singleton)
            {
                _singleton = singleton;
            }

            internal virtual bool HasPendingUpdate => false;
            internal abstract bool HasActiveSuggestion { get; }
            internal abstract void GetSuggestion(string userInput);
            internal abstract void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine);
            internal abstract void Clear(bool cursorAtEol);
            internal abstract void Reset();
        }

        private class PredictionListView : PredictionViewBase
        {
            internal const int ListMaxCount = 10;
            internal const int ListMaxWidth = 100;
            internal const int SourceWidth = 7;

            private List<SuggestionEntry> _listItems;
            private int _listItemWidth;
            private int _listItemCount;
            private int _selectedIndex;
            private bool _updatePending;

            internal int SelectedItemIndex => _selectedIndex;

            internal string SelectedItemText
            {
                get {
                    if (_listItems == null)
                        return null;

                    if (_selectedIndex >= 0)
                        return _listItems[_selectedIndex].SuggestionText;

                    if (_selectedIndex == -1)
                        return _inputText;

                    throw new InvalidOperationException("Unexpected '_selectedIndex' value: " + _selectedIndex);
                }
            }

            internal PredictionListView(PSConsoleReadLine singleton)
                : base(singleton)
            {
                Reset();
            }

            internal override bool HasPendingUpdate => _updatePending;
            internal override bool HasActiveSuggestion => _listItems != null;

            internal override void GetSuggestion(string userInput)
            {
                try
                {
                    _listItems = _singleton.GetHistorySuggestions(userInput, ListMaxCount);
                    if (_listItems != null)
                    {
                        _inputText = userInput;
                        _listItemWidth = Math.Min(_singleton._console.BufferWidth, ListMaxWidth);
                        _listItemCount = Math.Min(_listItems.Count, ListMaxCount);
                        _selectedIndex = -1;
                    }
                }
                catch
                {
                    Reset();
                }
            }

            internal override void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine)
            {
                _updatePending = false;
                if (_listItems == null) { return; }

                for (int i = 0; i < _listItemCount; i++)
                {
                    currentLogicalLine += 1;
                    if (currentLogicalLine == consoleBufferLines.Count)
                    {
                        consoleBufferLines.Add(new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH));
                    }

                    bool itemSelected = i == _selectedIndex;
                    StringBuilder currentLineBuffer = consoleBufferLines[currentLogicalLine];

                    if (itemSelected) currentLineBuffer.Append(TextSelectedBg);
                    currentLineBuffer.Append(_listItems[i].GetListItemText(_listItemWidth, _inputText));
                    if (itemSelected) currentLineBuffer.Append(AnsiReset);
                }
            }

            internal override void Clear(bool cursorAtEol)
            {
                if (_listItems == null) { return; }

                int top = cursorAtEol
                    ? _singleton._console.CursorTop
                    : _singleton.ConvertOffsetToPoint(_inputText.Length).Y;

                _singleton.WriteBlankLines(top + 1, _listItemCount);
                Reset();
            }

            internal override void Reset()
            {
                _inputText = null;
                _listItems = null;
                _listItemWidth = _listItemCount = _selectedIndex = -1;
                _updatePending = false;
            }

            internal void UpdateListSelection(int move)
            {
                int virtualItemIndex = _selectedIndex + 1;
                int virtualItemCount = _listItemCount + 1;

                _updatePending = true;
                virtualItemIndex += move;

                if (virtualItemIndex >= 0 && virtualItemIndex < virtualItemCount)
                {
                    _selectedIndex = virtualItemIndex - 1;
                }
                else if (virtualItemIndex >= virtualItemCount)
                {
                    _selectedIndex = virtualItemIndex % virtualItemCount - 1;
                }
                else
                {
                    _selectedIndex = virtualItemIndex % virtualItemCount + virtualItemCount - 1;
                }
            }
        }

        private class PredictionInlineView : PredictionViewBase
        {
            private string _suggestionText;
            private string _lastInputText;

            internal string SuggestionText => _suggestionText;

            internal PredictionInlineView(PSConsoleReadLine singleton)
                : base(singleton)
            {
            }

            internal override bool HasActiveSuggestion => _suggestionText != null;

            internal override void GetSuggestion(string userInput)
            {
                try
                {
                    _inputText = userInput;

                    if (_suggestionText == null || _suggestionText.Length <= userInput.Length ||
                        _lastInputText.Length > userInput.Length ||
                        !_suggestionText.StartsWith(userInput, _singleton._options.HistoryStringComparison))
                    {
                        _lastInputText = userInput;
                        _suggestionText = _singleton.GetHistorySuggestion(userInput);
                    }
                }
                catch
                {
                    Reset();
                }
            }

            internal override void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine)
            {
                if (_suggestionText == null) { return; }

                int inputLength = _inputText.Length;
                StringBuilder currentLineBuffer = consoleBufferLines[currentLogicalLine];

                currentLineBuffer.Append(_singleton._options._predictionColor)
                    .Append(_suggestionText, inputLength, _suggestionText.Length - inputLength)
                    .Append(DefaultFg);
            }

            internal override void Clear(bool cursorAtEol)
            {
                if (_suggestionText == null) { return; }

                int left, top;
                int inputLen = _inputText.Length;
                IConsole console = _singleton._console;

                if (cursorAtEol)
                {
                    left = console.CursorLeft;
                    top = console.CursorTop;
                    console.BlankRestOfLine();
                }
                else
                {
                    Point bufferEndPoint = _singleton.ConvertOffsetToPoint(inputLen);
                    left = bufferEndPoint.X;
                    top = bufferEndPoint.Y;
                    _singleton.WriteBlankRestOfLine(left, top);
                }

                int bufferWidth = console.BufferWidth;
                int columns = LengthInBufferCells(_suggestionText, inputLen, _suggestionText.Length);

                int remainingLenInCells = bufferWidth - left;
                columns -= remainingLenInCells;
                if (columns > 0)
                {
                    int extra = columns % bufferWidth > 0 ? 1 : 0;
                    int count = columns / bufferWidth + extra;
                    _singleton.WriteBlankLines(top + 1, count);
                }

                Reset();
            }

            internal override void Reset()
            {
                _suggestionText = _inputText = _lastInputText = null;
            }

            internal int FindForwardSuggestionWordPoint(int currentIndex, string wordDelimiters)
            {
                System.Diagnostics.Debug.Assert(
                    _suggestionText != null && _suggestionText.Length > _singleton._buffer.Length,
                    "Caller needs to make sure the suggestion text exist.");

                if (currentIndex >= _suggestionText.Length)
                {
                    return _suggestionText.Length;
                }

                int i = currentIndex;
                if (!_singleton.InWord(_suggestionText[i], wordDelimiters))
                {
                    // Scan to end of current non-word region
                    while (++i < _suggestionText.Length)
                    {
                        if (_singleton.InWord(_suggestionText[i], wordDelimiters))
                        {
                            break;
                        }
                    }
                }

                if (i < _suggestionText.Length)
                {
                    while (++i < _suggestionText.Length)
                    {
                        if (!_singleton.InWord(_suggestionText[i], wordDelimiters))
                        {
                            if (_suggestionText[i] == ' ')
                            {
                                // If the end of this suggestion word is a space, then we consider the word
                                // is complete and include the space in the accepted suggestion word.
                                i++;
                            }

                            break;
                        }
                    }
                }

                return i;
            }
        }
    }
}
