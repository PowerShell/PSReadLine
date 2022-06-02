/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation.Subsystem.Prediction;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        ///     The base type of the prediction view.
        /// </summary>
        private abstract class PredictionViewBase
        {
            protected readonly PSConsoleReadLine _singleton;
            private List<SuggestionEntry> _cacheHistoryList;

            private HashSet<string> _cacheHistorySet;
            protected string _inputText;
            protected Task<List<PredictionResult>> _predictionTask;

            protected PredictionViewBase(PSConsoleReadLine singleton)
            {
                _singleton = singleton;
            }

            /// <summary>
            ///     Gets whether to use plugin as a source.
            /// </summary>
            internal bool UsePlugin => (_singleton.Options.PredictionSource & PredictionSource.Plugin) != 0;

            /// <summary>
            ///     Gets whether to use history as a source.
            /// </summary>
            internal bool UseHistory => (_singleton.Options.PredictionSource & PredictionSource.History) != 0;

            /// <summary>
            ///     Gets whether an update to the view is pending.
            /// </summary>
            internal virtual bool HasPendingUpdate => false;

            /// <summary>
            ///     Gets whether there is currently any suggestion results.
            /// </summary>
            internal abstract bool HasActiveSuggestion { get; }

            /// <summary>
            ///     Get suggestion results.
            /// </summary>
            internal abstract void GetSuggestion(string userInput);

            /// <summary>
            ///     Render the suggestion view.
            /// </summary>
            internal abstract void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine);

            /// <summary>
            ///     Get called when a suggestion result is accepted.
            /// </summary>
            internal abstract void OnSuggestionAccepted();

            /// <summary>
            ///     Clear the current suggestion view.
            /// </summary>
            /// <param name="cursorAtEol">Indicate if the cursor is currently at the end of input.</param>
            internal abstract void Clear(bool cursorAtEol);

            /// <summary>
            ///     Reset the view instance.
            /// </summary>
            internal virtual void Reset()
            {
                _inputText = null;
                _predictionTask = null;
            }

            /// <summary>
            ///     Currently we only select single-line history that is prefixed with the user input,
            ///     but it can be improved to not strictly use the user input as a prefix, but a hint
            ///     to extract a partial pipeline or statement from a single-line or multiple-line
            ///     history entry.
            /// </summary>
            protected string GetOneHistorySuggestion(string text)
            {
                var history = _singleton._history;
                var comparison = _singleton.Options.HistoryStringComparison;

                for (var index = history.Count - 1; index >= 0; index--)
                {
                    var line = history[index].CommandLine.TrimEnd();
                    if (line.Length > text.Length)
                    {
                        var isMultiLine = line.IndexOf('\n') != -1;
                        if (!isMultiLine && line.StartsWith(text, comparison)) return line;
                    }
                }

                return null;
            }

            /// <summary>
            ///     Get multiple suggestion results from histories, as long as a history is a single-line
            ///     command that contains the user input.
            ///     We favor history commands that are prefixed with the user input over those that contain
            ///     the user input in the middle or at the end.
            /// </summary>
            /// <param name="input">User input.</param>
            /// <param name="count">Maximum number of results to return.</param>
            protected List<SuggestionEntry> GetHistorySuggestions(string input, int count)
            {
                List<SuggestionEntry> results = null;
                var remainingCount = count;

                var history = _singleton._history;
                var comparison = _singleton.Options.HistoryStringComparison;
                var comparer = _singleton.Options.HistoryStringComparer;

                _cacheHistorySet ??= new HashSet<string>(comparer);
                _cacheHistoryList ??= new List<SuggestionEntry>();

                for (var historyIndex = history.Count - 1; historyIndex >= 0; historyIndex--)
                {
                    var line = history[historyIndex].CommandLine.TrimEnd();

                    // Skip the history command lines that are smaller in length than the user input,
                    // or contain multiple logical lines.
                    if (line.Length <= input.Length || _cacheHistorySet.Contains(line) ||
                        line.IndexOf('\n') != -1) continue;

                    var matchIndex = line.IndexOf(input, comparison);
                    if (matchIndex == -1) continue;

                    if (results == null) results = new List<SuggestionEntry>(count);

                    _cacheHistorySet.Add(line);
                    if (matchIndex == 0)
                    {
                        results.Add(new SuggestionEntry(line, matchIndex));
                        if (--remainingCount == 0) break;
                    }
                    else if (_cacheHistoryList.Count < remainingCount)
                    {
                        _cacheHistoryList.Add(new SuggestionEntry(line, matchIndex));
                    }
                }

                if (remainingCount > 0 && _cacheHistoryList.Count > 0)
                    for (var i = 0; i < remainingCount && i < _cacheHistoryList.Count; i++)
                        results.Add(_cacheHistoryList[i]);

                _cacheHistorySet.Clear();
                _cacheHistoryList.Clear();
                return results;
            }

            /// <summary>
            ///     Calls to the prediction API for suggestion results.
            /// </summary>
            protected void PredictInput()
            {
                _predictionTask = _singleton._mockableMethods.PredictInputAsync(_singleton._ast, _singleton._tokens);
            }

            /// <summary>
            ///     Gets the results from the prediction task.
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
        ///     This type represents the list view for prediction.
        /// </summary>
        private class PredictionListView : PredictionViewBase
        {
            internal const int ListMaxCount = 10;
            internal const int ListMaxWidth = 100;
            internal const int SourceMaxWidth = 15;

            internal const int MinWindowWidth = 54;
            internal const int MinWindowHeight = 15;

            // Caches re-used when aggregating the suggestion results from predictors and history.
            private List<int> _cacheList1;
            private List<int> _cacheList2;
            private int _listItemHeight;

            private List<SuggestionEntry> _listItems;
            private int _listItemWidth;
            private bool _updatePending;

            internal PredictionListView(PSConsoleReadLine singleton)
                : base(singleton)
            {
                Reset();
            }

            /// <summary>
            ///     Gets whether the current window size meets the minimum requirement for the List view to work.
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
            ///     The index of the currently selected item.
            /// </summary>
            internal int SelectedItemIndex { get; private set; }

            /// <summary>
            ///     The text of the currently selected item.
            /// </summary>
            internal string SelectedItemText
            {
                get
                {
                    if (_listItems == null)
                        return null;

                    if (SelectedItemIndex >= 0)
                        return _listItems[SelectedItemIndex].SuggestionText;

                    if (SelectedItemIndex == -1)
                        return _inputText;

                    throw new InvalidOperationException("Unexpected '_selectedIndex' value: " + SelectedItemIndex);
                }
            }

            internal override bool HasPendingUpdate => _updatePending;
            internal override bool HasActiveSuggestion => _listItems != null;

            internal override void GetSuggestion(string userInput)
            {
                var inputUnchanged = string.Equals(_inputText, userInput, _singleton.Options.HistoryStringComparison);
                if (!inputUnchanged && SelectedItemIndex > -1)
                    // User input was changed while a specific list item was selected. This is a strong indicator that
                    // the selected list item was accepted, because the user was editing based on the selected item.
                    //
                    // But be noted that it's not guaranteed to be 100% accurate, because it's still possible that the
                    // user selected all the buffer and then was replacing it with something totally different.
                    OnSuggestionAccepted();

                if (!WindowSizeMeetsMinRequirement)
                {
                    // If the window size is too small for the list view to work, we just disable the list view.
                    Reset();
                    return;
                }

                _inputText = userInput;
                SelectedItemIndex = -1;
                _listItemWidth = Math.Min(_singleton._console.BufferWidth, ListMaxWidth);

                if (inputUnchanged)
                    // This could happen when the user types 'ctrl+z' (undo) while looping through the suggestion list.
                    // The 'undo' operation would revert the line back to the original user input, and in that cases, we
                    // could reuse the existing suggestion results.
                    return;

                _listItems?.Clear();

                try
                {
                    if (UsePlugin) PredictInput();

                    if (UseHistory) _listItems = GetHistorySuggestions(userInput, ListMaxCount);
                }
                catch
                {
                    Reset();
                }
            }

            /// <summary>
            ///     Aggregate the suggestion results from both the prediction API and the history.
            /// </summary>
            /// <remarks>
            ///     If the prediction source contains both the history and plugin, then we allocate 3
            ///     slots at most for history suggestions. The remaining slots are evenly distributed
            ///     to each predictor plugin.
            /// </remarks>
            private void AggregateSuggestions()
            {
                var results = GetPredictionResults();
                if (results?.Count > 0)
                    try
                    {
                        _listItems ??= new List<SuggestionEntry>();
                        _cacheList1 ??=
                            new List<int>(); // This list holds the total number of suggestions from each of the predictors.
                        _cacheList2 ??=
                            new List<int>(); // This list holds the final number of suggestions that will be rendered for each of the predictors.

                        var pCount = 0;
                        var hCount = Math.Min(3, _listItems.Count);
                        var remRows = ListMaxCount - hCount;

                        // Calculate the number of plugins that we need to handle,
                        // and the number of results each of them returned.
                        foreach (var item in results)
                            if (item.Suggestions?.Count > 0)
                            {
                                pCount++;
                                _cacheList1.Add(item.Suggestions.Count);

                                if (pCount == remRows) break;
                            }

                        // Calculate the average slots to be allocated to each plugin.
                        var ave = remRows / pCount;

                        // Assign the results of each plugin to the average slots.
                        // Note that it's possible a plugin may return less results than the average slots,
                        // and in that case, the unused slots will be come remaining slots that are to be
                        // distributed again.
                        for (var i = 0; i < pCount; i++)
                        {
                            var val = _cacheList1[i];
                            if (val > ave) val = ave;

                            _cacheList1[i] -= val;
                            _cacheList2.Add(val);
                            remRows -= val;
                        }

                        // Distribute the remaining slots to each of the plugins one by one.
                        var more = true;
                        while (remRows > 0 && more)
                        {
                            more = false;
                            for (var i = 0; i < pCount; i++)
                            {
                                if (_cacheList1[i] == 0) continue;

                                _cacheList1[i]--;
                                _cacheList2[i]++;

                                remRows--;
                                if (remRows == 0) break;

                                more = _cacheList1[i] > 0;
                            }
                        }

                        if (hCount > 0) _listItems.RemoveRange(hCount, _listItems.Count - hCount);

                        var index = -1;
                        var comparison = _singleton.Options.HistoryStringComparison;

                        // Finalize the list items by assign the results to the allocated slots for each plugin.
                        foreach (var item in results)
                            if (item.Suggestions?.Count > 0)
                            {
                                index++;
                                if (index == pCount) break;

                                var num = _cacheList2[index];
                                for (var i = 0; i < num; i++)
                                {
                                    var sugText = item.Suggestions[i].SuggestionText ?? string.Empty;
                                    var matchIndex = sugText.IndexOf(_inputText, comparison);
                                    _listItems.Add(new SuggestionEntry(item.Name, item.Id, item.Session, sugText,
                                        matchIndex));
                                }

                                if (item.Session.HasValue)
                                    // Send feedback only if the mini-session id is specified.
                                    // When it's not specified, we consider the predictor doesn't accept feedback.
                                    _singleton._mockableMethods.OnSuggestionDisplayed(item.Id, item.Session.Value, num);
                            }
                    }
                    finally
                    {
                        _cacheList1.Clear();
                        _cacheList2.Clear();
                    }

                if (_listItems?.Count > 0)
                    _listItemHeight = Math.Min(_listItems.Count, ListMaxCount);
                else
                    Reset();
            }

            internal override void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine)
            {
                if (_updatePending)
                    _updatePending = false;
                else
                    AggregateSuggestions();

                if (_listItems == null) return;

                for (var i = 0; i < _listItemHeight; i++)
                {
                    currentLogicalLine += 1;
                    if (currentLogicalLine == consoleBufferLines.Count)
                        consoleBufferLines.Add(new StringBuilder(COMMON_WIDEST_CONSOLE_WIDTH));

                    var itemSelected = i == SelectedItemIndex;
                    var currentLineBuffer = consoleBufferLines[currentLogicalLine];

                    var selectionColor = itemSelected ? _singleton.Options._listPredictionSelectedColor : null;
                    currentLineBuffer.Append(
                        _listItems[i].GetListItemText(
                            _listItemWidth,
                            _inputText,
                            selectionColor));

                    if (itemSelected) currentLineBuffer.Append(VTColorUtils.AnsiReset);
                }
            }

            internal override void OnSuggestionAccepted()
            {
                if (!UsePlugin) return;

                if (_listItems != null && SelectedItemIndex != -1)
                {
                    var item = _listItems[SelectedItemIndex];
                    if (item.PredictorSession.HasValue)
                        // Send feedback only if the mini-session id is specified.
                        // When it's not specified, we consider the predictor doesn't accept feedback.
                        _singleton._mockableMethods.OnSuggestionAccepted(item.PredictorId, item.PredictorSession.Value,
                            item.SuggestionText);
                }
            }

            internal override void Clear(bool cursorAtEol)
            {
                if (_listItems == null) return;

                var top = cursorAtEol
                    ? _singleton._console.CursorTop
                    : _singleton.ConvertOffsetToPoint(_inputText.Length).Y;

                _singleton.WriteBlankLines(top + 1, _listItemHeight);
                Reset();
            }

            internal override void Reset()
            {
                base.Reset();
                _listItems = null;
                _listItemWidth = _listItemHeight = SelectedItemIndex = -1;
                _updatePending = false;
            }

            /// <summary>
            ///     Update the index of the selected item based on <paramref name="move" />.
            /// </summary>
            /// <param name="move"></param>
            internal void UpdateListSelection(int move)
            {
                var virtualItemIndex = SelectedItemIndex + 1;
                var virtualItemCount = _listItemHeight + 1;

                _updatePending = true;
                virtualItemIndex += move;

                if (virtualItemIndex >= 0 && virtualItemIndex < virtualItemCount)
                    SelectedItemIndex = virtualItemIndex - 1;
                else if (virtualItemIndex >= virtualItemCount)
                    SelectedItemIndex = virtualItemIndex % virtualItemCount - 1;
                else
                    SelectedItemIndex = virtualItemIndex % virtualItemCount + virtualItemCount - 1;
            }
        }

        /// <summary>
        ///     This type represents the inline view for prediction.
        /// </summary>
        private class PredictionInlineView : PredictionViewBase
        {
            private bool _alreadyAccepted;
            private string _lastInputText;
            private Guid _predictorId;
            private uint? _predictorSession;
            private int _renderedLength;

            internal PredictionInlineView(PSConsoleReadLine singleton)
                : base(singleton)
            {
            }

            internal string SuggestionText { get; private set; }

            internal override bool HasActiveSuggestion => SuggestionText != null;

            internal override void GetSuggestion(string userInput)
            {
                try
                {
                    _inputText = userInput;

                    if (SuggestionText == null || SuggestionText.Length <= userInput.Length ||
                        _lastInputText.Length > userInput.Length ||
                        !SuggestionText.StartsWith(userInput, _singleton.Options.HistoryStringComparison))
                    {
                        _alreadyAccepted = false;
                        SuggestionText = null;
                        _lastInputText = userInput;

                        if (UsePlugin) PredictInput();

                        if (UseHistory)
                        {
                            SuggestionText = GetOneHistorySuggestion(userInput);
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
                    foreach (var item in results)
                    {
                        if (item.Suggestions == null || item.Suggestions.Count == 0) continue;

                        var index = 0;
                        foreach (var sug in item.Suggestions)
                        {
                            if (sug.SuggestionText != null &&
                                sug.SuggestionText.StartsWith(_inputText, _singleton.Options.HistoryStringComparison))
                            {
                                _predictorId = item.Id;
                                _predictorSession = item.Session;
                                SuggestionText = sug.SuggestionText;

                                if (_predictorSession.HasValue)
                                    // Send feedback only if the mini-session id is specified.
                                    // When it's not specified, we consider the predictor doesn't accept feedback.
                                    _singleton._mockableMethods.OnSuggestionDisplayed(_predictorId,
                                        _predictorSession.Value, -index);

                                return;
                            }

                            index++;
                        }
                    }
            }

            internal override void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine)
            {
                AggregateSuggestions();

                if (SuggestionText == null)
                {
                    Reset();
                    return;
                }

                var inputLength = _inputText.Length;
                var totalLength = SuggestionText.Length;

                // Get the maximum buffer cells that could be available to the current command line.
                var maxBufferCells = _singleton._console.BufferHeight * _singleton._console.BufferWidth -
                                     _singleton._initialX;
                var skipRendering = false;

                // Assuming the suggestion text contains wide characters only (1 character takes up 2 buffer cells),
                // if it still can fit in the console buffer, then we are all good; otherwise, it is possible that
                // it could not fit, and thus more calculation is needed to check if that's really the case.
                if (totalLength * 2 > maxBufferCells)
                {
                    var length = SubstringLengthByCells(SuggestionText, maxBufferCells);
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
                        var lenFromEnd = SubstringLengthByCellsFromEnd(SuggestionText, length - 1, 4);
                        totalLength = length - lenFromEnd;
                        if (totalLength <= inputLength)
                            // No suggestion left after truncation, so no need to render.
                            skipRendering = true;
                    }
                }

                if (skipRendering)
                {
                    _renderedLength = 0;
                    return;
                }

                _renderedLength = totalLength;
                var currentLineBuffer = consoleBufferLines[currentLogicalLine];

                currentLineBuffer
                    .Append(_singleton.Options._inlinePredictionColor)
                    .Append(SuggestionText, inputLength, _renderedLength - inputLength);

                if (_renderedLength < SuggestionText.Length) currentLineBuffer.Append("...");

                currentLineBuffer.Append(VTColorUtils.AnsiReset);
            }

            internal override void OnSuggestionAccepted()
            {
                if (!UsePlugin) return;

                if (!_alreadyAccepted && SuggestionText != null && _predictorSession.HasValue)
                {
                    _alreadyAccepted = true;

                    // Send feedback only if the mini-session id is specified.
                    // When it's not specified, we consider the predictor doesn't accept feedback.
                    _singleton._mockableMethods.OnSuggestionAccepted(_predictorId, _predictorSession.Value,
                        SuggestionText);
                }
            }

            internal override void Clear(bool cursorAtEol)
            {
                if (SuggestionText == null) return;

                if (_renderedLength > 0)
                {
                    // Clear the suggestion only if we actually rendered it.
                    int left, top;
                    var inputLen = _inputText.Length;
                    var console = _singleton._console;

                    if (cursorAtEol)
                    {
                        left = console.CursorLeft;
                        top = console.CursorTop;
                        console.BlankRestOfLine();
                    }
                    else
                    {
                        var bufferEndPoint = _singleton.ConvertOffsetToPoint(inputLen);
                        left = bufferEndPoint.X;
                        top = bufferEndPoint.Y;
                        _singleton.WriteBlankRestOfLine(left, top);
                    }

                    var bufferWidth = console.BufferWidth;
                    var columns = LengthInBufferCells(SuggestionText, inputLen, _renderedLength);

                    var remainingLenInCells = bufferWidth - left;
                    columns -= remainingLenInCells;
                    if (columns > 0)
                    {
                        var extra = columns % bufferWidth > 0 ? 1 : 0;
                        var count = columns / bufferWidth + extra;
                        _singleton.WriteBlankLines(top + 1, count);
                    }
                }

                Reset();
            }

            internal override void Reset()
            {
                base.Reset();
                SuggestionText = _lastInputText = null;
                _predictorId = Guid.Empty;
                _predictorSession = null;
                _alreadyAccepted = false;
                _renderedLength = 0;
            }

            /// <summary>
            ///     Perform forward lookup to find the ending index of the next word in the suggestion text.
            /// </summary>
            internal int FindForwardSuggestionWordPoint(int currentIndex, string wordDelimiters)
            {
                Debug.Assert(
                    SuggestionText != null && SuggestionText.Length > _singleton._buffer.Length,
                    "Caller needs to make sure the suggestion text exist.");

                if (currentIndex >= SuggestionText.Length) return SuggestionText.Length;

                var i = currentIndex;
                if (!_singleton.InWord(SuggestionText[i], wordDelimiters))
                    // Scan to end of current non-word region
                    while (++i < SuggestionText.Length)
                        if (_singleton.InWord(SuggestionText[i], wordDelimiters))
                            break;

                if (i < SuggestionText.Length)
                    while (++i < SuggestionText.Length)
                        if (!_singleton.InWord(SuggestionText[i], wordDelimiters))
                        {
                            if (SuggestionText[i] == ' ')
                                // If the end of this suggestion word is a space, then we consider the word
                                // is complete and include the space in the accepted suggestion word.
                                i++;

                            break;
                        }

                return i;
            }
        }
    }
}