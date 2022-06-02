/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    /// <summary>
    ///     Insert the key.
    /// </summary>
    public static void SelfInsert(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!key.HasValue) return;

        var keyChar = key.Value.KeyChar;
        if (keyChar == '\0')
            return;

        if (arg is int count)
        {
            if (count <= 0)
                return;
        }
        else
        {
            count = 1;
        }

        if (Singleton._visualSelectionCommandCount > 0)
        {
            _renderer.GetRegion(out var start, out var length);
            Replace(start, length, new string(keyChar, count));
        }
        else if (count > 1)
        {
            Insert(new string(keyChar, count));
        }
        else
        {
            Insert(keyChar);
        }
    }

    /// <summary>
    ///     Reverts all of the input to the current input.
    /// </summary>
    public static void RevertLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (Singleton._Prediction.RevertSuggestion()) return;

        if (Singleton._statusIsErrorMessage)
            // After an edit, clear the error message
            Singleton.ClearStatusMessage(false);

        while (Singleton._undoEditIndex > 0)
        {
            Singleton._edits[Singleton._undoEditIndex - 1].Undo();
            Singleton._undoEditIndex--;
        }

        _renderer.Render();
    }

    /// <summary>
    ///     Cancel the current input, leaving the input on the screen,
    ///     but returns back to the host so the prompt is evaluated again.
    /// </summary>
    public static void CancelLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton.ClearStatusMessage(false);
        var val = Singleton.buffer.Length;
        _renderer.Current = val;

        using var _ = Singleton._Prediction.DisableScoped();
        _renderer.ForceRender();

        Renderer.Console.Write("\x1b[91m^C\x1b[0m");

        Singleton.buffer.Clear(); // Clear so we don't actually run the input
        _renderer.Current = 0; // If Render is called, _current must be correct.
        SearcherReadLine.ResetCurrentHistoryIndex();
        Singleton._inputAccepted = true;
    }

    /// <summary>
    ///     Like KillLine - deletes text from the point to the end of the input,
    ///     but does not put the deleted text in the kill ring.
    /// </summary>
    public static void ForwardDeleteInput(ConsoleKeyInfo? key = null, object arg = null)
    {
        ForwardDeleteImpl(Singleton.buffer.Length, ForwardDeleteInput);
    }

    /// <summary>
    ///     Deletes text from the point to the end of the current logical line,
    ///     but does not put the deleted text in the kill ring.
    /// </summary>
    public static void ForwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        ForwardDeleteImpl(GetEndOfLogicalLinePos(_renderer.Current) + 1, ForwardDeleteLine);
    }

    /// <summary>
    ///     Deletes text from the cursor position to the specified end position
    ///     but does not put the deleted text in the kill ring.
    /// </summary>
    /// <param name="endPosition">0-based offset to one character past the end of the text.</param>
    private static void ForwardDeleteImpl(int endPosition, Action<ConsoleKeyInfo?, object> instigator)
    {
        var current = _renderer.Current;
        var buffer = Singleton.buffer;

        if (buffer.Length > 0 && current < endPosition)
        {
            var length = endPosition - current;
            var str = buffer.ToString(current, length);

            Singleton.SaveEditItem(
                EditItemDelete.Create(
                    str,
                    current,
                    instigator,
                    null,
                    !InViEditMode()));

            buffer.Remove(current, length);
            _renderer.Render();
        }
    }

    /// <summary>
    ///     Like BackwardKillInput - deletes text from the point to the start of the input,
    ///     but does not put the deleted text in the kill ring.
    public static void BackwardDeleteInput(ConsoleKeyInfo? key = null, object arg = null)
    {
        BackwardDeleteSubstring(0, BackwardDeleteInput);
    }

    /// <summary>
    ///     Like BackwardKillLine - deletes text from the point to the start of the logical line,
    ///     but does not put the deleted text in the kill ring.
    /// </summary>
    public static void BackwardDeleteLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        var position = GetBeginningOfLinePos(_renderer.Current);
        BackwardDeleteSubstring(position, BackwardDeleteLine);
    }

    private static void BackwardDeleteSubstring(int position, Action<ConsoleKeyInfo?, object> instigator)
    {
        if (_renderer.Current > position)
        {
            var count = _renderer.Current - position;

            Singleton.RemoveTextToViRegister(position, count, instigator, null, !InViEditMode());
            _renderer.Current = position;
            _renderer.Render();
        }
    }

    /// <summary>
    ///     Delete the character before the cursor.
    /// </summary>
    public static void BackwardDeleteChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (Singleton._visualSelectionCommandCount > 0)
        {
            _renderer.GetRegion(out var start, out var length);
            Delete(start, length);
            return;
        }

        if (Singleton.buffer.Length > 0 && _renderer.Current > 0)
        {
            var qty = arg as int? ?? 1;
            if (qty < 1) return; // Ignore useless counts
            qty = Math.Min(qty, _renderer.Current);

            var startDeleteIndex = _renderer.Current - qty;

            Singleton.RemoveTextToViRegister(startDeleteIndex, qty, BackwardDeleteChar, arg, !InViEditMode());
            _renderer.Current = startDeleteIndex;
            _renderer.Render();
        }
    }

    private void DeleteCharImpl(int qty, bool orExit)
    {
        if (_visualSelectionCommandCount > 0)
        {
            _renderer.GetRegion(out var start, out var length);
            Delete(start, length);
            return;
        }

        if (buffer.Length > 0)
        {
            if (_renderer.Current < buffer.Length)
            {
                qty = Math.Min(qty, Singleton.buffer.Length - _renderer.Current);

                RemoveTextToViRegister(_renderer.Current, qty, DeleteChar, qty, !InViEditMode());
                if (_renderer.Current >= buffer.Length)
                    _renderer.Current = Math.Max(0, buffer.Length + ViEndOfLineFactor);
                _renderer.Render();
            }
        }
        else if (orExit)
        {
            throw new ExitException();
        }
    }

    /// <summary>
    ///     Delete the character under the cursor.
    /// </summary>
    public static void DeleteChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        var qty = arg as int? ?? 1;
        if (qty < 1) return; // Ignore useless counts

        Singleton.DeleteCharImpl(qty, false);
    }

    /// <summary>
    ///     Delete the character under the cursor, or if the line is empty, exit the process.
    /// </summary>
    public static void DeleteCharOrExit(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton.DeleteCharImpl(1, true);
    }

    private bool AcceptLineImpl(bool validate)
    {
        using var _ = _Prediction.DisableScoped();

        buffer.ToString();
        if (ParseErrors.Any(e => e.IncompleteInput))
        {
            Insert('\n');
            return false;
        }

        // If text was pasted, for performance reasons we skip rendering for some time,
        // but if input is accepted, we won't have another chance to render.
        //
        // Also - if there was an emphasis, we want to clear that before accepting
        // and that requires rendering.
        var renderNeeded = EP.IsNotEmphasisEmpty() || _queuedKeys.Count > 0;

        EP.EmphasisInit();


        if (renderNeeded) _renderer.ForceRender();

        // Only run validation if we haven't before.  If we have and status line shows an error,
        // treat that as a -Force and accept the input so it is added to history, and PowerShell
        // can report an error as it normally does.
        if (validate && !_statusIsErrorMessage)
        {
            var insertionPoint = _renderer.Current;
            var errorMessage = Validate(RLAst);
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                // If there are more keys, assume the user pasted with a right click and
                // we should insert a newline even though validation failed.
                if (_queuedKeys.Count > 0)
                {
                    // Validation may have moved the cursor.  Because there are queued
                    // keys, we need to move the cursor back to the correct place, and
                    // ignore where validation put the cursor because the queued keys
                    // will be inserted in the wrong place.
                    SetCursorPosition(insertionPoint);
                    Insert('\n');
                }

                _renderer.StatusLinePrompt = "";
                _renderer.StatusBuffer.Append(errorMessage);
                _statusIsErrorMessage = true;
                _renderer.Render();
                return false;
            }
        }

        if (_statusIsErrorMessage) ClearStatusMessage(true);

        // Make sure cursor is at the end before writing the line.
        if (_renderer.Current != _rl.buffer.Length)
        {
            // Let public API set cursor to end of line incase end of line is end of buffer.
            _renderer.Current = _rl.buffer.Length;
            SetCursorPosition(_renderer.Current);
        }

        if (_Prediction.ActiveView is PredictionListView listView)
            // Send feedback to prediction plugin if a list item is accepted as the final command line.
            listView.OnSuggestionAccepted();

        // Clear the prediction view if there is one.
        _Prediction.ActiveView.Clear(true);

        Renderer.Console.Write("\n");
        _inputAccepted = true;
        return true;
    }

    private string Validate(Ast rootAst)
    {
        if (ParseErrors != null && ParseErrors.Length > 0)
        {
            // Move the cursor to the point of error
            _renderer.Current = ParseErrors[0].Extent.EndOffset;
            return ParseErrors[0].Message;
        }

        var validationVisitor = new CommandValidationVisitor(rootAst);
        rootAst.Visit(validationVisitor);
        if (!string.IsNullOrWhiteSpace(validationVisitor.detectedError)) return validationVisitor.detectedError;

        return null;
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
            return true;

        var cmdAsts = rootAst.FindAll(ast => ast is CommandAst, true).OfType<CommandAst>();
        foreach (var cmdAst in cmdAsts)
        {
            // If we dot source something, we can't in general know what is being
            // dot sourced so just assume the unresolved command will work.
            // If we use the invocation operator, allow that because an expression
            // is being invoked and it's reasonable to just allow it.
            if (cmdAst.InvocationOperator != TokenKind.Unknown) return true;

            // Are we importing a module or being tricky with Invoke-Expression?  Let those through.
            var candidateCommand = cmdAst.GetCommandName();
            if (candidateCommand.Equals("Import-Module", StringComparison.OrdinalIgnoreCase)
                || candidateCommand.Equals("ipmo", StringComparison.OrdinalIgnoreCase)
                || candidateCommand.Equals("Invoke-Expression", StringComparison.OrdinalIgnoreCase)
                || candidateCommand.Equals("iex", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (commandName.Length == 1)
            switch (commandName[0])
            {
                // The following are debugger commands that should be accepted if we're debugging
                // because the console host will interpret these commands directly.
                case 's':
                case 'v':
                case 'o':
                case 'c':
                case 'q':
                case 'k':
                case 'l':
                case 'S':
                case 'V':
                case 'O':
                case 'C':
                case 'Q':
                case 'K':
                case 'L':
                case '?':
                case 'h':
                case 'H':
                    // Ideally we would check $PSDebugContext, but it is set at function
                    // scope, and because we're in a module, we can't find that variable
                    // (arguably a PowerShell issue.)
                    // NestedPromptLevel is good enough though - it's rare to be in a nested.
                    var nestedPromptLevel = _engineIntrinsics.SessionState.PSVariable.GetValue("NestedPromptLevel");
                    if (nestedPromptLevel is int) return (int) nestedPromptLevel > 0;
                    break;
            }

        return false;
    }

    /// <summary>
    ///     Attempt to execute the current input.  If the current input is incomplete (for
    ///     example there is a missing closing parenthesis, bracket, or quote, then the
    ///     continuation prompt is displayed on the next line and PSReadLine waits for
    ///     keys to edit the current input.
    /// </summary>
    public static void AcceptLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton.AcceptLineImpl(false);
    }

    /// <summary>
    ///     Attempt to execute the current input.  If the current input is incomplete (for
    ///     example there is a missing closing parenthesis, bracket, or quote, then the
    ///     continuation prompt is displayed on the next line and PSReadLine waits for
    ///     keys to edit the current input.
    /// </summary>
    public static void ValidateAndAcceptLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton.AcceptLineImpl(true);
    }

    /// <summary>
    ///     Attempt to execute the current input.  If it can be executed (like AcceptLine),
    ///     then recall the next item from history the next time ReadLine is called.
    /// </summary>
    public static void AcceptAndGetNext(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (Singleton.AcceptLineImpl(false))
        {
            if (SearcherReadLine.CurrentHistoryIndex < _hs.Historys.Count - 1)
                _hs.GetNextHistoryIndex = SearcherReadLine.CurrentHistoryIndex + 1;
            else
                Ding();
        }
    }

    /// <summary>
    ///     The continuation prompt is displayed on the next line and PSReadLine waits for
    ///     keys to edit the current input.  This is useful to enter multi-line input as
    ///     a single command even when a single line is complete input by itself.
    /// </summary>
    public static void AddLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        Insert('\n');
    }

    /// <summary>
    ///     A new empty line is created above the current line regardless of where the cursor
    ///     is on the current line.  The cursor moves to the beginning of the new line.
    /// </summary>
    public static void InsertLineAbove(ConsoleKeyInfo? key = null, object arg = null)
    {
        // Move the current position to the beginning of the current line and only the current line.
        _renderer.Current = GetBeginningOfLinePos(_renderer.Current);
        Insert('\n');
        PreviousLine();
    }

    /// <summary>
    ///     A new empty line is created below the current line regardless of where the cursor
    ///     is on the current line.  The cursor moves to the beginning of the new line.
    /// </summary>
    public static void InsertLineBelow(ConsoleKeyInfo? key = null, object arg = null)
    {
        var i = _renderer.Current;
        for (; i < Singleton.buffer.Length; i++)
            if (Singleton.buffer[i] == '\n')
                break;

        _renderer.Current = i;

        Insert('\n');
    }

    private class CommandValidationVisitor : AstVisitor
    {
        private readonly Ast _rootAst;
        internal string detectedError;

        internal CommandValidationVisitor(Ast rootAst)
        {
            _rootAst = rootAst;
        }

        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            var commandName = commandAst.GetCommandName();
            if (commandName != null)
            {
                if (Singleton._engineIntrinsics != null)
                {
                    var commandInfo =
                        Singleton._engineIntrinsics.InvokeCommand.GetCommand(commandName, CommandTypes.All);
                    if (commandInfo == null && !Singleton.UnresolvedCommandCouldSucceed(commandName, _rootAst))
                    {
                        _renderer.Current = commandAst.CommandElements[0].Extent.EndOffset;
                        detectedError = string.Format(CultureInfo.CurrentCulture,
                            PSReadLineResources.CommandNotFoundError, commandName);
                        return AstVisitAction.StopVisit;
                    }
                }

                if (commandAst.CommandElements.Any(e => e is ScriptBlockExpressionAst))
                    if (Singleton.Options.CommandsToValidateScriptBlockArguments == null ||
                        !Singleton.Options.CommandsToValidateScriptBlockArguments.Contains(commandName))
                        return AstVisitAction.SkipChildren;
            }

            if (Singleton.Options.CommandValidationHandler != null)
                try
                {
                    Singleton.Options.CommandValidationHandler(commandAst);
                }
                catch (Exception e)
                {
                    detectedError = e.Message;
                }

            return !string.IsNullOrWhiteSpace(detectedError)
                ? AstVisitAction.StopVisit
                : AstVisitAction.Continue;
        }
    }
}