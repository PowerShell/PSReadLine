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

        private static readonly Dictionary<CompletionResultType, ConsoleKeyInfo []> KeysEndingCompletion =
            new Dictionary<CompletionResultType, ConsoleKeyInfo []>
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

        private static bool IsSingleQuote(char c) => c == '\'' || c == (char)8216 || c == (char)8217 || c == (char)8218;
        private static bool IsDoubleQuote(char c) => c == '"' || c == (char)8220 || c == (char)8221;

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
                    return '$' + s.Substring(2, s.Length - 3);
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
                MoveCursor(_current + cursorAdjustment);
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

        private static string ShortenLongCompletions(string s, int maxLength)
        {
            if (s.Length <= maxLength) return s;
            // position of split point where ... inserted
            int splitPos = 10;
			// TODO: will crash for console width < splitPos + 3

            // TODO: is it needed ?
            // insert '.'
            //if (s.Length - maxLength <= 2)
            //    return s.Substring(0, maxLength - splitPos - 1) + '.' + s.Substring(s.Length - splitPos, splitPos);
            // insert '...'
            return s.Substring(0, maxLength - splitPos - 3) + "..." + s.Substring(s.Length - splitPos, splitPos);
        }

        private class Menu
        {
            internal int Top;
            internal int ColumnWidth;
            internal int Rows;
            internal int Columns;
            internal CommandCompletion Completions;
            internal Collection<CompletionResult> MenuItems => Completions.CompletionMatches;
            internal CompletionResult CurrentMenuItem => MenuItems[CurrentSelection];
            internal int CurrentSelection
            {
                get => Completions.CurrentMatchIndex;
                set => Completions.CurrentMatchIndex = value;
            }

            public void DrawMenu(PSConsoleReadLine singleton)
            {
                singleton._console.SaveCursor();

                // Move cursor to the start of the first line after our input.
                this.Top = singleton.ConvertOffsetToCoordinates(singleton._buffer.Length).Y + 1;
                singleton._console.SetCursorPosition(0, this.Top);

                var bufferWidth = singleton._console.BufferWidth;
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
                        singleton._console.Write(GetMenuItem(items[index].ListItemText, columnWidth));
                        cells += columnWidth;
                    }

                    // Make sure we always write out exactly 1 buffer width to erase anything
                    // from a previous menu.
                    if (cells < bufferWidth)
                    {
                        singleton._console.Write(Spaces(bufferWidth - cells));
                    }
                }

                singleton._console.RestoreCursor();
            }

            public void Clear(PSConsoleReadLine singleton)
            {
                var console = singleton._console;
                console.SaveCursor();

                console.SetCursorPosition(0, Top);
                for (int i = 0; i < Rows; i++)
                {
                    console.Write(Spaces(console.BufferWidth));
                }
                
                console.RestoreCursor();
            }

            public void UpdateMenuSelection(IConsole console, int selectedItem, bool select)
            {
                console.SaveCursor();

                var row = Top + selectedItem % Rows;
                var col = ColumnWidth * (selectedItem / Rows);

                console.SetCursorPosition(col, row);

                if (select) console.Write("\x001b[7m");
                console.Write(GetMenuItem(Completions.CompletionMatches[selectedItem].ListItemText, ColumnWidth));
                if (select) console.Write("\x001b[27m");

                console.RestoreCursor();
            }

            public void MoveRight()    => CurrentSelection = Math.Min(CurrentSelection + Rows, MenuItems.Count - 1);
            public void MoveLeft()     => CurrentSelection = Math.Max(CurrentSelection - Rows, 0);
            public void MoveUp()       => CurrentSelection = Math.Max(CurrentSelection - 1, 0);
            public void MoveDown()     => CurrentSelection = Math.Min(CurrentSelection + 1, MenuItems.Count - 1);
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

        private Menu CreateCompletionMenu(CommandCompletion completions)
        {
            var bufferWidth = _console.BufferWidth;
            var matches = completions.CompletionMatches;
            var colWidth = Math.Min(matches.Max(c => c.ListItemText.Length) + 2, bufferWidth);
            var columns = Math.Max(1, bufferWidth / colWidth);

            return new Menu
            {
                ColumnWidth = colWidth,
                Columns = columns,
                Rows = (matches.Count + columns - 1) / columns,
                Completions = completions,
            };
        }

        private Collection<CompletionResult> FilterCompletions(Collection<CompletionResult> matches, string completionFilter)
        {
            var result = new Collection<CompletionResult>();
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
                    GetUnquotedText(item, consistentQuoting).StartsWith(completionFilter, StringComparison.OrdinalIgnoreCase)
                   )
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private int FindUserCompletinTextPosition(CompletionResult match, string userCompletionText)
        {
            if (match.ResultType == CompletionResultType.Variable && match.CompletionText[1] == '{')
            {
                return match.CompletionText.IndexOf(userCompletionText.Substring(1), StringComparison.OrdinalIgnoreCase) - 1;
            }
            return match.CompletionText.IndexOf(userCompletionText, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDoneWithCompletions(CompletionResult currentCompletion, ConsoleKeyInfo nextKey)
        {
            if (nextKey == Keys.Space || nextKey == Keys.Enter) return true;
            if (KeysEndingCompletion.TryGetValue(currentCompletion.ResultType, out var doneKeys))
            {
                return doneKeys.Contains(nextKey);
            }
            return false;
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

            var menu = CreateCompletionMenu(completions);

            if (menuSelect)
            {
                // Make sure the menu and line can appear on the screen at the same time,
                // if not, we'll skip the menu.

                var endBufferCoords = ConvertOffsetToCoordinates(_buffer.Length);
                var bufferLines = endBufferCoords.Y - _initialY + 1;
                if ((bufferLines + menu.Rows) > _console.WindowHeight)
                {
                    menuSelect = false;
                }
            }

            if (menuSelect)
            {
                MenuCompleteImpl(menu);
            }
            else
            {
                menu.DrawMenu(this);
                InvokePrompt(key: null, arg: menu.Top + menu.Rows);
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

            return item;
        }

        private void MenuCompleteImpl(Menu menu)
        {
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

            var completions = menu.Completions;
            completions.CurrentMatchIndex = 0;
            menu.DrawMenu(this);

            DoReplacementForCompletion(completions.CompletionMatches[0], completions);

            menu.UpdateMenuSelection(_console, menu.CurrentSelection, select: true);

            bool processingKeys = true;
            int backspaceCounter = 0;
            int previousSelection = menu.CurrentSelection;

            while (processingKeys)
            {
                if (menu.CurrentSelection != previousSelection)
                {
                    var currentMenuItem = menu.CurrentMenuItem;
                    int curPos = FindUserCompletinTextPosition(currentMenuItem, userCompletionText);
                    if (userCompletionText.Length == 0 &&
                        (IsSingleQuote(currentMenuItem.CompletionText[0]) || IsDoubleQuote(currentMenuItem.CompletionText[0])))
                    {
                        curPos++;
                    }

                    // set mark to the end of UserCompletion but in real completion (because of .\ and so on)
                    _mark = completions.ReplacementIndex + curPos + userCompletionText.Length;
                    DoReplacementForCompletion(currentMenuItem, completions);

                    menu.UpdateMenuSelection(_console, previousSelection, select: false);
                    menu.UpdateMenuSelection(_console, menu.CurrentSelection, select: true);

                    previousSelection = menu.CurrentSelection;

                    /*

                    var blanklinesCount = previousMenuTop - menuAreaTop;
                    previousMenuTop = menuAreaTop;
                    _console.WriteBufferLines(menuBuffer, ref menuAreaTop);

                    // Showing the menu may have scrolled the screen or moved the cursor, update initialY to reflect that.
                    _initialY -= (previousMenuTop - menuAreaTop);
                    // return cursor in place even if screen was scrolled
                    ExchangePointAndMark();

                    if (previousItem != -1 && blanklinesCount > 0)
                    {
                        WriteBlankLines(blanklinesCount, menuAreaTop + displayRows);
                    }
                    previousMenuTop = menuAreaTop;
                    previousItem = selectedItem;
                    */
                }

                int cycleBackspaceCounter = backspaceCounter;

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
                    string unAmbiguousText = GetUnambiguousPrefix(menu.MenuItems, out ambiguous);
                    int userComplPos = unAmbiguousText.IndexOf(userCompletionText, StringComparison.OrdinalIgnoreCase);

                    // ... If found - advance IncrementalCompletion ...
                    if (unAmbiguousText.Length > 0 && userComplPos >= 0 &&
                        unAmbiguousText.Length > (userComplPos + userCompletionText.Length))
                    {
                        userCompletionText = unAmbiguousText.Substring(userComplPos);
                        _current = completions.ReplacementIndex +
                                   FindUserCompletinTextPosition(menu.MenuItems[menu.CurrentSelection], userCompletionText) +
                                   userCompletionText.Length;
                        Render();
                        Ding();
                    }
                    // ... if no - usual Tab behaviour
                    else
                    {
                        menu.MoveN(1);
                    }
                }
                else if (nextKey == Keys.ShiftTab)
                {
                    menu.MoveN(-1);
                }
                else if (nextKey == Keys.CtrlG || nextKey == Keys.Escape)
                {
                    undo = true;
                    processingKeys = false;
                    _visualSelectionCommandCount = 0;
                    _mark = savedUserMark;
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
                                userCompletionText =
                                    GetUnquotedText(
                                        GetReplacementTextForDirectory(currentMenuItem.CompletionText,
                                            ref cursorAdjustment), consistentQuoting: false);
                            else
                                userCompletionText = GetUnquotedText(currentMenuItem, consistentQuoting: false);

                            // do not append the same char as last char in CompletionText (works for for '(', '\')
                            prependNextKey = userCompletionText[userCompletionText.Length - 1] != nextKey.KeyChar;
                        }
                    }
                    else if (nextKey == Keys.Backspace ||
                             (nextKey.KeyChar > 0 && !char.IsControl(nextKey.KeyChar)))
                    {
                        // TODO: Shift + Backspace does not fall here ?
                        if (nextKey == Keys.Backspace)
                        {
                            if (userCompletionText.Length > userInitialCompletionLength)
                            {
                                userCompletionText = userCompletionText.Substring(0, userCompletionText.Length - 1);
                            }
                            else if (backspaceCounter == 0)
                            {
                                Ding();
                                backspaceCounter++;
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
                        else
                        {
                            userCompletionText += nextKey.KeyChar;
                        }
                        if (processingKeys && backspaceCounter == 0) // do not rebuild menu on backspace with Ding()
                        {
                            // filter out matches and redraw menu
                            var tmpMatches = FilterCompletions(completions.CompletionMatches, userCompletionText);
                            if (tmpMatches.Count > 0)
                            {
                                /*
                                WriteBlankLines(displayRows, menuAreaTop);
                                var selectedCompletionText = menu.CurrentMenuItem.CompletionText;
                                matches = tmpMatches;
                                previousItem = -1;
                                menuBuffer = CreateCompletionMenu(matches,
                                    selectedCompletionText, out selectedItem, out menuColumnWidth, out displayRows);
                                    */
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
                    }
                    else // exit with any other Key chord
                    {
                        processingKeys = false;
                        prependNextKey = true;

                        // without this branch experience doesnt look naturally
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

                if (backspaceCounter == cycleBackspaceCounter)
                    backspaceCounter = 0;
            }

            menu.Clear(this);

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
