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

        /// <summary>
        /// Attempt to show help content.
        /// Show the full help for the command on the alternate screen buffer.
        /// </summary>
        public static void ShowCommandHelp(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.DynamicHelpImpl(isFullHelp: true);
        }

        /// <summary>
        /// Attempt to show help content.
        /// Show the short help of the parameter next to the cursor.
        /// </summary>
        public static void ShowParameterHelp(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.DynamicHelpImpl(isFullHelp: false);
        }

        object IPSConsoleReadLineMockableMethods.GetDynamicHelpContent(string commandName, string parameterName, bool isFullHelp)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                return null;
            }

            try
            {
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

                if (isFullHelp)
                {
                    return ps
                        .AddCommand($"Microsoft.PowerShell.Core\\Get-Help")
                        .AddParameter("Name", commandName)
                        .AddParameter("Full", value: true)
                        .AddCommand($"Microsoft.PowerShell.Utility\\Out-String")
                        .Invoke<string>()
                        .FirstOrDefault();
                }

                if (string.IsNullOrEmpty(parameterName))
                {
                    return null;
                }

                return ps
                    .AddCommand("Microsoft.PowerShell.Core\\Get-Help")
                    .AddParameter("Name", commandName)
                    .AddParameter("Parameter", parameterName)
                    .Invoke<PSObject>()
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                // GetDynamicHelpContent could scroll the screen, e.g. via Write-Progress. For example,
                // cd <TAB> under the CloudShell Azure drive will show the progress bar while fetching data.
                // We need to update the _initialY in case the current cursor postion has changed.
                if (_singleton._initialY > _console.CursorTop)
                {
                    _singleton._initialY = _console.CursorTop;
                }
            }
        }

        void IPSConsoleReadLineMockableMethods.RenderFullHelp(string content, string regexPatternToScrollTo)
        {
            _pager.Write(content, regexPatternToScrollTo);
        }

        private void WriteDynamicHelpContent(string commandName, string parameterName, bool isFullHelp)
        {
            var helpContent = _mockableMethods.GetDynamicHelpContent(commandName, parameterName, isFullHelp);

            if (helpContent is string fullHelp && fullHelp.Length > 0)
            {
                string regexPatternToScrollTo = null;

                if (!string.IsNullOrEmpty(parameterName))
                {
                    string upper = parameterName[0].ToString().ToUpperInvariant();
                    string lower = parameterName[0].ToString().ToLowerInvariant();
                    string remainingString = parameterName.Substring(1);
                    regexPatternToScrollTo = $"-[{upper}|{lower}]{remainingString} [<|\\[]";
                }

                _mockableMethods.RenderFullHelp(fullHelp, regexPatternToScrollTo);
            }
            else if (helpContent is PSObject paramHelp)
            {
                WriteParameterHelp(paramHelp);
            }
        }

        private void DynamicHelpImpl(bool isFullHelp)
        {
            if (isFullHelp)
            {
                _pager ??= new Pager();
            }

            int cursor = _singleton._current;
            string commandName = null;
            string parameterName = null;

            foreach(var token in _singleton._tokens)
            {
                var extent = token.Extent;

                if (extent.EndOffset > cursor)
                {
                    break;
                }

                if (token.TokenFlags == TokenFlags.CommandName)
                {
                    commandName = token.Text;
                }

                if (extent.StartOffset <= cursor && extent.EndOffset >= cursor)
                {
                    if (token.Kind == TokenKind.Parameter)
                    {
                        parameterName = ((ParameterToken)token).ParameterName;
                        break;
                    }
                }
            }

            WriteDynamicHelpContent(commandName, parameterName, isFullHelp);
        }

        private void WriteDynamicHelpBlock(Collection<string> helpBlock)
        {
            var bufferWidth = _console.BufferWidth;

            var dynHelp = new MultilineDisplayBlock
            {
                Singleton = this,
                ItemsToDisplay = helpBlock
            };

            dynHelp.DrawMultilineBlock();
            ReadKey();
            dynHelp.Clear();
        }

        private void WriteParameterHelp(dynamic helpContent)
        {
            Collection<string> helpBlock;

            if (helpContent == null || string.IsNullOrEmpty(helpContent?.Description?[0]?.Text))
            {
                helpBlock = new Collection<string>()
                {
                    String.Empty,
                    PSReadLineResources.NeedsUpdateHelp
                };
            }
            else
            {
                string syntax = $"-{helpContent.name} <{helpContent.type.name}>";
                string desc = "DESC: " + helpContent.Description[0].Text;
                
                // trim new line characters as some help content has it at the end of the first list on the description.
                desc = desc.Trim('\r', '\n');
                
                string details = $"Required: {helpContent.required}, Position: {helpContent.position}, Default Value: {helpContent.defaultValue}, Pipeline Input: {helpContent.pipelineInput}, WildCard: {helpContent.globbing}";

                helpBlock = new Collection<string>
                {
                    string.Empty,
                    syntax,
                    string.Empty,
                    desc,
                    details
                };
            }

            WriteDynamicHelpBlock(helpBlock);
        }

        private class MultilineDisplayBlock : DisplayBlockBase
        {
            internal Collection<string> ItemsToDisplay;

            private int multilineItems = 0;

            public void DrawMultilineBlock()
            {
                IConsole console = Singleton._console;

                multilineItems = 0;

                this.SaveCursor();

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

                var items = this.ItemsToDisplay;

                for (var index = 0; index < items.Count; index++)
                {
                    if (items[index].Length > bufferWidth)
                    {
                        // if the length required is exactly a multiple of buffer width.
                        multilineItems += items[index].Length / bufferWidth;

                        /* if the item length is more than the buffer width it needs 1 extra line for the remaining characters.
                        if (items[index].Length % bufferWidth != 0)
                        {
                            multilineItems++;
                        }*/
                    }

                    console.Write(items[index]);

                    // Explicit newline so consoles see each row as distinct lines, but skip the
                    // last line so we don't scroll.
                    if (index != (items.Count - 1))
                    {
                        AdjustForPossibleScroll(1);
                        MoveCursorDown(1);
                    }
                }

                this.RestoreCursor();
            }

            public void Clear()
            {
                _singleton.WriteBlankLines(Top, ItemsToDisplay.Count + multilineItems);
            }
        }
    }
}
