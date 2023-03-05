/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation.Subsystem.Prediction;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;

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

                    _cacheHistorySet.Add(line);
                    results ??= new List<SuggestionEntry>(capacity: count);

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
				// Is it breaking abstraction too much to pull this directly from the singleton options? Should this be a separate property on the class itself? I see this done in other classes so I assume its OK.
				var predictionTimeout = _singleton.Options.PredictionTimeout;

				//TODO: Is there a better way to handle the nullable conversion here? I would think the pattern matching would allow it since I already tested for null but it still shows a compiler error here (can't convert int? to int) if I dont handle the nullable. -1 will throw an exception with this particular method which is what we want as this should never be null.
				_predictionTask = (predictionTimeout is null)
					? _singleton._mockableMethods.PredictInputAsync(_singleton._ast, _singleton._tokens)
					: _predictionTask = _singleton._mockableMethods.PredictInputAsync(_singleton._ast, _singleton._tokens, predictionTimeout ?? -1);
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
            // Item count constants.
            internal const int ListMaxCount = 50;
            internal const int HistoryMaxCount = 10;

            // List view constants.
            internal const int ListViewMaxHeight = 10;
            internal const int ListViewMaxWidth = 100;
            internal const int SourceMaxWidth = 15;

            // Minimal window size.
            internal const int MinWindowWidth = 50;
            internal const int MinWindowHeight = 5;

            // The items to be displayed in the list view.
            private List<SuggestionEntry> _listItems;
            // Information about the sources of those items.
            private List<SourceInfo> _sources;
            // The index that is currently selected by user.
            private int _selectedIndex;
            // Indicates to have the list view starts at the selected index.
            private bool _renderFromSelected;
            // Indicates a navigation update within the list view is pending.
            private bool _updatePending;

            // The max list height to be used for rendering, which is auto-adjusted based on terminal height.
            private int _maxViewHeight;
            // The actual height of the list view that is currently rendered.
            private int _listViewHeight;
            // The actual width of the list view that is currently rendered.
            private int _listViewWidth;
            // An index pointing to the item that is shown in the first slot of the list view.
            private int _listViewTop;
            // An index pointing to the item right AFTER the one that is shown in the last slot of the list view.
            private int _listViewEnd;

            // Indicates if we need to check on the height for each navigation in the list view.
            private bool _checkOnHeight;
            // To warn about that the terminal size is too small to display the list view.
            private bool _warnAboutSize;
            // Indicates if a warning message was displayed.
            private bool _warningPrinted;

            // Caches re-used when aggregating the suggestion results from predictors and history.
            // Those caches help us avoid allocation on tons of short-lived collections.
            private List<int> _cacheList1;
            private List<int> _cacheList2;
            private HashSet<string> _cachedHistorySet;
            private StringComparer _cachedComparer;

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

            /// <summary>
            /// Calculate the max width and height of the list view based on the current terminal size.
            /// </summary>
            private (int maxWidth, int maxHeight, bool checkOnHeight) RefreshMaxViewSize()
            {
                var console = _singleton._console;
                int maxWidth = Math.Min(console.BufferWidth, ListViewMaxWidth);

                (int maxHeight, bool moreCheck) = console.BufferHeight switch
                {
                    > ListViewMaxHeight * 2 => (ListViewMaxHeight, false),
                    > ListViewMaxHeight => (ListViewMaxHeight / 2, false),
                    _ => (ListViewMaxHeight / 3, true)
                };

                return (maxWidth, maxHeight, moreCheck);
            }

            /// <summary>
            /// Check if the height becomes too small for the current rendering.
            /// </summary>
            private bool HeightIsTooSmall()
            {
                int physicalLineCountForBuffer = _singleton.EndOfBufferPosition().Y - _singleton._initialY + 1;
                return _singleton._console.BufferHeight < physicalLineCountForBuffer + _maxViewHeight + 1 /* one metadata line */;
            }

            /// <summary>
            /// Get suggestion results.
            /// </summary>
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
                    // If the window size is too small to show the list view, we disable the list view and show a warning.
                    _warnAboutSize = true;
                    return;
                }

                _inputText = userInput;
                // Reset the list item selection.
                _selectedIndex = -1;
                // Refresh the list view width and height in case the terminal was resized.
                (_listViewWidth, _maxViewHeight, _checkOnHeight) = RefreshMaxViewSize();

                if (inputUnchanged)
                {
                    // This could happen when the user types 'ctrl+z' (undo) while looping through the suggestion list.
                    // The 'undo' operation would revert the line back to the original user input, and in that cases, we
                    // could reuse the existing suggestion results.
                    return;
                }

                _listItems?.Clear();
                _sources?.Clear();

                try
                {
                    if (UsePlugin)
                    {
                        PredictInput();
                    }

                    if (UseHistory)
                    {
                        _listItems = GetHistorySuggestions(userInput, HistoryMaxCount);
                        if (_listItems?.Count > 0)
                        {
                            _sources = new List<SourceInfo>() { new SourceInfo(SuggestionEntry.HistorySource, _listItems.Count - 1, prevSourceEndIndex: -1) };
                        }
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
                        _sources ??= new List<SourceInfo>();
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
                        // and in that case, the unused slots will become remaining slots which are to be
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

                                more |= _cacheList1[i] > 0;
                            }
                        }

                        if (hCount > 0)
                        {
                            if (hCount < _listItems.Count)
                            {
                                _listItems.RemoveRange(hCount, _listItems.Count - hCount);
                                _sources.Clear();
                                _sources.Add(new SourceInfo(SuggestionEntry.HistorySource, hCount - 1, prevSourceEndIndex: -1));
                            }

                            if (_cachedComparer != _singleton._options.HistoryStringComparer)
                            {
                                // Create the cached history set if not yet, or re-create the set if case-sensitivity was changed by the user.
                                _cachedComparer = _singleton._options.HistoryStringComparer;
                                _cachedHistorySet = new HashSet<string>(_cachedComparer);
                            }

                            foreach (SuggestionEntry entry in _listItems)
                            {
                                _cachedHistorySet.Add(entry.SuggestionText);
                            }
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

                                int skipCount = 0;
                                int num = _cacheList2[index];
                                foreach (PredictiveSuggestion suggestion in item.Suggestions)
                                {
                                    string sugText = suggestion.SuggestionText ?? string.Empty;
                                    if (_cachedHistorySet?.Contains(sugText) == true)
                                    {
                                        // Skip the prediction result that is exactly the same as one of the history results.
                                        skipCount++;
                                        continue;
                                    }

                                    int matchIndex = sugText.IndexOf(_inputText, comparison);
                                    _listItems.Add(new SuggestionEntry(item.Name, item.Id, item.Session, sugText, matchIndex));

                                    if (--num == 0)
                                    {
                                        // Break after we've added the desired number of prediction results.
                                        break;
                                    }
                                }

                                // Get the number of prediction results that were actually put in the list after filtering out the duplicate ones.
                                int count = _cacheList2[index] - num;
                                if (count > 0)
                                {
                                    // If we had at least one source, we take the end index of the last source in the list.
                                    int prevEndIndex = _sources.Count > 0 ? _sources[_sources.Count - 1].EndIndex : -1;
                                    int endIndex = _listItems.Count - 1;
                                    _sources.Add(new SourceInfo(_listItems[endIndex].Source, endIndex, prevEndIndex));

                                    if (item.Session.HasValue && count > 0)
                                    {
                                        // Send feedback only if the mini-session id is specified and we truely have its results in the list to be rendered.
                                        // When the mini-session id is not specified, we consider the predictor doesn't accept feedback.
                                        //
                                        // NOTE: when any duplicate results were skipped, the 'count' passed in here won't be accurate as it still includes
                                        // those skipped ones. This is due to the limitation of the 'OnSuggestionDisplayed' interface method, which didn't
                                        // assume any prediction results from a predictor could be filtered out at the initial design time. We will have to
                                        // change the predictor interface to pass in accurate information, such as:
                                        //   void OnSuggestionDisplayed(Guid predictorId, uint session, int countOrIndex, int[] skippedIndices)
                                        //
                                        // However, an interface change has huge impacts. At least, a newer version of PSReadLine will stop working on the
                                        // existing PowerShell 7+ versions. For this particular issue, the chance that it could happen is low and the impact
                                        // of the inaccurate feedback is also low, so we should delay this interface change until another highly-demanded
                                        // change to the interface is required in future (e.g. changes related to supporting OpenAI models).
                                        _singleton._mockableMethods.OnSuggestionDisplayed(item.Id, item.Session.Value, count + skipCount);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        _cacheList1.Clear();
                        _cacheList2.Clear();
                        _cachedHistorySet?.Clear();
                    }
                }

                if (_listItems?.Count > 0)
                {
                    // Initialize the view window position here.
                    _listViewTop = 0;
                    _listViewEnd = Math.Min(_listItems.Count, _maxViewHeight);
                    _listViewHeight = _listViewEnd - _listViewTop;
                }
                else
                {
                    Reset();
                }
            }

            /// <summary>
            /// Generate the rendering text for the list view.
            /// </summary>
            internal override void RenderSuggestion(List<StringBuilder> consoleBufferLines, ref int currentLogicalLine)
            {
                if (_warnAboutSize || (_checkOnHeight && HeightIsTooSmall()))
                {
                    _warningPrinted = true;
                    RenderWarningLine(NextBufferLine(consoleBufferLines, ref currentLogicalLine));

                    Reset();
                    return;
                }
                else
                {
                    _warningPrinted = false;
                }

                if (_updatePending)
                {
                    _updatePending = false;
                }
                else
                {
                    AggregateSuggestions();
                }

                if (_listItems is null)
                {
                    return;
                }

                // Create the metadata line.
                RenderMetadataLine(NextBufferLine(consoleBufferLines, ref currentLogicalLine));

                if (_selectedIndex >= 0)
                {
                    // An item was selected, so update the view window accrodingly.
                    if (_renderFromSelected)
                    {
                        // Render from the selected index if there are enough items left for a page.
                        // If not, then render all remaining items plus a few from above the selected one, so as to render a full page.
                        _renderFromSelected = false;
                        int offset = _maxViewHeight - Math.Min(_listItems.Count - _selectedIndex, _maxViewHeight);
                        _listViewTop = offset > 0 ? Math.Max(0, _selectedIndex - offset) : _selectedIndex;
                        _listViewEnd = Math.Min(_listItems.Count, _listViewTop + _maxViewHeight);
                    }
                    else
                    {
                        // - if the selected item is within the current top/end, then no need to move the list view window.
                        // - if the selected item is before the current top, then move the top to the selected item.
                        // - if the selected item is after the current end, then move the end to one beyond the selected item.
                        if (_selectedIndex < _listViewTop)
                        {
                            _listViewTop = _selectedIndex;
                            _listViewEnd = Math.Min(_listItems.Count, _selectedIndex + _maxViewHeight);
                        }
                        else if (_selectedIndex >= _listViewEnd)
                        {
                            _listViewEnd = _selectedIndex + 1;
                            _listViewTop = Math.Max(0, _listViewEnd - _maxViewHeight);
                        }
                    }

                    _listViewHeight = _listViewEnd - _listViewTop;
                }

                for (int i = _listViewTop; i < _listViewEnd; i++)
                {
                    bool itemSelected = i == _selectedIndex;
                    string selectionColor = itemSelected ? _singleton._options._listPredictionSelectedColor : null;

                    NextBufferLine(consoleBufferLines, ref currentLogicalLine)
                        .Append(_listItems[i].GetListItemText(
                            _listViewWidth,
                            _inputText,
                            selectionColor));
                }
            }

            /// <summary>
            /// Generate the rendering text for the warning message.
            /// </summary>
            private void RenderWarningLine(StringBuilder buffer)
            {
                // Add italic text effect to the highlight color.
                string highlightStyle = _singleton._options._listPredictionColor + "\x1b[3m";

                buffer.Append(highlightStyle)
                    .Append(PSReadLineResources.WindowSizeTooSmallWarning)
                    .Append(VTColorUtils.AnsiReset);
            }

            /// <summary>
            /// Calculate the height of the list when warning was displayed.
            /// </summary>
            private int GetPesudoListHeightForWarningRendering()
            {
                int bufferWidth = _singleton._console.BufferWidth;
                int lengthInCells = LengthInBufferCells(PSReadLineResources.WindowSizeTooSmallWarning);
                int pesudoListHeight = lengthInCells / bufferWidth;

                if (lengthInCells % bufferWidth == 0)
                {
                    pesudoListHeight--;
                }

                return pesudoListHeight;
            }

            /// <summary>
            /// Generate the rendering text for the metadata line.
            /// </summary>
            private void RenderMetadataLine(StringBuilder buffer)
            {
                // Add italic text effect to the highlight color.
                string highlightStyle = _singleton._options._listPredictionColor + "\x1b[3m";
                string dimmedStyle = PSConsoleReadLineOptions.DefaultInlinePredictionColor;
                string activeStyle = null;

                // Render the quick indicator.
                buffer.Append(highlightStyle)
                    .Append('<')
                    .Append(_selectedIndex > -1 ? _selectedIndex + 1 : "-")
                    .Append('/')
                    .Append(_listItems.Count)
                    .Append('>')
                    .Append(VTColorUtils.AnsiReset);

                if (_listViewWidth < 60)
                {
                    // We don't render the additional information about sources when the list view width is less than 60.
                    // Adjust the position of quick indicator a little bit in this case and call it done.
                    buffer.Insert(0, VTColorUtils.AnsiReset);
                    buffer.Insert(VTColorUtils.AnsiReset.Length, " ", count: 2);
                    return;
                }

                /// <summary>
                /// A helper function to avoid appending extra color VT sequences unnecessarily.
                /// </summary>
                static StringBuilder AppendColor(StringBuilder buffer, string colorToUse, ref string activeColor, out int nextCharPos)
                {
                    if (activeColor is null)
                    {
                        buffer.Append(colorToUse);
                    }
                    else if (activeColor != colorToUse)
                    {
                        buffer.Append(VTColorUtils.AnsiReset).Append(colorToUse);
                    }

                    activeColor = colorToUse;
                    nextCharPos = buffer.Length;
                    return buffer;
                }

                // The list view width decides how to render the source information:
                //  - when width >= 80, we render upto 3 sources,
                //  - when width >= 60, we render upto 2 sources.
                // The reason to select '80' and '60' here is because:
                //  - To render upto 3 sources, the maximum cell length that could be taken by both the total-count part and the extra-info part
                //    will be 75 (7+68), so we choose '80' as the minimal requirement for rendering 3 sources.
                //  - To render upto 2 sources, the maximum cell length that could be taken by both the total-count part and the extra-info part
                //    will be 55 (7+48), so we choose '60' as the minimal requirement for rendering 2 sources.
                int maxSourceCount = _listViewWidth >= 80 ? 3 : 2;
                int charPosition = buffer.Length;
                int totalCountPartLength = buffer.Length - highlightStyle.Length - VTColorUtils.AnsiReset.Length;
                int additionalPartLength = 0;

                int selected = -1;
                int startFrom = 0;

                // If a list item was selected, calculate which source the list item belongs to and which source
                // to start render for the additional information part.
                if (_selectedIndex > -1)
                {
                    for (int i = 0; i < _sources.Count; i++)
                    {
                        if (_selectedIndex <= _sources[i].EndIndex)
                        {
                            selected = i;
                            break;
                        }
                    }

                    if (selected == 0)
                    {
                        startFrom = 0;
                    }
                    else if (selected == _sources.Count - 1)
                    {
                        startFrom = Math.Max(0, selected - (maxSourceCount - 1));
                    }
                    else
                    {
                        startFrom = maxSourceCount == 3 ? selected - 1 : selected;
                    }
                }

                // Start the extra information about the sources -- add the opening arrow bracket.
                AppendColor(buffer, dimmedStyle, ref activeStyle, out _).Append('<');
                additionalPartLength++;

                // Add the prefix, continue to use dimmed color.
                if (startFrom > 0)
                {
                    buffer.Append(SuggestionEntry.Ellipsis).Append(' ');
                    additionalPartLength += 2;
                }

                // Add the sources.
                for (int i = 0; i < maxSourceCount; i++)
                {
                    int index = startFrom + i;
                    if (index == _sources.Count)
                    {
                        break;
                    }

                    if (i > 0)
                    {
                        // Add the separator.
                        buffer.Append(' ');
                        additionalPartLength++;
                    }

                    int nextCharPos;
                    SourceInfo info = _sources[index];
                    if (selected == index)
                    {
                        AppendColor(buffer, highlightStyle, ref activeStyle, out nextCharPos)
                            .Append(info.SourceName)
                            .Append('(')
                            .Append(_selectedIndex - info.PrevSourceEndIndex)
                            .Append('/')
                            .Append(info.ItemCount)
                            .Append(')');
                    }
                    else
                    {
                        AppendColor(buffer, dimmedStyle, ref activeStyle, out nextCharPos)
                            .Append(info.SourceName)
                            .Append('(')
                            .Append(info.ItemCount)
                            .Append(')');
                    }

                    // Need to take into account multi-cell characters when calculating length.
                    additionalPartLength += LengthInBufferCells(buffer, nextCharPos, buffer.Length);
                }

                // Add the suffix.
                if (startFrom + maxSourceCount < _sources.Count)
                {
                    AppendColor(buffer, dimmedStyle, ref activeStyle, out _)
                        .Append(' ')
                        .Append(SuggestionEntry.Ellipsis);
                    additionalPartLength += 2;
                }

                // Add the closing arrow bracket.
                AppendColor(buffer, dimmedStyle, ref activeStyle, out _)
                    .Append('>')
                    .Append(VTColorUtils.AnsiReset);
                additionalPartLength++;

                // Lastly, insert the padding spaces.
                int padding = _listViewWidth - additionalPartLength - totalCountPartLength;
                buffer.Insert(charPosition, " ", padding);
            }

            /// <summary>
            /// Trigger the feedback about a suggestion was accepted.
            /// </summary>
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

            /// <summary>
            /// Clear the list view.
            /// </summary>
            internal override void Clear(bool cursorAtEol)
            {
                if (_listItems == null && !_warningPrinted)
                {
                    return;
                }

                int listHeight = _warningPrinted
                    ? GetPesudoListHeightForWarningRendering()
                    : _listViewHeight;

                int top = cursorAtEol
                    ? _singleton._console.CursorTop
                    : _singleton.EndOfBufferPosition().Y;

                _warningPrinted = false;
                _singleton.WriteBlankLines(top + 1, listHeight + 1 /* plus 1 to include the metadata line */);
                Reset();
            }

            /// <summary>
            /// Reset all the list view states.
            /// </summary>
            internal override void Reset()
            {
                base.Reset();

                _sources = null;
                _listItems = null;
                _maxViewHeight = _listViewTop = _listViewEnd = _listViewWidth = _listViewHeight = _selectedIndex = -1;
                _warnAboutSize = _checkOnHeight = _updatePending = _renderFromSelected = false;
            }

            /// <summary>
            /// Update the index of the selected item based on <paramref name="move"/>.
            /// </summary>
            /// <param name="move"></param>
            internal void UpdateListSelection(int move)
            {
                // While moving around the list, we want to go back to the original input when we move one down
                // after the last item, or move one up before the first item in the list.
                // So, we can imagine a virtual list constructed by inserting the original input at the index 0
                // of the real list.
                int virtualItemIndex = _selectedIndex + 1;
                int virtualItemCount = _listItems.Count + 1;

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

            /// <summary>
            /// Page up/down within the list view.
            /// Update the index of the selected item based on <paramref name="pageUp"/> and <paramref name="num"/>.
            /// </summary>
            internal bool UpdateListByPaging(bool pageUp, int num)
            {
                if (_selectedIndex == -1)
                {
                    return false;
                }

                int oldSelectedIndex = _selectedIndex;
                int lastItemIndex = _listItems.Count - 1;

                for (int i = 0; i < num; i++)
                {
                    if (pageUp)
                    {
                        if (_selectedIndex == 0)
                        {
                            break;
                        }

                        // Do one page up.
                        _selectedIndex = _selectedIndex == _listViewEnd - 1
                            ? _listViewTop
                            : Math.Max(0, _selectedIndex - (_maxViewHeight - 1));
                    }
                    else
                    {
                        if (_selectedIndex == lastItemIndex)
                        {
                            break;
                        }

                        // Do one page down.
                        _selectedIndex = _selectedIndex == _listViewTop
                            ? _listViewEnd - 1
                            : Math.Min(lastItemIndex, _selectedIndex + (_maxViewHeight - 1));
                    }
                }

                if (_selectedIndex != oldSelectedIndex)
                {
                    // The selected item is changed, so we need to update the rendering.
                    _updatePending = true;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Loop up/down through the sources rendered in the list view.
            /// Update the index of the selected item based on <paramref name="jumpUp"/> and <paramref name="num"/>.
            /// </summary>
            internal bool UpdateListByLoopingSources(bool jumpUp, int num)
            {
                if (_selectedIndex == -1)
                {
                    return false;
                }

                int selectedSource = -1;
                for (int i = 0; i < _sources.Count; i++)
                {
                    if (_selectedIndex <= _sources[i].EndIndex)
                    {
                        selectedSource = i;
                        break;
                    }
                }

                int oldSelectedIndex = _selectedIndex;
                for (int i = 0; i < num; i++)
                {
                    if (jumpUp)
                    {
                        _selectedIndex = selectedSource == 0
                            ? _sources[_sources.Count - 1].PrevSourceEndIndex + 1
                            : _sources[selectedSource - 1].PrevSourceEndIndex + 1;
                    }
                    else
                    {
                        _selectedIndex = selectedSource == _sources.Count - 1
                            ? 0
                            : _sources[selectedSource].EndIndex + 1;
                    }
                }

                if (_selectedIndex != oldSelectedIndex)
                {
                    // The selected item is changed, so we need to update the rendering.
                    _updatePending = true;
                    _renderFromSelected = true;
                    return true;
                }

                return false;
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

                        // We need to truncate 4 buffer cells earlier (just to be safe), so we have enough room to add the ellipsis.
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
