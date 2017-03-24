/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        // Tab completion state
        private int _tabCommandCount;
        private CommandCompletion _tabCompletions;
        private Runspace _runspace;

        // Stub helper method so completion can be mocked
        [ExcludeFromCodeCoverage]
        CommandCompletion IPSConsoleReadLineMockableMethods.CompleteInput(string input, int cursorIndex, Hashtable options, System.Management.Automation.PowerShell powershell)
        {
            return CalloutUsingDefaultConsoleMode(
                () => CommandCompletion.CompleteInput(input, cursorIndex, options, powershell));
        }

        /// <summary>
        /// Attempt to complete the text surrounding the cursor with the next
        /// available completion.
        /// </summary>
        public static void TabCompleteNext(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.Complete(forward: true);
        }

        /// <summary>
        /// Attempt to complete the text surrounding the cursor with the previous
        /// available completion.
        /// </summary>
        public static void TabCompletePrevious(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.Complete(forward: false);
        }

        private static bool IsSingleQuote(char c)
        {
            return c == '\'' || c == (char)8216 || c == (char)8217 || c == (char)8218;
        }

        private static bool IsDoubleQuote(char c)
        {
            return c == '"' || c == (char)8220 || c == (char)8221;
        }

        private static bool IsQuoted(string s)
        {
            if (s.Length >= 2)
            {
                var first = s[0];
                var last = s[s.Length - 1];

                return ((IsSingleQuote(first) && IsSingleQuote(last))
                        ||
                        (IsDoubleQuote(first) && IsDoubleQuote(last)));
            }
            return false;
        }

        private static string GetUnquotedText(string s, bool consistentQuoting)
        {
            if (!consistentQuoting && IsQuoted(s))
            {
                s = s.Substring(1, s.Length - 2);
            }
            return s;
        }

        /// <summary>
        /// Attempt to perform completion on the text surrounding the cursor.
        /// If there are multiple possible completions, the longest unambiguous
        /// prefix is used for completion.  If trying to complete the longest
        /// unambiguous completion, a list of possible completions is displayed.
        /// </summary>
        public static void Complete(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.CompleteImpl(key, arg, false);
        }

        /// <summary>
        /// Attempt to perform completion on the text surrounding the cursor.
        /// If there are multiple possible completions, the longest unambiguous
        /// prefix is used for completion.  If trying to complete the longest
        /// unambiguous completion, a list of possible completions is displayed.
        /// </summary>
        public static void MenuComplete(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.CompleteImpl(key, arg, true);
        }

        private void CompleteImpl(ConsoleKeyInfo? key, object arg, bool menuSelect)
        {
            var completions = GetCompletions();
            if (completions == null || completions.CompletionMatches.Count == 0)
                return;

            if (_tabCommandCount > 0)
            {
                if (completions.CompletionMatches.Count == 1)
                {
                    Ding();
                }
                else
                {
                    PossibleCompletionsImpl(completions, menuSelect);
                }
                return;
            }

            if (completions.CompletionMatches.Count == 1)
            {
                // We want to add a backslash for directory completion if possible.  This
                // is mostly only needed if we have a single completion - if there are multiple
                // completions, then we'll be showing the possible completions where it's very
                // unlikely that we would add a trailing backslash.

                DoReplacementForCompletion(completions.CompletionMatches[0], completions);
                return;
            }

            if (menuSelect)
            {
                PossibleCompletionsImpl(completions, true);
                return;
            }

            // Find the longest unambiguous prefix.  This might be the empty
            // string, in which case we don't want to remove any of the users input,
            // instead we'll immediately show possible completions.
            // For the purposes of unambiguous prefix, we'll ignore quotes if
            // some completions aren't quoted.
            var firstResult = completions.CompletionMatches[0];
            int quotedCompletions = completions.CompletionMatches.Count(match => IsQuoted(match.CompletionText));
            bool consistentQuoting =
                quotedCompletions == 0 ||
                (quotedCompletions == completions.CompletionMatches.Count &&
                 quotedCompletions == completions.CompletionMatches.Count(
                    m => m.CompletionText[0] == firstResult.CompletionText[0]));

            bool ambiguous = false;
            var replacementText = GetUnquotedText(firstResult.CompletionText, consistentQuoting);
            foreach (var match in completions.CompletionMatches.Skip(1)) 
            {
                var matchText = GetUnquotedText(match.CompletionText, consistentQuoting);
                for (int i = 0; i < replacementText.Length; i++)
                {
                    if (i == matchText.Length
                        || char.ToLowerInvariant(replacementText[i]) != char.ToLowerInvariant(matchText[i]))
                    {
                        ambiguous = true;
                        replacementText = replacementText.Substring(0, i);
                        break;
                    }
                }
                if (replacementText.Length == 0)
                {
                    break;
                }
            }

            if (replacementText.Length > 0)
            {
                Replace(completions.ReplacementIndex, completions.ReplacementLength, replacementText);
                completions.ReplacementLength = replacementText.Length;

                if (ambiguous)
                {
                    Ding();
                }
            }
            else
            {
                // No common prefix, don't wait for a second tab, just show the possible completions
                // right away.
                PossibleCompletionsImpl(completions, false);
            }

            _tabCommandCount += 1;
        }

        private CommandCompletion GetCompletions()
        {
            if (_tabCommandCount == 0)
            {
                try
                {
                    _tabCompletions = null;

                    // Could use the overload that takes an AST as it's faster (we've already parsed the
                    // input for coloring) but that overload is a little more complicated in passing in the
                    // cursor position.
                    System.Management.Automation.PowerShell ps;
                    if (!_mockableMethods.RunspaceIsRemote(_runspace))
                    {
                        ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                    }
                    else
                    {
                        ps = System.Management.Automation.PowerShell.Create();
                        ps.Runspace = _runspace;
                    }
                    _tabCompletions = _mockableMethods.CompleteInput(_buffer.ToString(), _current, null, ps);

                    if (_tabCompletions.CompletionMatches.Count == 0)
                        return null;
                }
                catch (Exception)
                {
                }                
            }

            return _tabCompletions;
        }

        private void Complete(bool forward)
        {
            var completions = GetCompletions();
            if (completions == null)
                return;

            completions.CurrentMatchIndex += forward ? 1 : -1;
            if (completions.CurrentMatchIndex < 0)
            {
                completions.CurrentMatchIndex = completions.CompletionMatches.Count - 1;
            }
            else if (completions.CurrentMatchIndex == completions.CompletionMatches.Count)
            {
                completions.CurrentMatchIndex = 0;
            }

            var completionResult = completions.CompletionMatches[completions.CurrentMatchIndex];
            DoReplacementForCompletion(completionResult, completions);
            _tabCommandCount += 1;
        }

        private void DoReplacementForCompletion(CompletionResult completionResult, CommandCompletion completions)
        {
            var replacementText = completionResult.CompletionText;
            int cursorAdjustment = 0;
            if (completionResult.ResultType == CompletionResultType.ProviderContainer)
            {
                replacementText = GetReplacementTextForDirectory(replacementText, ref cursorAdjustment);
            }
            Replace(completions.ReplacementIndex, completions.ReplacementLength, replacementText);
            if (cursorAdjustment != 0)
            {
                _current += cursorAdjustment;
                PlaceCursor();
            }
            completions.ReplacementLength = replacementText.Length;
        }

        private static string GetReplacementTextForDirectory(string replacementText, ref int cursorAdjustment)
        {
            if (!replacementText.EndsWith("\\", StringComparison.Ordinal))
            {
                if (replacementText.EndsWith("\\'", StringComparison.Ordinal) || replacementText.EndsWith("\\\"", StringComparison.Ordinal))
                {
                    cursorAdjustment = -1;
                }
                else if (replacementText.EndsWith("'", StringComparison.Ordinal) || replacementText.EndsWith("\"", StringComparison.Ordinal))
                {
                    var len = replacementText.Length;
                    replacementText = replacementText.Substring(0, len - 1) + '\\' + replacementText[len - 1];
                    cursorAdjustment = -1;
                }
                else
                {
                    replacementText = replacementText + '\\';
                }
            }
            return replacementText;
        }

        private static void InvertSelectedCompletion(CHAR_INFO[] buffer, int selectedItem, int menuColumnWidth, int menuRows)
        {
            var selectedX = selectedItem / menuRows;
            var selectedY = selectedItem - (selectedX * menuRows);
            var start = selectedY * _singleton._console.BufferWidth + selectedX * menuColumnWidth;
            for (int i = 0; i < menuColumnWidth; i++)
            {
                int j = i + start;
                buffer[j].ForegroundColor = (ConsoleColor)((int)buffer[j].ForegroundColor ^ 7);
                buffer[j].BackgroundColor = (ConsoleColor)((int)buffer[j].BackgroundColor ^ 7);
            }
        }

        /// <summary>
        /// Display the list of possible completions.
        /// </summary>
        public static void PossibleCompletions(ConsoleKeyInfo? key = null, object arg = null)
        {
            var completions = _singleton.GetCompletions();
            _singleton.PossibleCompletionsImpl(completions, menuSelect: false);
        }

        private static string HandleNewlinesForPossibleCompletions(string s)
        {
            s = s.Trim();
            var newlineIndex = s.IndexOfAny(new []{'\r', '\n'});
            if (newlineIndex >= 0)
            {
                s = s.Substring(0, newlineIndex) + "...";
            }
            return s;
        }

        private static CHAR_INFO[] CreateCompletionMenu(System.Collections.ObjectModel.Collection<CompletionResult> matches, IConsole console, bool showToolTips, out int MenuColumnWidth, out int DisplayRows)
        {
            var minColWidth = matches.Max(c => c.ListItemText.Length);
            minColWidth += 2;
            var bufferWidth = console.BufferWidth;
            ConsoleBufferBuilder cb;
            if (showToolTips)
            {
                const string seperator = "- ";
                var maxTooltipWidth = bufferWidth - minColWidth - seperator.Length;

                DisplayRows = matches.Count;
                cb = new ConsoleBufferBuilder(DisplayRows * bufferWidth, console);
                for (int index = 0; index < matches.Count; index++)
                {
                    var match = matches[index];
                    var listItemText = HandleNewlinesForPossibleCompletions(match.ListItemText);
                    cb.Append(listItemText);
                    var spacesNeeded = minColWidth - listItemText.Length;
                    if (spacesNeeded > 0)
                    {
                        cb.Append(' ', spacesNeeded);
                    }
                    cb.Append(seperator);
                    var toolTip = HandleNewlinesForPossibleCompletions(match.ToolTip);
                    toolTip = toolTip.Length <= maxTooltipWidth
                                  ? toolTip
                                  : toolTip.Substring(0, maxTooltipWidth);
                    cb.Append(toolTip);

                    // Make sure we always write out exactly 1 buffer width
                    spacesNeeded = (bufferWidth * (index + 1)) - cb.Length;
                    if (spacesNeeded > 0)
                    {
                        cb.Append(' ', spacesNeeded);
                    }
                }
                MenuColumnWidth = bufferWidth;
            }
            else
            {
                var screenColumns = bufferWidth;
                var displayColumns = Math.Max(1, screenColumns / minColWidth);
                DisplayRows = (matches.Count + displayColumns - 1) / displayColumns;
                cb = new ConsoleBufferBuilder(DisplayRows * bufferWidth, console);
                for (var row = 0; row < DisplayRows; row++)
                {
                    for (var col = 0; col < displayColumns; col++)
                    {
                        var index = row + (DisplayRows * col);
                        if (index >= matches.Count)
                            break;
                        var match = matches[index];
                        var item = HandleNewlinesForPossibleCompletions(match.ListItemText);
                        cb.Append(item);
                        cb.Append(' ', minColWidth - item.Length);
                    }

                    // Make sure we always write out exactly 1 buffer width
                    var spacesNeeded = (bufferWidth * (row + 1)) - cb.Length;
                    if (spacesNeeded > 0)
                    {
                        cb.Append(' ', spacesNeeded);
                    }
                }
                MenuColumnWidth = minColWidth;
            }

            return cb.ToArray();
        }

        private void PossibleCompletionsImpl(CommandCompletion completions, bool menuSelect)
        {
            if (completions == null || completions.CompletionMatches.Count == 0)
            {
                Ding();
                return;
            }

            if (completions.CompletionMatches.Count >= _options.CompletionQueryItems)
            {
                if (!PromptYesOrNo(string.Format(CultureInfo.CurrentCulture, PSReadLineResources.DisplayAllPossibilities, completions.CompletionMatches.Count)))
                {
                    return;
                }
            }

            var matches = completions.CompletionMatches;

            int menuColumnWidth;
            int displayRows;
            var menuBuffer = CreateCompletionMenu(matches, _console, Options.ShowToolTips, out menuColumnWidth, out displayRows);

            if (menuSelect)
            {
                // Make sure the menu and line can appear on the screen at the same time,
                // if not, we'll skip the menu.

                var endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                var bufferLines = endBufferCoords.Y - _initialY + 1;
                if ((bufferLines + displayRows) > _console.WindowHeight)
                {
                    menuSelect = false;
                }
            }

            if (menuSelect)
            {
                RemoveEditsAfterUndo();
                var undoPoint = _edits.Count;

                int selectedItem = 0;
                bool undo = false;

                int savedUserMark = _singleton._mark;
                _visualSelectionCommandCount += 1;

                string userCompletionText = _buffer.ToString().Substring(completions.ReplacementIndex, _current - completions.ReplacementIndex);
                DoReplacementForCompletion(matches[0], completions);

                // Recompute end of buffer coordinates as the replacement could have
                // added a line.
                var endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                var menuAreaTop = endBufferCoords.Y + 1;
                var previousMenuTop = menuAreaTop;

                InvertSelectedCompletion(menuBuffer, selectedItem, menuColumnWidth, displayRows);
                _console.WriteBufferLines(menuBuffer, ref menuAreaTop);

                // Showing the menu may have scrolled the screen or moved the cursor, update initialY to reflect that.
                _initialY -= (previousMenuTop - menuAreaTop);
                PlaceCursor();

                int previousItem = selectedItem;

                bool processingKeys = true;
                while (processingKeys)
                {
                    previousMenuTop = menuAreaTop;
                    SetMark(); // set mark to cursor
                    int curPos = matches[selectedItem].CompletionText.IndexOf(userCompletionText, StringComparison.OrdinalIgnoreCase);
                    // set cursor to the end of UserCompletion but in real completion (because of .\ and so on)
                    _current = completions.ReplacementIndex + curPos + userCompletionText.Length;
                    Render();

                    var nextKey = ReadKey();
                    if (nextKey == Keys.Home)
                    {
                        selectedItem = 0;
                    }
                    else if (nextKey == Keys.End)
                    {
                        selectedItem = matches.Count - 1;
                    }
                    else if (nextKey == Keys.RightArrow)
                    {
                        selectedItem = Math.Min(selectedItem + displayRows, matches.Count - 1);
                    }
                    else if (nextKey == Keys.LeftArrow)
                    {
                        selectedItem = Math.Max(selectedItem - displayRows, 0);
                    }
                    else if (nextKey == Keys.DownArrow)
                    {
                        selectedItem = Math.Min(selectedItem + 1, matches.Count - 1);
                    }
                    else if (nextKey == Keys.UpArrow)
                    {
                        selectedItem = Math.Max(selectedItem - 1, 0);
                    }
                    else if (nextKey == Keys.PageDown)
                    {
                        selectedItem = Math.Min(selectedItem + displayRows - (selectedItem % displayRows) - 1, matches.Count - 1);
                    }
                    else if (nextKey == Keys.PageUp)
                    {
                        selectedItem = Math.Max(selectedItem - (selectedItem % displayRows), 0);
                    }
                    else if (nextKey == Keys.Tab)
                    {
                        selectedItem = (selectedItem + 1) % matches.Count;
                    }
                    else if (nextKey == Keys.ShiftTab)
                    {
                        selectedItem = (selectedItem - 1) % matches.Count;
                        if (selectedItem < 0)
                        {
                            selectedItem += matches.Count;
                        }
                    }
                    else if (nextKey == Keys.CtrlG || nextKey == Keys.Escape)
                    {
                        undo = true;
                        processingKeys = false;
                        _visualSelectionCommandCount = 0;
                        _singleton._mark = savedUserMark;
                    }
                    else if (nextKey == Keys.Backspace)
                    {
                        // Esc alternative:
                        // stay string beginning as in current replacement but don't replace to full string
                        CompletionResult r = new CompletionResult(matches[selectedItem].CompletionText.Substring(0, _current - completions.ReplacementIndex));
                        DoReplacementForCompletion(r, completions);
                        processingKeys = false;
                        _visualSelectionCommandCount = 0;
                        _singleton._mark = savedUserMark;
                        Render();
                    }
                    else if (nextKey == Keys.Space || nextKey == Keys.Enter)
                    {
                        ExchangePointAndMark();
                        processingKeys = false;
                        _singleton._mark = savedUserMark;
                        _visualSelectionCommandCount = 0;
                        if (nextKey == Keys.Space) {
                            int cursorAdjustment = 0;
                            if (matches[selectedItem].ResultType == CompletionResultType.ProviderContainer)
                                userCompletionText = GetReplacementTextForDirectory(matches[selectedItem].CompletionText, ref cursorAdjustment);
                            _current -= cursorAdjustment;
                            PrependQueuedKeys(nextKey);
                        }
                        else
                            Render();
                    }
                    else if (nextKey.KeyChar != 0)
                    {
                        userCompletionText += nextKey.KeyChar;
                        string replacementText = matches[selectedItem].CompletionText;
                        // filter out matches and redraw menu
                        WriteBlankLines(displayRows, menuAreaTop);
                        matches = new System.Collections.ObjectModel.Collection<CompletionResult>();

                        foreach (CompletionResult item in completions.CompletionMatches)
                        {
                            if (item.ListItemText.StartsWith(userCompletionText, StringComparison.OrdinalIgnoreCase) ||
                                GetUnquotedText(item.CompletionText, false).StartsWith(userCompletionText, StringComparison.OrdinalIgnoreCase)
                               )
                               matches.Add(item);
                        }
                        if (matches.Count > 0)
                        {
                            menuBuffer = CreateCompletionMenu(matches, _console, Options.ShowToolTips, out menuColumnWidth, out displayRows);
                            previousItem = selectedItem = 0;
                            InvertSelectedCompletion(menuBuffer, previousItem, menuColumnWidth, displayRows);
                            _console.WriteBufferLines(menuBuffer, ref menuAreaTop);
                            DoReplacementForCompletion(matches[0], completions);
                        }
                        else
                        {
                            CompletionResult r = new CompletionResult(replacementText.Substring(0, _current - completions.ReplacementIndex)+nextKey.KeyChar);
                            DoReplacementForCompletion(r, completions);
                            _singleton._mark = savedUserMark;
                            _visualSelectionCommandCount = 0;
                            processingKeys = false;
                        }
                    }
                    else
                    {
                        Ding();
                    }

                    if (selectedItem != previousItem)
                    {
                        DoReplacementForCompletion(matches[selectedItem], completions);

                        endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                        menuAreaTop = endBufferCoords.Y + 1;

                        InvertSelectedCompletion(menuBuffer, previousItem, menuColumnWidth, displayRows);
                        InvertSelectedCompletion(menuBuffer, selectedItem, menuColumnWidth, displayRows);
                        _console.WriteBufferLines(menuBuffer, ref menuAreaTop);
                        previousItem = selectedItem;

                        if (previousMenuTop > menuAreaTop)
                        {
                            WriteBlankLines(previousMenuTop - menuAreaTop, menuAreaTop + displayRows);
                        }
                    }
                }

                WriteBlankLines(displayRows, menuAreaTop);

                var lastInsert = ((GroupedEdit)_edits[_edits.Count - 1])._groupedEditItems[1];
                Debug.Assert(lastInsert is EditItemInsertString, "The only edits possible here are pairs of Delete/Insert");
                var firstDelete = ((GroupedEdit)_edits[undoPoint])._groupedEditItems[0];
                Debug.Assert(firstDelete is EditItemDelete, "The only edits possible here are pairs of Delete/Insert");

                var groupEditCount = _edits.Count - undoPoint;
                _edits.RemoveRange(undoPoint, groupEditCount);
                _undoEditIndex = undoPoint;

                if (undo)
                {
                    // Pretend it never happened.
                    lastInsert.Undo();
                    firstDelete.Undo();
                    Render();
                }
                else
                {
                    // Leave one edit instead of possibly many to undo
                    SaveEditItem(GroupedEdit.Create(new List<EditItem> { firstDelete, lastInsert }));
                }
            }
            else
            {
                var endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                var menuAreaTop = endBufferCoords.Y + 1;

                _console.WriteBufferLines(menuBuffer, ref menuAreaTop);
                _initialY = menuAreaTop + displayRows;
                Render();
            }
        }
    }
}
