/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        // Tab completion state
        private int _tabCommandCount;
        private CommandCompletion _tabCompletions;
        private Runspace _runspace;
        private string _directorySeparator;

        private static readonly Dictionary<CompletionResultType, PSKeyInfo []> KeysEndingCompletion =
            new Dictionary<CompletionResultType, PSKeyInfo []>
        {
            { CompletionResultType.Variable,          new[] { Keys.Period } },
            { CompletionResultType.Namespace,         new[] { Keys.Period } },
            { CompletionResultType.Property,          new[] { Keys.Period } },
            { CompletionResultType.ProviderContainer, new[] { Keys.Backslash, Keys.Slash } },
            { CompletionResultType.Method,            new[] { Keys.LParen, Keys.RParen } },
            { CompletionResultType.Type,              new[] { Keys.RBracket } },
            { CompletionResultType.ParameterName,     new[] { Keys.Colon } },
            { CompletionResultType.ParameterValue,    new[] { Keys.Comma } },
        };

        private static readonly char[] EolChars = {'\r', '\n'};

        // String helper for directory paths
        private static readonly string DefaultDirectorySeparator = System.IO.Path.DirectorySeparatorChar.ToString();

        // Stub helper method so completion can be mocked
        [ExcludeFromCodeCoverage]
        CommandCompletion IPSConsoleReadLineMockableMethods.CompleteInput(string input, int cursorIndex, Hashtable options, System.Management.Automation.PowerShell powershell)
        {
            return CallPossibleExternalApplication(
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

        private static bool IsSingleQuote(char c) => c == '\'' || c == (char)8216 || c == (char)8217 || c == (char)8218 || c == (char)8219;
        private static bool IsDoubleQuote(char c) => c == '"' || c == (char)8220 || c == (char)8221 || c == (char)8222;

        // variable can be "quoted" like ${env:CommonProgramFiles(x86)}
        private static bool IsQuotedVariable(string s) => s.Length > 2 && s[1] == '{' && s[s.Length - 1] == '}';

        private static bool IsQuoted(string s)
        {
            if (s.Length >= 2)
            {
                //consider possible '& ' prefix
                var first = (s.Length > 4 && s.StartsWith("& ")) ? s[2] : s[0];
                var last = s[s.Length - 1];

                return (IsSingleQuote(first) && IsSingleQuote(last)) ||
                       (IsDoubleQuote(first) && IsDoubleQuote(last));
            }
            return false;
        }

        private static string GetUnquotedText(string s, bool consistentQuoting)
        {
            if (!consistentQuoting && IsQuoted(s))
            {
                //consider possible '& ' prefix
                int startindex = s.StartsWith("& ") ? 3 : 1;
                s = s.Substring(startindex, s.Length - startindex - 1);
            }
            return s;
        }

        private static string GetUnquotedText(CompletionResult match, bool consistentQuoting)
        {
            var s = match.CompletionText;
            if (match.ResultType == CompletionResultType.Variable)
            {
                if (IsQuotedVariable(s))
                {
                    return s[0] + s.Substring(2, s.Length - 3);
                }
                return s;
            }
            return GetUnquotedText(s, consistentQuoting);
        }

        /// <summary>
        /// Attempt to perform completion on the text surrounding the cursor.
        /// If there are multiple possible completions, the longest unambiguous
        /// prefix is used for completion.  If trying to complete the longest
        /// unambiguous completion, a list of possible completions is displayed.
        /// </summary>
        public static void Complete(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.CompleteImpl(false);
        }

        /// <summary>
        /// Attempt to perform completion on the text surrounding the cursor.
        /// If there are multiple possible completions, the longest unambiguous
        /// prefix is used for completion.  If trying to complete the longest
        /// unambiguous completion, a list of possible completions is displayed.
        /// </summary>
        public static void MenuComplete(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.CompleteImpl(true);
        }

        private bool IsConsistentQuoting(Collection<CompletionResult> matches)
        {
            int quotedCompletions = matches.Count(match => IsQuoted(match.CompletionText));
            return
                quotedCompletions == 0 ||
                (quotedCompletions == matches.Count &&
                 quotedCompletions == matches.Count(
                    m => m.CompletionText[0] == matches[0].CompletionText[0]));
        }

        private string GetUnambiguousPrefix(Collection<CompletionResult> matches, out bool ambiguous)
        {
            // Find the longest unambiguous prefix.  This might be the empty
            // string, in which case we don't want to remove any of the users input,
            // instead we'll immediately show possible completions.
            // For the purposes of unambiguous prefix, we'll ignore quotes if
            // some completions aren't quoted.
            ambiguous = false;
            var firstResult = matches[0];
            bool consistentQuoting = IsConsistentQuoting(matches);

            var replacementText = GetUnquotedText(firstResult, consistentQuoting);
            foreach (var match in matches.Skip(1))
            {
                var matchText = GetUnquotedText(match, consistentQuoting);
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
            if (replacementText.Length == 0)
            {
                replacementText = firstResult.ListItemText;
                foreach (var match in matches.Skip(1))
                {
                    var matchText = match.ListItemText;
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
            }
            return replacementText;
        }

        private void CompleteImpl(bool menuSelect)
        {
            if (InViInsertMode())   // must close out the current edit group before engaging menu completion
            {
                ViCommandMode();
                ViInsertWithAppendImpl();
            }

            // Do not show suggestion text during tab completion.
            using var _ = _prediction.DisableScoped();

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

            var replacementText = GetUnambiguousPrefix(completions.CompletionMatches, out var ambiguous);

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
                        _directorySeparator ??= DefaultDirectorySeparator;
                        ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                    }
                    else
                    {
                        if (_directorySeparator is null)
                        {
                            // Use the default separator by default.
                            _directorySeparator = DefaultDirectorySeparator;
                            PSPrimitiveDictionary dict = _runspace.GetApplicationPrivateData();

                            if (dict["PSVersionTable"] is PSPrimitiveDictionary versionTable)
                            {
                                // If the 'Platform' key is available and its value is not 'Win*', then the server side is macOS or Linux.
                                // In that case, we use the forward slash '/' as the directory separator.
                                // Otherwise, the server side is Windows and we use the backward slash '\' instead.
                                _directorySeparator = versionTable["Platform"] is string platform && !platform.StartsWith("Win", StringComparison.Ordinal) ? "/" : @"\";
                            }
                        }

                        ps = System.Management.Automation.PowerShell.Create();
                        ps.Runspace = _runspace;
                    }

                    _tabCompletions = _mockableMethods.CompleteInput(_buffer.ToString(), _current, null, ps);
                    if (_tabCompletions.CompletionMatches.Count == 0)
                    {
                        return null;
                    }

                    // Validate the replacement index/length - if we can't do
                    // the replacement, we'll ignore the completions.
                    var start = _tabCompletions.ReplacementIndex;
                    var length = _tabCompletions.ReplacementLength;

                    if (start < 0 || start > _singleton._buffer.Length)
                    {
                        return null;
                    }

                    if (length < 0 || length > (_singleton._buffer.Length - start))
                    {
                        return null;
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    // GetCompletions could scroll the screen, e.g. via Write-Progress. For example,
                    // cd <TAB> under the CloudShell Azure drive will show the progress bar while fetching data.
                    // We need to update the _initialY in case the current cursor postion has changed.
                    if (_singleton._initialY > _console.CursorTop)
                    {
                        _singleton._initialY = _console.CursorTop;
                    }
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

            // When we increment _tabCommandCount, we won't try getting new completions on the next
            // tab, instead we'll cycle through the possible completions.
            // If there was just one completion, there is nothing to cycle through, so skip the
            // increment to let the next tab fetch new completions. Commonly there won't be,
            // but in the case of directory completion, it is possible because we add the trailing
            // directory separator.
            if (completions.CompletionMatches.Count > 1)
            {
                _tabCommandCount += 1;
            }
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
                MoveCursor(_current + cursorAdjustment);
            }
            completions.ReplacementLength = replacementText.Length;
        }

        private string GetReplacementTextForDirectory(string replacementText, ref int cursorAdjustment)
        {
            if (!replacementText.EndsWith(_directorySeparator , StringComparison.Ordinal))
            {
                if (replacementText.EndsWith(string.Format("{0}\'", _directorySeparator), StringComparison.Ordinal) ||
                    replacementText.EndsWith(string.Format("{0}\"", _directorySeparator), StringComparison.Ordinal))
                {
                    cursorAdjustment = -1;
                }
                else if (replacementText.EndsWith("'", StringComparison.Ordinal) ||
                         replacementText.EndsWith("\"", StringComparison.Ordinal))
                {
                    var len = replacementText.Length;
                    replacementText = replacementText.Substring(0, len - 1) + _directorySeparator + replacementText[len - 1];
                    cursorAdjustment = -1;
                }
                else
                {
                    replacementText += _directorySeparator;
                }
            }
            return replacementText;
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
            var newlineIndex = s.IndexOfAny(EolChars);
            if (newlineIndex >= 0)
            {
                s = s.Substring(0, newlineIndex) + "...";
            }
            return s;
        }

        private class Menu : DisplayBlockBase
        {
            internal int PreviousTop;
            internal int ColumnWidth;
            internal int BufferLines;
            internal int Rows;
            internal int Columns;
            internal int ToolTipLines;

            internal Collection<CompletionResult> MenuItems;
            internal CompletionResult CurrentMenuItem => MenuItems[CurrentSelection];
            internal int CurrentSelection;

            public void DrawMenu(Menu previousMenu, bool menuSelect)
            {
                IConsole console = Singleton._console;

                if (menuSelect)
                {
                    console.CursorVisible = false;
                    SaveCursor();
                }

                PreviousTop = Top;
                MoveCursorToStartDrawingPosition(console);

                var bufferWidth = console.BufferWidth;
                var columnWidth = this.ColumnWidth;

                var items = this.MenuItems;
                for (var row = 0; row < this.Rows; row++)
                {
                    var cells = 0;
                    for (var col = 0; col < this.Columns; col++)
                    {
                        var index = row + (this.Rows * col);
                        if (index >= items.Count)
                        {
                            break;
                        }
                        console.Write(GetMenuItem(items[index].ListItemText, columnWidth));
                        cells += columnWidth;
                    }

                    // Make sure we always write out exactly 1 buffer width to erase anything
                    // from a previous menu.
                    if (cells < bufferWidth)
                    {
                        // 'BlankRestOfLine' erases rest of the current line, but the cursor is not moved.
                        console.BlankRestOfLine();
                    }

                    // Explicit newline so consoles see each row as distinct lines, but skip the
                    // last line so we don't scroll.
                    if (row != (this.Rows - 1) || !menuSelect) {
                        AdjustForPossibleScroll(1);
                        MoveCursorDown(1);
                    }
                }

                bool extraPreRowsCleared = false;
                if (previousMenu != null)
                {
                    if (Rows < previousMenu.Rows + previousMenu.ToolTipLines)
                    {
                        // If the last menu row took the whole buffer width, then the cursor could be pushed to the
                        // beginning of the next line in the legacy console host (NOT in modern terminals such as
                        // Windows Terminal, VSCode Terminal, or virtual-terminal-enabled console host). In such a
                        // case, there is no need to move the cursor to the next line.
                        //
                        // If that is not the case, namely 'CursorLeft != 0', then the rest of the last menu row was
                        // erased, but the cursor was not moved to the next line, so we will move the cursor.
                        if (console.CursorLeft != 0)
                        {
                            // There are lines from the previous rendering that need to be cleared,
                            // so we are sure there is no need to scroll.
                            MoveCursorDown(1);
                        }

                        Singleton.WriteBlankLines(previousMenu.Rows + previousMenu.ToolTipLines - Rows);
                        extraPreRowsCleared = true;
                    }
                }

                if (menuSelect)
                {
                    // if the menu has moved, we need to clear the lines under it
                    if (Top < PreviousTop)
                    {
                        // In either of the following two cases, we will need to move the cursor to the next line:
                        //  - if extra rows from previous menu were cleared, then we know the current line was erased
                        //    but the cursor was not moved to the next line.
                        //  - if 'CursorLeft != 0', then the rest of the last menu row was erased, but the cursor
                        //    was not moved to the next line.
                        if (extraPreRowsCleared || console.CursorLeft != 0)
                        {
                            // There are lines from the previous rendering that need to be cleared,
                            // so we are sure there is no need to scroll.
                            MoveCursorDown(1);
                        }

                        Singleton.WriteBlankLines(PreviousTop - Top);
                    }

                    RestoreCursor();
                    console.CursorVisible = true;
                }
            }

            public void Clear()
            {
                Singleton.WriteBlankLines(Top, Rows + ToolTipLines);
            }

            public void UpdateMenuSelection(int selectedItem, bool select, bool showTooltips, string toolTipColor)
            {
                var console = Singleton._console;
                var menuItem = MenuItems[selectedItem];
                var listItem = menuItem.ListItemText;

                string toolTip = null;
                if (showTooltips)
                {
                    toolTip = menuItem.ToolTip.Trim();

                    // Don't bother showing the tooltip if it doesn't add information.
                    showTooltips = !string.IsNullOrWhiteSpace(toolTip)
                        && !string.Equals(toolTip, listItem, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(toolTip, menuItem.CompletionText, StringComparison.OrdinalIgnoreCase);
                }

                // We'll use one blank line to set the tooltip apart from the menu,
                // and there will be at least 1 line in the tooltip, possibly more.
                var toolTipLines = 2;
                if (showTooltips)
                {
                    // Determine if showing the tooltip would scroll the top of our buffer off the screen.

                    int lineLength = 0;
                    bool fullLine = false;
                    for (var i = 0; i < toolTip.Length; i++)
                    {
                        char c = toolTip[i];
                        if (c == '\r' && i < toolTip.Length && toolTip[i+1] == '\n')
                        {
                            // Skip the newline, but handle LF, CRLF, and CR.
                            i += 1;
                        }

                        if (c == '\r' || c == '\n')
                        {
                            // If we happened to have a full line right before the newline character, then we
                            // skip this newline character because we already increased the line count.
                            if (!fullLine)
                            {
                                toolTipLines += 1;
                                lineLength = 0;
                            }
                        }
                        else
                        {
                            lineLength += 1;
                            if (lineLength == console.BufferWidth)
                            {
                                toolTipLines += 1;
                                lineLength = 0;

                                // Indicate that we just had a full line.
                                fullLine = true;
                                continue;
                            }
                        }

                        fullLine = false;
                    }

                    // The +1 is for the blank line between the menu and tooltips.
                    if (BufferLines + Rows + toolTipLines + 1 > console.WindowHeight)
                    {
                        showTooltips = false;
                    }
                }

                SaveCursor();

                var row = Top + selectedItem % Rows;
                var col = ColumnWidth * (selectedItem / Rows);

                console.SetCursorPosition(col, row);

                if (select) console.Write(Singleton.Options._selectionColor);
                console.Write(GetMenuItem(listItem, ColumnWidth));
                if (select) console.Write(VTColorUtils.AnsiReset);

                ToolTipLines = 0;
                if (showTooltips)
                {
                    Debug.Assert(select, "On unselect, caller must clear the tooltip");
                    console.SetCursorPosition(0, Top + Rows - 1);
                    // Move down 2 so we have 1 blank line between the menu and buffer.
                    AdjustForPossibleScroll(toolTipLines);
                    MoveCursorDown(2);
                    console.Write(toolTipColor);
                    console.Write(toolTip);
                    ToolTipLines = toolTipLines;

                    console.Write(VTColorUtils.AnsiReset);
                }

                RestoreCursor();
            }

            public void MoveRight()
            {
                int nextInSameRow = CurrentSelection + Rows;
                if (nextInSameRow <= MenuItems.Count - 1)
                {
                    CurrentSelection = nextInSameRow;
                    return;
                }

                // Index of the column where 'CurrentSelection' is at, assuming columns start from left at index 0.
                int columnIndex = CurrentSelection / Rows;
                int leftmostItemInSameRow = CurrentSelection - columnIndex * Rows;

                // Index of the row where 'leftMostItemAtSameRow' is at, assuming rows start from top at index 0.
                int rowIndex = leftmostItemInSameRow % Rows;

                // If 'rowIndex == Rows - 1', then 'CurrentSelection' is at the rightmost position in the last row,
                // so moving-to-right again should move to the item at index 0.
                CurrentSelection = rowIndex == Rows - 1 ? 0 : leftmostItemInSameRow + 1;
            }

            public void MoveLeft()
            {
                int previousInSameRow = CurrentSelection - Rows;
                if (previousInSameRow >= 0)
                {
                    CurrentSelection = previousInSameRow;
                    return;
                }

                // Index of the row where 'CurrentSelection' is at, assuming rows start from top at index 0.
                int rowIndex = CurrentSelection % Rows;
                int leftmostItemInPreviousRow = rowIndex == 0 ? Rows - 1 : rowIndex - 1;

                int lastItemIndex = MenuItems.Count - 1;
                // Index of the column where the last item is at, assuming columns start from left at index 0.
                int lastItemColumnIndex = lastItemIndex / Rows;

                // Get the rightmost item in the previous row.
                CurrentSelection = leftmostItemInPreviousRow + lastItemColumnIndex * Rows;
                if (CurrentSelection > lastItemIndex)
                {
                    CurrentSelection = leftmostItemInPreviousRow + (lastItemColumnIndex - 1) * Rows;
                }
            }

            public void MoveUp()
            {
                CurrentSelection = CurrentSelection > 0 ? CurrentSelection - 1 : MenuItems.Count - 1;
            }

            public void MoveDown()
            {
                CurrentSelection = CurrentSelection < (MenuItems.Count - 1) ? CurrentSelection + 1 : 0;
            }

            public void MovePageDown() => CurrentSelection = Math.Min(CurrentSelection + Rows - (CurrentSelection % Rows) - 1,
                                                                      MenuItems.Count - 1);
            public void MovePageUp()   => CurrentSelection = Math.Max(CurrentSelection - (CurrentSelection % Rows), 0);

            public void MoveN(int n)
            {
                CurrentSelection = (CurrentSelection + n) % MenuItems.Count;
                if (CurrentSelection < 0)
                {
                    CurrentSelection += MenuItems.Count;
                }
            }
        }

        private Menu CreateCompletionMenu(Collection<CompletionResult> matches)
        {
            var bufferWidth = _console.BufferWidth;
            var colWidth = Math.Min(matches.Max(c => LengthInBufferCells(c.ListItemText)) + 2, bufferWidth);
            var columns = Math.Max(1, bufferWidth / colWidth);

            return new Menu
            {
                Singleton = this,
                ColumnWidth = colWidth,
                Columns = columns,
                Rows = (matches.Count + columns - 1) / columns,
                MenuItems = matches,
            };
        }

        private Collection<CompletionResult> FilterCompletions(CommandCompletion completion, string completionFilter)
        {
            var newMatches = new Collection<CompletionResult>();
            var matches = completion.CompletionMatches;

            bool consistentQuoting = IsConsistentQuoting(matches);
            // add possible first quote to userCompletionText
            if (consistentQuoting)
            {
                var quote = matches[0].CompletionText[0];
                if (IsSingleQuote(quote) || IsDoubleQuote(quote))
                {
                    completionFilter = quote + completionFilter;
                }
            }

            foreach (CompletionResult item in matches)
            {
                if (item.ListItemText.StartsWith(completionFilter, StringComparison.OrdinalIgnoreCase) ||
                    GetUnquotedText(item, consistentQuoting).StartsWith(completionFilter, StringComparison.OrdinalIgnoreCase))
                {
                    newMatches.Add(item);
                }
            }

            return newMatches;
        }

        private int FindUserCompletionTextPosition(CompletionResult match, string userCompletionText)
        {
            return match.ResultType == CompletionResultType.Variable && userCompletionText.Length > 1 && match.CompletionText[1] == '{'
                ? match.CompletionText.IndexOf(userCompletionText.Substring(1), StringComparison.OrdinalIgnoreCase) - 1
                : match.CompletionText.IndexOf(userCompletionText, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDoneWithCompletions(CompletionResult currentCompletion, PSKeyInfo nextKey)
        {
            return (nextKey == Keys.Space && ! currentCompletion.CompletionText.Contains(' '))
                || nextKey == Keys.Enter
                || KeysEndingCompletion.TryGetValue(currentCompletion.ResultType, out var doneKeys)
                   && doneKeys.Contains(nextKey);
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

            var menu = CreateCompletionMenu(completions.CompletionMatches);

            if (menuSelect)
            {
                // Make sure the menu and line can appear on the screen at the same time,
                // if not, we'll skip the menu.

                var endBufferPoint = ConvertOffsetToPoint(_buffer.Length);
                menu.BufferLines = endBufferPoint.Y - _initialY + 1 + _options.ExtraPromptLineCount;
                if (menu.BufferLines + menu.Rows > _console.WindowHeight)
                {
                    menuSelect = false;
                }
            }

            if (menuSelect)
            {
                MenuCompleteImpl(menu, completions);
            }
            else
            {
                menu.DrawMenu(null, menuSelect: false);
                InvokePrompt(key: null, arg: _console.CursorTop);
            }
        }

        private static string GetMenuItem(string item, int columnWidth)
        {
            item = HandleNewlinesForPossibleCompletions(item);
            var spacesNeeded = columnWidth - LengthInBufferCells(item);
            if (spacesNeeded > 0)
            {
                item = item + Spaces(spacesNeeded);
            }
            else if (spacesNeeded < 0)
            {
                item = SubstringByCells(item, columnWidth - 3) + "...";
            }

            return item;
        }

        private void MenuCompleteImpl(Menu menu, CommandCompletion completions)
        {
            var menuStack = new Stack<Menu>(10);
            menuStack.Push(null);  // Used to help detect excess backspaces

            RemoveEditsAfterUndo();
            var undoPoint = _edits.Count;

            bool undo = false;

            int savedUserMark = _mark;
            _visualSelectionCommandCount++;

            // Get the unambiguous prefix, possibly removing the first quote
            var userCompletionText = GetUnambiguousPrefix(menu.MenuItems, out var ambiguous);
            if (userCompletionText.Length > 0)
            {
                var c = userCompletionText[0];
                if (IsSingleQuote(c) || IsDoubleQuote(c))
                {
                    userCompletionText = userCompletionText.Substring(1);
                }
            }

            var userInitialCompletionLength = userCompletionText.Length;

            bool processingKeys = true;
            int previousSelection = -1;

            while (processingKeys)
            {
                if (menu.CurrentSelection != previousSelection)
                {
                    var currentMenuItem = menu.CurrentMenuItem;
                    int curPos = FindUserCompletionTextPosition(currentMenuItem, userCompletionText);
                    if (userCompletionText.Length == 0 &&
                        (IsSingleQuote(currentMenuItem.CompletionText[0]) || IsDoubleQuote(currentMenuItem.CompletionText[0])))
                    {
                        curPos++;
                    }

                    // set mark to the end of UserCompletion but in real completion (because of .\ and so on)
                    _mark = completions.ReplacementIndex + curPos + userCompletionText.Length;
                    DoReplacementForCompletion(currentMenuItem, completions);

                    ExchangePointAndMark();

                    if (previousSelection == -1)
                    {
                        completions.CurrentMatchIndex = 0;
                        menu.DrawMenu(null, menuSelect: true);
                    }
                    else
                    {
                        // After replacement, the menu might be misplaced from the command line
                        // getting shorter or longer.
                        var endOfCommandLine = ConvertOffsetToPoint(_buffer.Length);
                        var topAdjustment = (endOfCommandLine.Y + 1) - menu.Top;
                        int oldInitialY = _initialY;

                        if (topAdjustment != 0)
                        {
                            menu.DrawMenu(null, menuSelect: true);
                        }
                        if (topAdjustment > 0)
                        {
                            // Render did not clear the rest of the command line which flowed
                            // into the menu, so we must do that here.
                            menu.SaveCursor();

                            if (oldInitialY > _initialY)
                            {
                                // Scrolling happened when drawing the menu, so we need to adjust
                                // this point as it was calculated before drawing the menu.
                                endOfCommandLine.Y -= oldInitialY - _initialY;
                            }

                            _console.SetCursorPosition(endOfCommandLine.X, endOfCommandLine.Y);
                            _console.Write(Spaces(_console.BufferWidth - endOfCommandLine.X));
                            menu.RestoreCursor();
                        }

                        if (menu.ToolTipLines > 0)
                        {
                            // Erase previous tooltip, taking into account if the menu moved up/down.
                            WriteBlankLines(menu.Top + menu.Rows, -topAdjustment + menu.ToolTipLines);
                        }

                        menu.UpdateMenuSelection(
                            previousSelection,
                            select: false,
                            showTooltips: false,
                            Options._emphasisColor);
                    }

                    menu.UpdateMenuSelection(
                        menu.CurrentSelection,
                        select: true,
                        Options.ShowToolTips,
                        Options._emphasisColor);

                    previousSelection = menu.CurrentSelection;
                }

                var nextKey = ReadKey();
                if (nextKey == Keys.RightArrow) { menu.MoveRight(); }
                else if (nextKey == Keys.LeftArrow) { menu.MoveLeft(); }
                else if (nextKey == Keys.DownArrow) { menu.MoveDown(); }
                else if (nextKey == Keys.UpArrow) { menu.MoveUp(); }
                else if (nextKey == Keys.PageDown) { menu.MovePageDown(); }
                else if (nextKey == Keys.PageUp) { menu.MovePageUp(); }
                else if (nextKey == Keys.Tab)
                {
                    // Search for possible unambiguous common prefix.
                    string unambiguousText = GetUnambiguousPrefix(menu.MenuItems, out ambiguous);
                    int userComplPos = unambiguousText.IndexOf(userCompletionText, StringComparison.OrdinalIgnoreCase);

                    // Obtain all the menu items beginning with unambigousText, so we can count them.
                    var unambiguousMenuItems = menu.MenuItems.Where(item =>
                        item.CompletionText
                            .Trim('\'') // handles comparisons with items that have spaces in them (these auto receive quote wraps)
                            .StartsWith(unambiguousText, StringComparison.OrdinalIgnoreCase)
                    );
                    int countUnambiguousItems = Enumerable.Count(unambiguousMenuItems);

                    // If there is only 1 item, autoaccept it
                    if (unambiguousText.Length > 0 && userComplPos >= 0 && countUnambiguousItems == 1 )
                    {
                        processingKeys = false;
                        int cursorAdjustment = 0;

                        var onlyCompletionResult = unambiguousMenuItems.First();
                        userCompletionText = onlyCompletionResult.CompletionText;
                        
                        _current = userCompletionText.Length;
                        // Append a slash if it's a filesystem container
                        DoReplacementForCompletion(onlyCompletionResult, completions);
                        _current -= cursorAdjustment;

                        // Autoaccepts the single available option (otherwise need to press rightarrow/tab a second time manually)
                        PrependQueuedKeys(Keys.RightArrow);
                    }
                    // For multiple items which are shorter length than the unambiguous text, autocomplete through the unambiguous text.
                    else if (unambiguousText.Length > 0 && userComplPos >= 0 &&
                        unambiguousText.Length > (userComplPos + userCompletionText.Length))
                    {
                        userCompletionText = unambiguousText.Substring(userComplPos);
                            _current = completions.ReplacementIndex +
                                       FindUserCompletionTextPosition(menu.MenuItems[menu.CurrentSelection], userCompletionText) +
                                       userCompletionText.Length;
                            Render();
                            Ding();
                    }
                    // For multiple items and only ambiguous text remaining, tab will cycle through the available choices.
                    else
                    {
                        menu.MoveN(1);
                    }
                }
                else if (nextKey == Keys.ShiftTab)
                {
                    menu.MoveN(-1);
                }
                else if (nextKey == Keys.CtrlG
                      || nextKey == Keys.Escape)
                {
                    undo = true;
                    processingKeys = false;
                    _visualSelectionCommandCount = 0;
                    _mark = savedUserMark;
                }
                else if (nextKey == Keys.Backspace)
                {
                    // TODO: Shift + Backspace does not fail here?
                    if (menuStack.Count > 1)
                    {
                        previousSelection = -1;
                        userCompletionText = userCompletionText.Substring(0, userCompletionText.Length - 1);

                        Menu newMenu = menuStack.Peek();
                        int pos = FindUserCompletionTextPosition(newMenu.CurrentMenuItem, userCompletionText);
                        if (pos >= 0)
                        {
                            newMenu = menuStack.Pop();
                            newMenu.DrawMenu(menu, menuSelect: true);

                            menu = newMenu;
                        }
                        // else {
                        //     We should not pop the stack yet. The updated user completion text contains characters
                        //     that are not included in the selected item of the menu at the top of stack. This may
                        //     happen when the user pressed a 'Tab' before this 'Backspace', which updated the user
                        //     completion text to include the unambiguous common prefix of the available completion
                        //     candidates. In this case, we should stay in the current menu.
                        // }
                    }
                    else if (menuStack.Count == 1)
                    {
                        Ding();

                        Debug.Assert(menuStack.Peek() == null, "sentinel value expected");
                        // Pop so the next backspace sends us to the else block and out of the loop.
                        menuStack.Pop();
                    }
                    else
                    {
                        processingKeys = false;
                        undo = true;
                        _visualSelectionCommandCount = 0;
                        _mark = savedUserMark;
                        PrependQueuedKeys(nextKey);
                    }
                }
                else
                {
                    bool prependNextKey = false;
                    int cursorAdjustment = 0;
                    bool truncateCurrentCompletion = false;
                    bool keepSelection = false;

                    var currentMenuItem = menu.CurrentMenuItem;
                    if (IsDoneWithCompletions(currentMenuItem, nextKey))
                    {
                        processingKeys = false;
                        ExchangePointAndMark(); // cursor to the end of Completion
                        if (nextKey != Keys.Enter)
                        {
                            if (currentMenuItem.ResultType == CompletionResultType.ProviderContainer)
                            {
                                userCompletionText = GetUnquotedText(
                                    GetReplacementTextForDirectory(currentMenuItem.CompletionText, ref cursorAdjustment),
                                                                   consistentQuoting: false);
                            }
                            else
                            {
                                userCompletionText = GetUnquotedText(currentMenuItem, consistentQuoting: false);
                            }

                            // do not append the same char as last char in CompletionText (works for '(', '\')
                            prependNextKey = userCompletionText[userCompletionText.Length - 1] != nextKey.KeyChar;
                        }
                    }
                    else if (nextKey.KeyChar > 0 && !char.IsControl(nextKey.KeyChar))
                    {
                        userCompletionText += nextKey.KeyChar;
                        // filter out matches and redraw menu
                        var newMatches = FilterCompletions(completions, userCompletionText);
                        if (newMatches.Count > 0)
                        {
                            var newMenu = CreateCompletionMenu(newMatches);

                            newMenu.DrawMenu(menu, menuSelect: true);
                            previousSelection = -1;

                            // Remember the current menu for when we see Backspace.
                            menu.ToolTipLines = 0;
                            if (menuStack.Count == 0)
                            {
                                // The user hit backspace before there were any items on the stack
                                // and we removed the sentinel - so put it back now.
                                menuStack.Push(null);
                            }

                            menuStack.Push(menu);
                            menu = newMenu;
                        }
                        else
                        {
                            processingKeys = false;
                            prependNextKey = true;
                            // we exit loop with current completion up to cursor
                            truncateCurrentCompletion = true;
                            if (userInitialCompletionLength == 0)
                            {
                                undo = true;
                            }
                        }
                    }
                    else // exit with any other Key chord
                    {
                        processingKeys = false;
                        prependNextKey = true;

                        // without this branch experience doesn't look naturally
                        if (_dispatchTable.TryGetValue(nextKey, out var handler) &&
                            (
                                handler.Action == CopyOrCancelLine ||
                                handler.Action == Cut ||
                                handler.Action == DeleteChar ||
                                handler.Action == Paste
                            )
                        )
                        {
                            keepSelection = true;
                        }
                    }

                    if (!processingKeys) // time to exit loop
                    {
                        if (truncateCurrentCompletion && !undo)
                        {
                            CompletionResult r = new CompletionResult(currentMenuItem
                                .CompletionText.Substring(0, _current - completions.ReplacementIndex));
                            DoReplacementForCompletion(r, completions);
                        }
                        if (keepSelection)
                        {
                            _visualSelectionCommandCount = 1;
                        }
                        else
                        {
                            _visualSelectionCommandCount = 0;
                            // if mark was set after cursor, it restored in uninspected position, because text before mark now longer
                            // should we correct it ? I think not, beause any other text insertion does not correct it
                            _mark = savedUserMark;
                        }
                        // without render all key chords that just move cursor leave selection visible, but it can be wrong
                        if (!undo && !keepSelection)
                        {
                            Render();
                        }
                        if (prependNextKey)
                        {
                            _current -= cursorAdjustment;
                            PrependQueuedKeys(nextKey);
                        }
                    }
                }
            }

            menu.Clear();

            var lastInsert = ((GroupedEdit) _edits[_edits.Count - 1])._groupedEditItems[1];
            Debug.Assert(lastInsert is EditItemInsertString, "The only edits possible here are pairs of Delete/Insert");
            var firstDelete = ((GroupedEdit) _edits[undoPoint])._groupedEditItems[0];
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
                SaveEditItem(GroupedEdit.Create(new List<EditItem> {firstDelete, lastInsert}));
            }
        }
    }
}
