﻿using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using PSConsoleUtilities.Internal;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        // Tab completion state
        private int _tabCommandCount;
        private CommandCompletion _tabCompletions;
        private Runspace _remoteRunspace;

        // Stub helper method so completion can be mocked
        [ExcludeFromCodeCoverage]
        CommandCompletion IPSConsoleReadLineMockableMethods.CompleteInput(string input, int cursorIndex, Hashtable options, PowerShell powershell)
        {
            return CommandCompletion.CompleteInput(input, cursorIndex, options, powershell);
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
            var completions = _singleton.GetCompletions();
            if (completions == null || completions.CompletionMatches.Count == 0)
                return;

            if (_singleton._tabCommandCount > 0)
            {
                if (completions.CompletionMatches.Count == 1)
                {
                    Ding();
                }
                else
                {
                    _singleton.PossibleCompletionsImpl(null, null, menuSelect: true);
                }
                return;
            }

            if (completions.CompletionMatches.Count == 1)
            {
                // We want to add a backslash for directory completion if possible.  This
                // is mostly only needed if we have a single completion - if there are multiple
                // completions, then we'll be showing the possible completions where it's very
                // unlikely that we would add a trailing backslash.

                _singleton.DoReplacementForCompletion(completions.CompletionMatches[0], completions);
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
                _singleton.PossibleCompletionsImpl(null, null, menuSelect: true);
            }

            _singleton._tabCommandCount += 1;
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
                    PowerShell ps;
                    if (_remoteRunspace == null)
                    {
                        ps = PowerShell.Create(RunspaceMode.CurrentRunspace);
                    }
                    else
                    {
                        ps = PowerShell.Create();
                        ps.Runspace = _remoteRunspace;
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
            if (!replacementText.EndsWith("\\"))
            {
                if (replacementText.EndsWith("\\'") || replacementText.EndsWith("\\\""))
                {
                    cursorAdjustment = -1;
                }
                else if (replacementText.EndsWith("'") || replacementText.EndsWith("\""))
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
            var start = selectedY * Console.BufferWidth + selectedX * menuColumnWidth;
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
            _singleton.PossibleCompletionsImpl(key, arg, menuSelect: false);
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

        private void PossibleCompletionsImpl(ConsoleKeyInfo? key, object arg, bool menuSelect)
        {
            var completions = GetCompletions();
            if (completions == null || completions.CompletionMatches.Count == 0)
            {
                Ding();
                return;
            }

            if (completions.CompletionMatches.Count >= _options.CompletionQueryItems)
            {
                if (!PromptYesOrNo(string.Format(PSReadLineResources.DisplayAllPossibilities, completions.CompletionMatches.Count)))
                {
                    return;
                }
            }

            var matches = completions.CompletionMatches;
            var minColWidth = matches.Max(c => c.ListItemText.Length);
            minColWidth += 2;
            var menuColumnWidth = minColWidth;

            int displayRows;
            var bufferWidth = Console.BufferWidth;
            ConsoleBufferBuilder cb;
            if (Options.ShowToolTips)
            {
                const string seperator = "- ";
                var maxTooltipWidth = bufferWidth - minColWidth - seperator.Length;

                displayRows = matches.Count;
                cb = new ConsoleBufferBuilder(displayRows * bufferWidth);
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
                menuColumnWidth = bufferWidth;
            }
            else
            {
                var screenColumns = bufferWidth;
                var displayColumns = Math.Max(1, screenColumns / minColWidth);
                displayRows = (completions.CompletionMatches.Count + displayColumns - 1) / displayColumns;
                cb = new ConsoleBufferBuilder(displayRows * bufferWidth);
                for (var row = 0; row < displayRows; row++)
                {
                    for (var col = 0; col < displayColumns; col++)
                    {
                        var index = row + (displayRows * col);
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
            }

            var menuBuffer = cb.ToArray();

            if (menuSelect)
            {
                // Make sure the menu and line can appear on the screen at the same time,
                // if not, we'll skip the menu.

                var endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                var bufferLines = endBufferCoords.Y - _initialY + 1;
                if ((bufferLines + displayRows) > Console.WindowHeight)
                {
                    menuSelect = false;
                }
            }

            if (menuSelect)
            {
                StartEditGroup();

                int selectedItem = 0;
                bool undo = false;

                DoReplacementForCompletion(matches[0], completions);

                // Recompute end of buffer coordinates as the replacement could have
                // added a line.
                var endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                var menuAreaTop = endBufferCoords.Y + 1;
                var previousMenuTop = menuAreaTop;

                InvertSelectedCompletion(menuBuffer, selectedItem, menuColumnWidth, displayRows);
                WriteBufferLines(menuBuffer, ref menuAreaTop);

                if (previousMenuTop != menuAreaTop)
                {
                    // Showing the menu scrolled the screen, update initialY to reflect that.
                    _initialY -= (previousMenuTop - menuAreaTop);
                    PlaceCursor();
                    previousMenuTop = menuAreaTop;
                }
                int previousItem = selectedItem;

                bool processingKeys = true;
                while (processingKeys)
                {
                    var nextKey = ReadKey();
                    if (nextKey == Keys.RightArrow)
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
                    }
                    else
                    {
                        PrependQueuedKeys(nextKey);
                        processingKeys = false;
                    }

                    if (selectedItem != previousItem)
                    {
                        DoReplacementForCompletion(matches[selectedItem], completions);

                        endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                        menuAreaTop = endBufferCoords.Y + 1;

                        InvertSelectedCompletion(menuBuffer, previousItem, menuColumnWidth, displayRows);
                        InvertSelectedCompletion(menuBuffer, selectedItem, menuColumnWidth, displayRows);
                        WriteBufferLines(menuBuffer, ref menuAreaTop);
                        previousItem = selectedItem;

                        if (previousMenuTop > menuAreaTop)
                        {
                            WriteBlankLines(previousMenuTop - menuAreaTop, menuAreaTop + displayRows);
                        }
                    }
                }

                WriteBlankLines(displayRows, menuAreaTop);

                EndEditGroup();

                if (undo)
                {
                    // Pretend it never happened.
                    Undo();
                }
            }
            else
            {
                var endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                var menuAreaTop = endBufferCoords.Y + 1;

                WriteBufferLines(menuBuffer, ref menuAreaTop);
                _initialY = menuAreaTop + displayRows;
                Render();
            }
        }
    }
}
