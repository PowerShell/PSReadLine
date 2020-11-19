/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.Internal;
using Microsoft.PowerShell.PSReadLine;
using Microsoft.PowerShell;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private Microsoft.PowerShell.Pager _pager;
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

        /* private static string GetHelpItem(string item, int columnWidth)
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
        } */

        private class DynamicHelp : MultilineDisplayBlock
        {
            internal Collection<string> HelpItems;

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
                        console.Write(GetItem(items[index], columnWidth));
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

                bool extraPreRowsCleared = false;
                if (previousMenu != null)
                {
                    if (Rows < previousMenu.Rows)
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

                        Singleton.WriteBlankLines(previousMenu.Rows - Rows);
                        extraPreRowsCleared = true;
                    }
                }

                // if the menu has moved, we need to clear the lines under it
                if (bufferEndPoint.Y < PreviousTop)
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
                WriteBlankLines(Top, Rows);
            }
        }
    }
}
