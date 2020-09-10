/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation.Subsystem;
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
            protected Task<List<PredictionResult>> _predictionTask;
            protected string _inputText;

            private HashSet<string> _cacheHistorySet;
            private List<SuggestionEntry> _cacheHistoryList;

            protected PredictionViewBase(PSConsoleReadLine singleton)
            {
                _singleton = singleton;
            }

            protected bool UsePlugin => (_singleton._options.PredictionSource & PredictionSource.Plugin) != 0;

            protected bool UseHistory => (_singleton._options.PredictionSource & PredictionSource.History) != 0;

            internal virtual bool HasPendingUpdate => false;

            internal abstract bool HasActiveSuggestion { get; }

            internal abstract void GetSuggestion(string userInput);

            internal abstract void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine);

            internal abstract void OnSuggestionAccepted();

            internal abstract void Clear(bool cursorAtEol);

            internal virtual void Reset()
            {
                _inputText = null;
                _predictionTask = null;
            }

            internal void OnCommandLineAccepted()
            {
                if (UsePlugin)
                {
                    _singleton._mockableMethods.OnCommandLineAccepted(_singleton._recentHistory.ToArray());
                }
            }

            /// <summary>
            /// Currently we only select single-line history that is prefixed with the user input,
            /// but it can be improved to not strictly use the user input as a prefix, but a hint
            /// to extract a partial pipeline or statement from a single-line or multiple-line
            /// history entry.
            /// </summary>
            protected string GetOneHistorySuggestion(string text)
            {
                var history = _singleton._history;
                var comparison = _singleton._options.HistoryStringComparison;

                for (int index = history.Count - 1; index >= 0; index--)
                {
                    var line = history[index].CommandLine.TrimEnd();
                    if (line.Length > text.Length)
                    {
                        bool isMultiLine = line.IndexOf('\n') != -1;
                        if (!isMultiLine && line.StartsWith(text, comparison))
                        {
                            return line;
                        }
                    }
                }

                return null;
            }

            protected List<SuggestionEntry> GetHistorySuggestions(string input, int count)
            {
                const string source = "History";
                List<SuggestionEntry> results = null;
                int remainingCount = count;

                var history = _singleton._history;
                var comparison = _singleton._options.HistoryStringComparison;
                var comparer = _singleton._options.HistoryStringComparer;

                _cacheHistorySet ??= new HashSet<string>(comparer);
                _cacheHistoryList ??= new List<SuggestionEntry>();

                for (int historyIndex = history.Count - 1; historyIndex >= 0; historyIndex--)
                {
                    var line = history[historyIndex].CommandLine.TrimEnd();

                    // Skip the history command lines that are smaller in length than the user input,
                    // or contain multiple logical lines.
                    if (line.Length <= input.Length || _cacheHistorySet.Contains(line) || line.IndexOf('\n') != -1)
                    {
                        continue;
                    }

                    int matchIndex = line.IndexOf(input, comparison);
                    if (matchIndex == -1)
                    {
                        continue;
                    }

                    if (results == null)
                    {
                        results = new List<SuggestionEntry>(capacity: count);
                    }

                    _cacheHistorySet.Add(line);
                    if (matchIndex == 0)
                    {
                        results.Add(new SuggestionEntry(source, Guid.Empty, line, matchIndex));
                        if (--remainingCount == 0)
                        {
                            break;
                        }
                    }
                    else if (_cacheHistoryList.Count < remainingCount)
                    {
                        _cacheHistoryList.Add(new SuggestionEntry(source, Guid.Empty, line, matchIndex));
                    }
                }

                if (remainingCount > 0 && _cacheHistoryList.Count > 0)
                {
                    for (int i = 0; i < remainingCount && i < _cacheHistoryList.Count; i++)
                    {
                        results.Add(_cacheHistoryList[i]);
                    }
                }

                _cacheHistorySet.Clear();
                _cacheHistoryList.Clear();
                return results;
            }

            protected void PredictInput()
            {
                _predictionTask = _singleton._mockableMethods.PredictInput(_singleton._ast, _singleton._tokens);
            }

            protected List<PredictionResult> GetPredictionResults()
            {
                try
                {
                    return _predictionTask?.Result;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    _predictionTask = null;
                }
            }
        }

        private class PredictionListView : PredictionViewBase
        {
            internal const int ListMaxCount = 10;
            internal const int ListMaxWidth = 100;
            internal const int SourceMaxWidth = 15;

            private List<SuggestionEntry> _listItems;
            private int _listItemWidth;
            private int _listItemHeight;
            private int _selectedIndex;
            private bool _updatePending;

            private List<int> _cacheList1;
            private List<int> _cacheList2;

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
                _inputText = userInput;
                _selectedIndex = -1;
                _listItemWidth = Math.Min(_singleton._console.BufferWidth, ListMaxWidth);
                _listItems?.Clear();

                try
                {
                    if (UsePlugin)
                    {
                        PredictInput();
                    }

                    if (UseHistory)
                    {
                        _listItems = GetHistorySuggestions(userInput, ListMaxCount);
                    }
                }
                catch
                {
                    Reset();
                }
            }

            private void AggregateSuggestions()
            {
                var results = GetPredictionResults();
                if (results?.Count > 0)
                {
                    try
                    {
                        _listItems ??= new List<SuggestionEntry>();
                        _cacheList1 ??= new List<int>();
                        _cacheList2 ??= new List<int>();

                        int pCount = 0;
                        int hCount = Math.Min(3, _listItems.Count);
                        int remRows = ListMaxCount - hCount;

                        foreach (var item in results)
                        {
                            if (item.Suggestions?.Count > 0)
                            {
                                pCount++;
                                _cacheList1.Add(item.Suggestions.Count);

                                if (pCount == remRows)
                                {
                                    break;
                                }
                            }
                        }

                        int ave = remRows / pCount;

                        for (int i = 0; i < pCount; i++)
                        {
                            int val = _cacheList1[i];
                            if (val > ave)
                            {
                                val = ave;
                            }

                            _cacheList1[i] -= val;
                            _cacheList2.Add(val);
                            remRows -= val;
                        }

                        bool more = true;
                        while (remRows > 0 && more)
                        {
                            more = false;
                            for (int i = 0; i < pCount; i++)
                            {
                                if (_cacheList1[i] == 0)
                                {
                                    continue;
                                }

                                _cacheList1[i]--;
                                _cacheList2[i]++;

                                remRows--;
                                if (remRows == 0)
                                {
                                    break;
                                }

                                more = _cacheList1[i] > 0;
                            }
                        }

                        if (hCount > 0)
                        {
                            _listItems.RemoveRange(hCount, _listItems.Count - hCount);
                        }

                        int index = -1;
                        var comparison = _singleton._options.HistoryStringComparison;

                        foreach (var item in results)
                        {
                            if (item.Suggestions?.Count > 0)
                            {
                                index++;
                                if (index == pCount)
                                {
                                    break;
                                }

                                for (int i = 0; i < _cacheList2[index]; i++)
                                {
                                    string sugText = item.Suggestions[i].SuggestionText ?? string.Empty;
                                    int matchIndex = sugText.IndexOf(_inputText, comparison);
                                    _listItems.Add(new SuggestionEntry(item.Name, item.Id, sugText, matchIndex));
                                }
                            }
                        }
                    }
                    finally
                    {
                        _cacheList1.Clear();
                        _cacheList2.Clear();
                    }
                }

                if (_listItems?.Count > 0)
                {
                    _listItemHeight = Math.Min(_listItems.Count, ListMaxCount);
                }
                else
                {
                    Reset();
                }
            }

            internal override void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine)
            {
                if (_updatePending)
                {
                    _updatePending = false;
                }
                else
                {
                    AggregateSuggestions();
                }

                if (_listItems == null)
                {
                    return;
                }

                for (int i = 0; i < _listItemHeight; i++)
                {
                    currentLogicalLine += 1;
                    if (currentLogicalLine == consoleBufferLines.Count)
                    {
                        consoleBufferLines.Add(new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH));
                    }

                    bool itemSelected = i == _selectedIndex;
                    StringBuilder currentLineBuffer = consoleBufferLines[currentLogicalLine];

                    if (itemSelected)
                    {
                        currentLineBuffer.Append(TextSelectedBg);
                    }

                    currentLineBuffer.Append(_listItems[i].GetListItemText(_listItemWidth, _inputText));

                    if (itemSelected)
                    {
                        currentLineBuffer.Append(AnsiReset);
                    }
                }
            }

            internal override void OnSuggestionAccepted()
            {
                if (!UsePlugin)
                {
                    return;
                }

                if (_listItems != null && _selectedIndex != -1)
                {
                    var item = _listItems[_selectedIndex];
                    if (item.PredictorId != Guid.Empty)
                    {
                        _singleton._mockableMethods.OnSuggestionAccepted(item.PredictorId, item.SuggestionText);
                    }
                }
            }

            internal override void Clear(bool cursorAtEol)
            {
                if (_listItems == null) { return; }

                int top = cursorAtEol
                    ? _singleton._console.CursorTop
                    : _singleton.ConvertOffsetToPoint(_inputText.Length).Y;

                _singleton.WriteBlankLines(top + 1, _listItemHeight);
                Reset();
            }

            internal override void Reset()
            {
                base.Reset();
                _listItems = null;
                _listItemWidth = _listItemHeight = _selectedIndex = -1;
                _updatePending = false;
            }

            internal void UpdateListSelection(int move)
            {
                int virtualItemIndex = _selectedIndex + 1;
                int virtualItemCount = _listItemHeight + 1;

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
            private Guid _predictorId;
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
                        _suggestionText = null;
                        _lastInputText = userInput;

                        if (UsePlugin)
                        {
                            PredictInput();
                        }

                        if (UseHistory)
                        {
                            _suggestionText = GetOneHistorySuggestion(userInput);
                            _predictorId = Guid.Empty;
                        }
                    }
                }
                catch
                {
                    Reset();
                }
            }

            private void AggregateSuggestions()
            {
                var results = GetPredictionResults();
                if (results?.Count > 0)
                {
                    foreach (var item in results)
                    {
                        if (item.Suggestions == null || item.Suggestions.Count == 0)
                        {
                            continue;
                        }

                        foreach (var sug in item.Suggestions)
                        {
                            if (sug.SuggestionText != null &&
                                sug.SuggestionText.StartsWith(_inputText, _singleton._options.HistoryStringComparison))
                            {
                                _predictorId = item.Id;
                                _suggestionText = sug.SuggestionText;
                                return;
                            }
                        }
                    }
                }
            }

            internal override void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine)
            {
                AggregateSuggestions();

                if (_suggestionText == null)
                {
                    Reset();
                    return;
                }

                int inputLength = _inputText.Length;
                StringBuilder currentLineBuffer = consoleBufferLines[currentLogicalLine];

                currentLineBuffer.Append(_singleton._options._predictionColor)
                    .Append(_suggestionText, inputLength, _suggestionText.Length - inputLength)
                    .Append(AnsiReset);
            }

            internal override void OnSuggestionAccepted()
            {
                if (!UsePlugin)
                {
                    return;
                }

                if (_suggestionText != null && _predictorId != Guid.Empty)
                {
                    _singleton._mockableMethods.OnSuggestionAccepted(_predictorId, _suggestionText);
                }
            }

            internal override void Clear(bool cursorAtEol)
            {
                if (_suggestionText == null)
                {
                    return;
                }

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
                base.Reset();
                _suggestionText = _lastInputText = null;
                _predictorId = Guid.Empty;
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
