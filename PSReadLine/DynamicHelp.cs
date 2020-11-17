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
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private Pager _pager;
        private static System.Management.Automation.PowerShell _ps;

        /// <summary>
        /// Attempt to show help content.
        /// Show the full help for the command on the alternate screen buffer.
        /// </summary>
        public static void DynamicHelpFullHelp(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.DynamicHelpImpl(isFullHelp: true);
        }

        /// <summary>
        /// Attempt to show help content.
        /// Show the short help of the parameter next to the cursor.
        /// </summary>
        public static void DynamicHelpParameter(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.DynamicHelpImpl(isFullHelp: false);
        }

        private void DynamicHelpImpl(bool isFullHelp)
        {
            _pager ??= new Pager();

            if (InViInsertMode())   // must close out the current edit group before engaging menu completion
            {
                ViCommandMode();
                ViInsertWithAppend();
            }

            int cursor = _singleton._current;
            string commandName = null;
            string parameterName = null;

            foreach(var token in _singleton._tokens)
            {
                if (token.TokenFlags == TokenFlags.CommandName)
                {
                    commandName = token.Extent.Text;
                }

                var extent = token.Extent;
                if (extent.StartOffset <= cursor && extent.EndOffset >= cursor)
                {
                    if (token.Kind == TokenKind.Parameter)
                    {
                        parameterName = ((ParameterToken)token).ParameterName;

                    }
                    // Possible corner case here when cursor is at the end
                }
            }

            if (!String.IsNullOrEmpty(commandName) && isFullHelp)
            {
                _ps ??= System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                _ps.Commands.Clear();

                var fullHelp = _ps
                    .AddCommand($"Microsoft.PowerShell.Core\\Get-Help")
                    .AddParameter("Name", commandName)
                    .AddParameter("Full", value: true)
                    .AddCommand($"Microsoft.PowerShell.Utility\\Out-String")
                    .Invoke<string>()
                    .FirstOrDefault();

                if (!String.Equals(fullHelp, String.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    string regexPatternToScrollTo = null;

                    if (!String.IsNullOrEmpty(parameterName))
                    {
                        string upper = parameterName[0].ToString().ToUpperInvariant();
                        string lower = parameterName[0].ToString().ToLowerInvariant();
                        string remainingString = parameterName.Substring(1);
                        regexPatternToScrollTo = $"-[{upper}|{lower}]{remainingString} [<|\\[]";
                    }

                    _pager.Write(fullHelp, regexPatternToScrollTo);
                }
            }
            else if (!String.IsNullOrEmpty(commandName) && !String.IsNullOrEmpty(parameterName))
            {
                _ps ??= System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
                _ps.Commands.Clear();

                PSObject paramHelp = _ps
                    .AddCommand("Microsoft.PowerShell.Core\\Get-Help")
                    .AddParameter("Name", commandName)
                    .AddParameter("Parameter", parameterName)
                    .Invoke<PSObject>()
                    .FirstOrDefault();

                WriteParameterHelp(paramHelp);
            }
        }

        private void WriteDynamicHelpBlock(Collection<string> helpBlock)
        {
            var bufferWidth = _console.BufferWidth;
            var colWidth = Math.Min(helpBlock.Max(s => LengthInBufferCells(s)) + 2, bufferWidth);
            int columns = 1;

            var dynHelp = new DynamicHelp
            {
                Singleton = this,
                ColumnWidth = colWidth,
                Columns = columns,
                Rows = (helpBlock.Count + columns - 1) / columns,
                HelpItems = helpBlock
            };

            dynHelp.SaveCursor();
            dynHelp.DrawHelpBlock(dynHelp);

            Console.ReadKey(intercept: true);

            dynHelp.Clear();
            dynHelp.RestoreCursor();
        }

        private void WriteParameterHelp(dynamic helpContent)
        {
            Collection<string> helpBlock;

            if (helpContent == null || String.IsNullOrEmpty(helpContent?.Description?[0]?.Text))
            {
                helpBlock = new Collection<string>()
                {
                    String.Empty,
                    PSReadLineResources.NeedsUpdateHelp
                };
            }
            else
            {
                char c = (char)0x1b;

                string syntax = $"{c}[7m-{helpContent.name} <{helpContent.type.name}>{c}[0m";
                string desc = "DESC: " + helpContent.Description[0].Text;
                string details = $"Required: {helpContent.required}, Position: {helpContent.position}, Default Value: {helpContent.defaultValue}, Pipeline Input: {helpContent.pipelineInput}, WildCard: {helpContent.globbing}";

                helpBlock = new Collection<string>
                {
                    String.Empty,
                    syntax,
                    String.Empty,
                    desc,
                    details
                };
            }

            WriteDynamicHelpBlock(helpBlock);
        }

        private class DynamicHelp
        {
            internal PSConsoleReadLine Singleton;
            internal int Top;

            internal int PreviousTop;
            internal int ColumnWidth;
            internal int BufferLines;
            internal int Rows;
            internal int Columns;
            internal int ToolTipLines;
            internal Collection<string> HelpItems;
            //internal CompletionResult CurrentMenuItem => MenuItems[CurrentSelection];
            internal int CurrentSelection;

            public void DrawHelpBlock(DynamicHelp previousMenu, bool menuSelect = true)
            {
                IConsole console = Singleton._console;

                // Move cursor to the start of the first line after our input.
                var bufferEndPoint = Singleton.ConvertOffsetToPoint(Singleton._buffer.Length);
                console.SetCursorPosition(bufferEndPoint.X, bufferEndPoint.Y);
                // Top must be initialized before calling AdjustForPossibleScroll, otherwise
                // on the last line of the buffer, the scroll operation causes Top to point
                // past the buffer, which in turn causes the menu to be printed twice.
                this.Top = bufferEndPoint.Y + 1;
                AdjustForPossibleScroll(1);
                MoveCursorDown(1);

                var bufferWidth = console.BufferWidth;
                var columnWidth = this.ColumnWidth;

                var items = this.HelpItems;
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
                        console.Write(GetHelpItem(items[index], columnWidth));
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
                    if (row != (this.Rows - 1) || !menuSelect)
                    {
                        AdjustForPossibleScroll(1);
                        MoveCursorDown(1);
                    }
                }

                if (previousMenu != null)
                {
                    if (Rows < previousMenu.Rows + previousMenu.ToolTipLines)
                    {
                        // Rest of the current line was erased, but the cursor was not moved to the next line.
                        if (console.CursorLeft != 0)
                        {
                            // There are lines from the previous rendering that need to be cleared,
                            // so we are sure there is no need to scroll.
                            MoveCursorDown(1);
                        }

                        Singleton.WriteBlankLines(previousMenu.Rows + previousMenu.ToolTipLines - Rows);
                    }
                }

                // if the menu has moved, we need to clear the lines under it
                if (bufferEndPoint.Y < PreviousTop)
                {
                    console.BlankRestOfLine();
                    Singleton.WriteBlankLines(PreviousTop - bufferEndPoint.Y);
                }

                PreviousTop = bufferEndPoint.Y;

                if (menuSelect)
                {
                    RestoreCursor();
                    console.CursorVisible = true;
                }
            }

            public void Clear()
            {
                WriteBlankLines(Top, Rows + ToolTipLines);
            }

            /* public void UpdateMenuSelection(int selectedItem, bool select, bool showTooltips, string toolTipColor)
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
                            toolTipLines += 1;
                            lineLength = 0;
                        }
                        else
                        {
                            lineLength += 1;
                            if (lineLength == console.BufferWidth)
                            {
                                toolTipLines += 1;
                                lineLength = 0;
                            }
                        }
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
                if (select) console.Write("\x1b[0m");

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

                    console.Write("\x1b[0m");
                }

                RestoreCursor();
            }*/

            /* public void MoveRight()    => CurrentSelection = Math.Min(CurrentSelection + Rows, HelpItems.Count - 1);
            public void MoveLeft()     => CurrentSelection = Math.Max(CurrentSelection - Rows, 0);
            public void MoveUp()       => CurrentSelection = Math.Max(CurrentSelection - 1, 0);
            public void MoveDown()     => CurrentSelection = Math.Min(CurrentSelection + 1, HelpItems.Count - 1);
            public void MovePageDown() => CurrentSelection = Math.Min(CurrentSelection + Rows - (CurrentSelection % Rows) - 1,
                                                                      HelpItems.Count - 1);
            public void MovePageUp()   => CurrentSelection = Math.Max(CurrentSelection - (CurrentSelection % Rows), 0);

            public void MoveN(int n)
            {
                CurrentSelection = (CurrentSelection + n) % MenuItems.Count;
                if (CurrentSelection < 0)
                {
                    CurrentSelection += MenuItems.Count;
                }
            }
            */

            private void MoveCursorDown(int cnt)
            {
                IConsole console = Singleton._console;
                while (cnt-- > 0)
                {
                    console.Write("\n");
                }
            }


            private void AdjustForPossibleScroll(int cnt)
            {
                IConsole console = Singleton._console;
                var scrollCnt = console.CursorTop + cnt + 1 - console.BufferHeight;
                if (scrollCnt > 0)
                {
                    Top -= scrollCnt;
                    _singleton._initialY -= scrollCnt;
                    _savedCursorTop -= scrollCnt;
                }
            }

            public void WriteBlankLines(int top, int count)
            {
                SaveCursor();
                Singleton._console.SetCursorPosition(0, top);
                Singleton.WriteBlankLines(count);
                RestoreCursor();
            }

            private int _savedCursorLeft;
            private int _savedCursorTop;

            public void SaveCursor()
            {
                IConsole console = Singleton._console;
                _savedCursorLeft = console.CursorLeft;
                _savedCursorTop = console.CursorTop;
            }

            public void RestoreCursor() => Singleton._console.SetCursorPosition(_savedCursorLeft, _savedCursorTop);
        }
    }
}
