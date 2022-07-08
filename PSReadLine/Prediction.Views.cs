/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation.Subsystem.Prediction;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// The base type of the prediction view.
        /// </summary>
        private abstract class PredictionViewBase
        {
            protected readonly PSConsoleReadLine _singleton;
            protected Task<List<PredictionResult>> _predictionTask;
            protected string _inputText;

            private HashSet<string> _cacheHistorySet;
            private List<SuggestionEntry> _cacheHistoryList;

            protected PredictionViewBase(PSConsoleReadLine singleton)
            {
                _singleton = singleton;
            }

            /// <summary>
            /// Gets whether to use plugin as a source.
            /// </summary>
            internal bool UsePlugin => (_singleton._options.PredictionSource & PredictionSource.Plugin) != 0;

            /// <summary>
            /// Gets whether to use history as a source.
            /// </summary>
            internal bool UseHistory => (_singleton._options.PredictionSource & PredictionSource.History) != 0;

            /// <summary>
            /// Gets whether an update to the view is pending.
            /// </summary>
            internal virtual bool HasPendingUpdate => false;

            /// <summary>
            /// Gets whether there is currently any suggestion results.
            /// </summary>
            internal abstract bool HasActiveSuggestion { get; }

            /// <summary>
            /// Get suggestion results.
            /// </summary>
            internal abstract void GetSuggestion(string userInput);

            /// <summary>
            /// Render the suggestion view.
            /// </summary>
            internal abstract void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine);

            /// <summary>
            /// Get called when a suggestion result is accepted.
            /// </summary>
            internal abstract void OnSuggestionAccepted();

            /// <summary>
            /// Clear the current suggestion view.
            /// </summary>
            /// <param name="cursorAtEol">Indicate if the cursor is currently at the end of input.</param>
            internal abstract void Clear(bool cursorAtEol);

            /// <summary>
            /// Reset the view instance.
            /// </summary>
            internal virtual void Reset()
            {
                _inputText = null;
                _predictionTask = null;
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

            /// <summary>
            /// Get multiple suggestion results from histories, as long as a history is a single-line
            /// command that contains the user input.
            /// We favor history commands that are prefixed with the user input over those that contain
            /// the user input in the middle or at the end.
            /// </summary>
            /// <param name="input">User input.</param>
            /// <param name="count">Maximum number of results to return.</param>
            protected List<SuggestionEntry> GetHistorySuggestions(string input, int count)
            {
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
                        results.Add(new SuggestionEntry(line, matchIndex));
                        if (--remainingCount == 0)
                        {
                            break;
                        }
                    }
                    else if (_cacheHistoryList.Count < remainingCount)
                    {
                        _cacheHistoryList.Add(new SuggestionEntry(line, matchIndex));
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

            /// <summary>
            /// Calls to the prediction API for suggestion results.
            /// </summary>
            protected void PredictInput()
            {
                _predictionTask = _singleton._mockableMethods.PredictInputAsync(_singleton._ast, _singleton._tokens);
            }

            /// <summary>
            /// Gets the results from the prediction task.
            /// </summary>
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

        /// <summary>
        /// This type represents the list view for prediction.
        /// </summary>
        private class PredictionListView : PredictionViewBase
        {
            internal const int ListMaxCount = 10;
            internal const int ListMaxWidth = 100;
            internal const int SourceMaxWidth = 15;

            internal const int MinWindowWidth = 54;
            internal const int MinWindowHeight = 15;

            private List<SuggestionEntry> _listItems;
            private int _listItemWidth;
            private int _listItemHeight;
            private int _selectedIndex;
            private bool _updatePending;

            // Caches re-used when aggregating the suggestion results from predictors and history.
            private List<int> _cacheList1;
            private List<int> _cacheList2;

            /// <summary>
            /// Gets whether the current window size meets the minimum requirement for the List view to work.
            /// </summary>
            private bool WindowSizeMeetsMinRequirement
            {
                get
                {
                    var console = _singleton._console;
                    return console.WindowWidth >= MinWindowWidth && console.WindowHeight >= MinWindowHeight;
                }
            }

            /// <summary>
            /// The index of the currently selected item.
            /// </summary>
            internal int SelectedItemIndex => _selectedIndex;

            /// <summary>
            /// The text of the currently selected item.
            /// </summary>
            internal string SelectedItemText
            {
                get
                {
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
                if (_singleton._initialY < 0)
                {
                    // Do not trigger list view prediction when the first line has already been scrolled up off the buffer.
                    // See https://github.com/PowerShell/PSReadLine/issues/3347 for an example where this may happen.
                    return;
                }

                bool inputUnchanged = string.Equals(_inputText, userInput, _singleton._options.HistoryStringComparison);
                if (!inputUnchanged && _selectedIndex > -1)
                {
                    // User input was changed while a specific list item was selected. This is a strong indicator that
                    // the selected list item was accepted, because the user was editing based on the selected item.
                    //
                    // But be noted that it's not guaranteed to be 100% accurate, because it's still possible that the
                    // user selected all the buffer and then was replacing it with something totally different.
                    OnSuggestionAccepted();
                }

                if (!WindowSizeMeetsMinRequirement)
                {
                    // If the window size is too small for the list view to work, we just disable the list view.
                    Reset();
                    return;
                }

                _inputText = userInput;
                _selectedIndex = -1;
                _listItemWidth = Math.Min(_singleton._console.BufferWidth, ListMaxWidth);

                if (inputUnchanged)
                {
                    // This could happen when the user types 'ctrl+z' (undo) while looping through the suggestion list.
                    // The 'undo' operation would revert the line back to the original user input, and in that cases, we
                    // could reuse the existing suggestion results.
                    return;
                }

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

            /// <summary>
            /// Aggregate the suggestion results from both the prediction API and the history.
            /// </summary>
            /// <remarks>
            /// If the prediction source contains both the history and plugin, then we allocate 3
            /// slots at most for history suggestions. The remaining slots are evenly distributed
            /// to each predictor plugin.
            /// </remarks>
            private void AggregateSuggestions()
            {
                var results = GetPredictionResults();
                if (results?.Count > 0)
                {
                    try
                    {
                        _listItems ??= new List<SuggestionEntry>();
                        _cacheList1 ??= new List<int>(); // This list holds the total number of suggestions from each of the predictors.
                        _cacheList2 ??= new List<int>(); // This list holds the final number of suggestions that will be rendered for each of the predictors.

                        int pCount = 0;
                        int hCount = Math.Min(3, _listItems.Count);
                        int remRows = ListMaxCount - hCount;

                        // Calculate the number of plugins that we need to handle,
                        // and the number of results each of them returned.
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

                        // Calculate the average slots to be allocated to each plugin.
                        int ave = remRows / pCount;

                        // Assign the results of each plugin to the average slots.
                        // Note that it's possible a plugin may return less results than the average slots,
                        // and in that case, the unused slots will be come remaining slots that are to be
                        // distributed again.
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

                        // Distribute the remaining slots to each of the plugins one by one.
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

                        // Finalize the list items by assign the results to the allocated slots for each plugin.
                        foreach (var item in results)
                        {
                            if (item.Suggestions?.Count > 0)
                            {
                                index++;
                                if (index == pCount)
                                {
                                    break;
                                }

                                int num = _cacheList2[index];
                                for (int i = 0; i < num; i++)
                                {
                                    string sugText = item.Suggestions[i].SuggestionText ?? string.Empty;
                                    int matchIndex = sugText.IndexOf(_inputText, comparison);
                                    _listItems.Add(new SuggestionEntry(item.Name, item.Id, item.Session, sugText, matchIndex));
                                }

                                if (item.Session.HasValue)
                                {
                                    // Send feedback only if the mini-session id is specified.
                                    // When it's not specified, we consider the predictor doesn't accept feedback.
                                    _singleton._mockableMethods.OnSuggestionDisplayed(item.Id, item.Session.Value, num);
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

                    string selectionColor = itemSelected ? _singleton._options._listPredictionSelectedColor : null;
                    currentLineBuffer.Append(
                        _listItems[i].GetListItemText(
                            _listItemWidth,
                            _inputText,
                            selectionColor));

                    if (itemSelected)
                    {
                        currentLineBuffer.Append(VTColorUtils.AnsiReset);
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
                    if (item.PredictorSession.HasValue)
                    {
                        // Send feedback only if the mini-session id is specified.
                        // When it's not specified, we consider the predictor doesn't accept feedback.
                        _singleton._mockableMethods.OnSuggestionAccepted(item.PredictorId, item.PredictorSession.Value, item.SuggestionText);
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

            /// <summary>
            /// Update the index of the selected item based on <paramref name="move"/>.
            /// </summary>
            /// <param name="move"></param>
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

        /// <summary>
        /// This type represents the inline view for prediction.
        /// </summary>
        private class PredictionInlineView : PredictionViewBase
        {
            private Guid _predictorId;
            private uint? _predictorSession;
            private string _suggestionText;
            private string _lastInputText;
            private int _renderedLength;
            private bool _alreadyAccepted;

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
                        _alreadyAccepted = false;
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
                            _predictorSession = null;
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

                        int index = 0;
                        foreach (var sug in item.Suggestions)
                        {
                            if (sug.SuggestionText != null &&
                                sug.SuggestionText.StartsWith(_inputText, _singleton._options.HistoryStringComparison))
                            {
                                _predictorId = item.Id;
                                _predictorSession = item.Session;
                                _suggestionText = sug.SuggestionText;

                                if (_predictorSession.HasValue)
                                {
                                    // Send feedback only if the mini-session id is specified.
                                    // When it's not specified, we consider the predictor doesn't accept feedback.
                                    _singleton._mockableMethods.OnSuggestionDisplayed(_predictorId, _predictorSession.Value, -index);
                                }

                                return;
                            }

                            index++;
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
                int totalLength = _suggestionText.Length;

                // Get the maximum buffer cells that could be available to the current command line.
                int maxBufferCells = _singleton._console.BufferHeight * _singleton._console.BufferWidth - _singleton._initialX;
                bool skipRendering = false;

                // Assuming the suggestion text contains wide characters only (1 character takes up 2 buffer cells),
                // if it still can fit in the console buffer, then we are all good; otherwise, it is possible that
                // it could not fit, and thus more calculation is needed to check if that's really the case.
                if (totalLength * 2 > maxBufferCells)
                {
                    int length = SubstringLengthByCells(_suggestionText, maxBufferCells);
                    if (length <= inputLength)
                    {
                        // Even the user input cannot fit in the console buffer without having part of it scrolled up-off the buffer.
                        // We don't attempt to render the suggestion text in this case.
                        skipRendering = true;
                    }
                    else if (length < totalLength)
                    {
                        // The whole suggestion text cannot fit in the console buffer without having part of it scrolled up off the buffer.
                        // We truncate the end part and append ellipsis.

                        // We need to truncate 4 buffer cells ealier (just to be safe), so we have enough room to add the ellipsis.
                        int lenFromEnd = SubstringLengthByCellsFromEnd(_suggestionText, length - 1, countOfCells: 4);
                        totalLength = length - lenFromEnd;
                        if (totalLength <= inputLength)
                        {
                            // No suggestion left after truncation, so no need to render.
                            skipRendering = true;
                        }
                    }
                }

                if (skipRendering)
                {
                    _renderedLength = 0;
                    return;
                }

                _renderedLength = totalLength;
                StringBuilder currentLineBuffer = consoleBufferLines[currentLogicalLine];

                currentLineBuffer
                    .Append(_singleton._options._inlinePredictionColor)
                    .Append(_suggestionText, inputLength, _renderedLength - inputLength);

                if (_renderedLength < _suggestionText.Length)
                {
                    currentLineBuffer.Append("...");
                }

                currentLineBuffer.Append(VTColorUtils.AnsiReset);
            }

            internal override void OnSuggestionAccepted()
            {
                if (!UsePlugin)
                {
                    return;
                }

                if (!_alreadyAccepted && _suggestionText != null && _predictorSession.HasValue)
                {
                    _alreadyAccepted = true;

                    // Send feedback only if the mini-session id is specified.
                    // When it's not specified, we consider the predictor doesn't accept feedback.
                    _singleton._mockableMethods.OnSuggestionAccepted(_predictorId, _predictorSession.Value, _suggestionText);
                }
            }

            internal override void Clear(bool cursorAtEol)
            {
                if (_suggestionText == null)
                {
                    return;
                }

                if (_renderedLength > 0)
                {
                    // Clear the suggestion only if we actually rendered it.
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
                    int columns = LengthInBufferCells(_suggestionText, inputLen, _renderedLength);

                    int remainingLenInCells = bufferWidth - left;
                    columns -= remainingLenInCells;
                    if (columns > 0)
                    {
                        int extra = columns % bufferWidth > 0 ? 1 : 0;
                        int count = columns / bufferWidth + extra;
                        _singleton.WriteBlankLines(top + 1, count);
                    }
                }

                Reset();
            }

            internal override void Reset()
            {
                base.Reset();
                _suggestionText = _lastInputText = null;
                _predictorId = Guid.Empty;
                _predictorSession = null;
                _alreadyAccepted = false;
                _renderedLength = 0;
            }

            /// <summary>
            /// Perform forward lookup to find the ending index of the next word in the suggestion text.
            /// </summary>
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
