using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Insert the key
        /// </summary>
        public static void SelfInsert(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                return;
            }

            if (arg is int)
            {
                var count = (int)arg;
                if (count <= 0)
                    return;
                if (count > 1)
                {
                    var toInsert = new string(key.Value.KeyChar, count);
                    if (_singleton._visualSelectionCommandCount > 0)
                    {
                        int start, length;
                        _singleton.GetRegion(out start, out length);
                        Replace(start, length, toInsert);
                    }
                    else
                    {
                        Insert(toInsert);
                    }
                    return;
                }
            }

            if (_singleton._visualSelectionCommandCount > 0)
            {
                int start, length;
                _singleton.GetRegion(out start, out length);
                Replace(start, length, new string(key.Value.KeyChar, 1));
            }
            else
            {
                Insert(key.Value.KeyChar);
            }

        }

        /// <summary>
        /// Reverts all of the input to the current input.
        /// </summary>
        public static void RevertLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._statusIsErrorMessage)
            {
                // After an edit, clear the error message
                _singleton.ClearStatusMessage(render: false);
            }

            while (_singleton._undoEditIndex > 0)
            {
                _singleton._edits[_singleton._undoEditIndex - 1].Undo();
                _singleton._undoEditIndex--;
            }
            _singleton.Render();
        }

        /// <summary>
        /// Cancel the current input, leaving the input on the screen,
        /// but returns back to the host so the prompt is evaluated again.
        /// </summary>
        public static void CancelLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._current = _singleton._buffer.Length;
            // We want to display ^C to show the line was canceled.  Instead of appending ^C
            // (or (char)3), we append 2 spaces so we don't affect tokenization too much, e.g.
            // changing a keyword to a command.
            _singleton._buffer.Append("  ");
            _singleton.ReallyRender();

            // Now that we've rendered with this extra spaces, go back and replace the spaces
            // with ^C colored in red (so it stands out.)
            var coordinates = _singleton.ConvertOffsetToCoordinates(_singleton._current);
            int i = (coordinates.Y - _singleton._initialY) * Console.BufferWidth + coordinates.X;
            _singleton._consoleBuffer[i].UnicodeChar = '^';
            _singleton._consoleBuffer[i].ForegroundColor = ConsoleColor.Red;
            _singleton._consoleBuffer[i].BackgroundColor = Console.BackgroundColor;
            _singleton._consoleBuffer[i+1].UnicodeChar = 'C';
            _singleton._consoleBuffer[i+1].ForegroundColor = ConsoleColor.Red;
            _singleton._consoleBuffer[i+1].BackgroundColor = Console.BackgroundColor;
            WriteBufferLines(_singleton._consoleBuffer, ref _singleton._initialY);

            var y = coordinates.Y + 1;
            _singleton.PlaceCursor(0, ref y);
            _singleton._buffer.Clear(); // Clear so we don't actually run the input
            _singleton._currentHistoryIndex = _singleton._history.Count;
            _singleton._inputAccepted = true;
        }

        /// <summary>
        /// Like ForwardKillLine - deletes text from the point to the end of the line,
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        public static void ForwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            var current = _singleton._current;
            var buffer = _singleton._buffer;
            if (buffer.Length > 0 && current < buffer.Length)
            {
                int length = buffer.Length - current;
                var str = buffer.ToString(current, length);
                _singleton.SaveEditItem(EditItemDelete.Create(str, current));
                buffer.Remove(current, length);
                _singleton.Render();
            }
        }

        /// <summary>
        /// Like BackwardKillLine - deletes text from the point to the start of the line,
        /// but does not put the deleted text in the kill ring.
        /// </summary>
        public static void BackwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._current > 0)
            {
                var str = _singleton._buffer.ToString(0, _singleton._current);
                _singleton.SaveEditItem(EditItemDelete.Create(str, 0));
                _singleton._buffer.Remove(0, _singleton._current);
                _singleton._current = 0;
                _singleton.Render();
            }
        }

        /// <summary>
        /// Delete the character before the cursor.
        /// </summary>
        public static void BackwardDeleteChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                int start, length;
                _singleton.GetRegion(out start, out length);
                Delete(start, length);
                return;
            }

            if (_singleton._buffer.Length > 0 && _singleton._current > 0)
            {
                int startDeleteIndex = _singleton._current - 1;
                _singleton.SaveEditItem(
                    EditItemDelete.Create(new string(_singleton._buffer[startDeleteIndex], 1), startDeleteIndex));
                _singleton._buffer.Remove(startDeleteIndex, 1);
                _singleton._current--;
                _singleton.Render();
            }
        }

        private void DeleteCharImpl(bool orExit)
        {
            if (_visualSelectionCommandCount > 0)
            {
                int start, length;
                GetRegion(out start, out length);
                Delete(start, length);
                return;
            }

            if (_buffer.Length > 0)
            {
                if (_current < _buffer.Length)
                {
                    SaveEditItem(EditItemDelete.Create(new string(_buffer[_current], 1), _current));
                    _buffer.Remove(_current, 1);
                    Render();
                }
            }
            else if (orExit)
            {
                throw new ExitException();
            }
        }

        /// <summary>
        /// Delete the character under the cursor.
        /// </summary>
        public static void DeleteChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.DeleteCharImpl(orExit: false);
        }

        /// <summary>
        /// Delete the character under the cursor, or if the line is empty, exit the process
        /// </summary>
        public static void DeleteCharOrExit(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.DeleteCharImpl(orExit: true);
        }

        private bool AcceptLineImpl(bool validate)
        {
            ParseInput();
            if (_parseErrors.Any(e => e.IncompleteInput))
            {
                Insert('\n');
                return false;
            }

            // If text was pasted, for performance reasons we skip rendering for some time,
            // but if input is accepted, we won't have another chance to render.
            //
            // Also - if there was an emphasis, we want to clear that before accepting
            // and that requires rendering.
            bool renderNeeded = _emphasisStart >= 0 || _queuedKeys.Count > 0;

            _renderForDemoNeeded = false;
            _emphasisStart = -1;
            _emphasisLength = 0;

            // Make sure cursor is at the end before writing the line
            _current = _buffer.Length;

            if (renderNeeded)
            {
                ReallyRender();
            }

            // Only run validation if we haven't before.  If we have and status line shows an error,
            // treat that as a -Force and accept the input so it is added to history, and PowerShell
            // can report an error as it normally does.
            if (validate && !_statusIsErrorMessage)
            {
                var errorMessage = Validate(_ast);
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    _statusLinePrompt = "";
                    _statusBuffer.Append(errorMessage);
                    _statusIsErrorMessage = true;
                    Render();
                    return false;
                }
            }

            if (_statusIsErrorMessage)
            {
                ClearStatusMessage(render: true);
            }

            var coordinates = ConvertOffsetToCoordinates(_current);
            var y = coordinates.Y + 1;
            PlaceCursor(0, ref y);
            _inputAccepted = true;
            return true;
        }

        private string Validate(Ast rootAst)
        {
            if (_parseErrors != null && _parseErrors.Length > 0)
            {
                // Move the cursor to the point of error
                _current = _parseErrors[0].Extent.EndOffset;
                return _parseErrors[0].Message;
            }

            var commandAsts = rootAst.FindAll(a => a is CommandAst, true);
            if (_engineIntrinsics != null)
            {
                foreach (var ast in commandAsts)
                {
                    var commandAst = (CommandAst)ast;
                    var commandName = commandAst.GetCommandName();
                    if (commandName != null)
                    {
                        var commandInfo = _engineIntrinsics.InvokeCommand.GetCommand(commandName, CommandTypes.All);
                        if (commandInfo == null && !UnresolvedCommandCouldSucceed(commandName, rootAst))
                        {
                            _current = commandAst.CommandElements[0].Extent.EndOffset;
                            return string.Format(PSReadLineResources.CommandNotFoundError, commandName);
                        }
                    }
                }
            }

            string errorMessage = null;
            if (_options.ValidationHandler != null)
            {
                try
                {
                    dynamic validationResult = _options.ValidationHandler(rootAst.Extent.Text);
                    if (validationResult != null)
                    {
                        errorMessage = validationResult as string;
                        if (errorMessage == null)
                        {
                            try
                            {
                                errorMessage = validationResult.Message;
                                _current = validationResult.Offset;
                            }
                            catch {}
                        }
                        if (errorMessage == null)
                        {
                            errorMessage = validationResult.ToString();
                        }
                    }
                }
                catch (Exception e)
                {
                    errorMessage = e.Message;
                    try
                    {
                        // If the exception object has an Offset property, we use it.
                        // If not, this will throw an exception which we ignore.
                        dynamic em = e;
                        _current = em.Offset;
                    }
                    catch {}
                }
            }

            return errorMessage;
        }

        private bool UnresolvedCommandCouldSucceed(string commandName, Ast rootAst)
        {
            // This is a little hacky, but we check for a few things where part of the current
            // command defines/imports new commands that PowerShell might not yet know about.
            // There is little reason to go to great lengths at being correct here, validation
            // is just a small usability  tweak to avoid cluttering up history - PowerShell
            // will report errors for stuff we actually let through.

            // Do we define a function matching the command name?
            var fnDefns = rootAst.FindAll(ast => ast is FunctionDefinitionAst, true).OfType<FunctionDefinitionAst>();
            if (fnDefns.Any(fnDefnAst => fnDefnAst.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var cmdAsts = rootAst.FindAll(ast => ast is CommandAst, true).OfType<CommandAst>();
            foreach (var cmdAst in cmdAsts)
            {
                // If we dot source something, we can't in general know what is being
                // dot sourced so just assume the unresolved command will work.
                // If we use the invocation operator, allow that because an expression
                // is being invoked and it's reasonable to just allow it.
                if (cmdAst.InvocationOperator != TokenKind.Unknown)
                {
                    return true;
                }

                // Are we importing a module or being tricky with Invoke-Expression?  Let those through.
                var candidateCommand = cmdAst.GetCommandName();
                if (candidateCommand.Equals("Import-Module", StringComparison.OrdinalIgnoreCase)
                    || candidateCommand.Equals("ipmo", StringComparison.OrdinalIgnoreCase)
                    || candidateCommand.Equals("Invoke-Expression", StringComparison.OrdinalIgnoreCase)
                    || candidateCommand.Equals("iex", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        static bool StaticParameterBindingSupported(CommandInfo commandInfo)
        {
            var aliasInfo = commandInfo as AliasInfo;

            if (aliasInfo != null)
            {
                commandInfo = aliasInfo.ResolvedCommand;
            }

            return (commandInfo is ExternalScriptInfo)
                   || (commandInfo is CmdletInfo)
                   || (commandInfo is FunctionInfo);
        }

        /// <summary>
        /// Attempt to execute the current input.  If the current input is incomplete (for
        /// example there is a missing closing parenthesis, bracket, or quote, then the
        /// continuation prompt is displayed on the next line and PSReadline waits for
        /// keys to edit the current input.
        /// </summary>
        public static void AcceptLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.AcceptLineImpl(false);
        }

        /// <summary>
        /// Attempt to execute the current input.  If the current input is incomplete (for
        /// example there is a missing closing parenthesis, bracket, or quote, then the
        /// continuation prompt is displayed on the next line and PSReadline waits for
        /// keys to edit the current input.
        /// </summary>
        public static void ValidateAndAcceptLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.AcceptLineImpl(true);
        }

        /// <summary>
        /// Attempt to execute the current input.  If it can be executed (like AcceptLine),
        /// then recall the next item from history the next time Readline is called.
        /// </summary>
        public static void AcceptAndGetNext(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton.AcceptLineImpl(false))
            {
                if (_singleton._currentHistoryIndex < (_singleton._history.Count - 1))
                {
                    _singleton._getNextHistoryIndex = _singleton._currentHistoryIndex + 1;
                }
                else
                {
                    Ding();
                }
            }
        }

        /// <summary>
        /// The continuation prompt is displayed on the next line and PSReadline waits for
        /// keys to edit the current input.  This is useful to enter multi-line input as
        /// a single command even when a single line is complete input by itself.
        /// </summary>
        public static void AddLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            Insert('\n');
        }
    }
}
