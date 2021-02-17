﻿/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        // Yank/Kill state
        private List<string> _killRing;
        private int _killIndex;
        private int _killCommandCount;
        private int _yankCommandCount;
        private int _yankStartPoint;
        private int _yankLastArgCommandCount;
        class YankLastArgState
        {
            internal int argument;
            internal int historyIndex;
            internal int historyIncrement;
            internal int startPoint = -1;
        }
        private YankLastArgState _yankLastArgState;
        private int _visualSelectionCommandCount;

        /// <summary>
        /// Mark the current location of the cursor for use in a subsequent editing command.
        /// </summary>
        public static void SetMark(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._mark = _singleton._current;
        }

        /// <summary>
        /// The cursor is placed at the location of the mark and the mark is moved
        /// to the location of the cursor.
        /// </summary>
        public static void ExchangePointAndMark(ConsoleKeyInfo? key = null, object arg = null)
        {
            var tmp = _singleton._mark;
            _singleton._mark = _singleton._current;
            _singleton.MoveCursor(Math.Min(tmp, _singleton._buffer.Length));
        }

        /// <summary>
        /// The contents of the kill ring are cleared.
        /// </summary>
        public static void ClearKillRing()
        {
            _singleton._killRing?.Clear();
            _singleton._killIndex = -1;    // So first add indexes 0.
        }

        private void Kill(int start, int length, bool prepend)
        {
            if (length <= 0)
            {
                // if we're already in the middle of some kills,
                // change _killCommandCount so it isn't zeroed out.
                // If, OTOH, _killCommandCount was 0 to begin with,
                // we won't append to something we're not supposed to.
                if (_killCommandCount > 0)
                    _killCommandCount++;
                return;
            }
            var killText = _buffer.ToString(start, length);
            SaveEditItem(EditItemDelete.Create(killText, start));
            _buffer.Remove(start, length);
            _current = start;
            Render();
            if (_killCommandCount > 0)
            {
                if (prepend)
                {
                    _killRing[_killIndex] = killText + _killRing[_killIndex];
                }
                else
                {
                    _killRing[_killIndex] += killText;
                }
            }
            else
            {
                if (_killRing.Count < Options.MaximumKillRingCount)
                {
                    _killRing.Add(killText);
                    _killIndex = _killRing.Count - 1;
                }
                else
                {
                    _killIndex += 1;
                    if (_killIndex == _killRing.Count)
                    {
                        _killIndex = 0;
                    }
                    _killRing[_killIndex] = killText;
                }
            }
            _killCommandCount += 1;
        }

        /// <summary>
        /// Clear the input from the cursor to the end of the input.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void KillLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.Kill(_singleton._current, _singleton._buffer.Length - _singleton._current, false);
        }

        /// <summary>
        /// Clear the input from the start of the input to the cursor.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void BackwardKillInput(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.Kill(0, _singleton._current, true);
        }

        /// <summary>
        /// Clear the input from the start of the current logical line to the cursor.  The cleared text is placed
        /// in the kill ring.
        /// </summary>
        public static void BackwardKillLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            var start = GetBeginningOfLinePos(_singleton._current);
            _singleton.Kill(start, _singleton._current, true);
        }

        /// <summary>
        /// Clear the input from the cursor to the end of the current word.  If the cursor
        /// is between words, the input is cleared from the cursor to the end of the next word.
        /// The cleared text is placed in the kill ring.
        /// </summary>
        public static void KillWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton.FindForwardWordPoint(_singleton.Options.WordDelimiters);
            _singleton.Kill(_singleton._current, i - _singleton._current, false);
        }

        /// <summary>
        /// Clear the input from the cursor to the end of the current word.  If the cursor
        /// is between words, the input is cleared from the cursor to the end of the next word.
        /// The cleared text is placed in the kill ring.
        /// </summary>
        public static void ShellKillWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            var token = _singleton.FindToken(_singleton._current, FindTokenMode.CurrentOrNext);
            var end = (token.Kind == TokenKind.EndOfInput)
                ? _singleton._buffer.Length
                : token.Extent.EndOffset;
            _singleton.Kill(_singleton._current, end - _singleton._current, false);
        }

        /// <summary>
        /// Clear the input from the start of the current word to the cursor.  If the cursor
        /// is between words, the input is cleared from the start of the previous word to the
        /// cursor.  The cleared text is placed in the kill ring.
        /// </summary>
        public static void BackwardKillWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton.FindBackwardWordPoint(_singleton.Options.WordDelimiters);
            _singleton.Kill(i, _singleton._current - i, true);
        }

        /// <summary>
        /// Clear the input from the start of the current word to the cursor.  If the cursor
        /// is between words, the input is cleared from the start of the previous word to the
        /// cursor.  The cleared text is placed in the kill ring.
        /// </summary>
        public static void UnixWordRubout(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton.FindBackwardWordPoint("");
            _singleton.Kill(i, _singleton._current - i, true);
        }

        /// <summary>
        /// Clear the input from the start of the current word to the cursor.  If the cursor
        /// is between words, the input is cleared from the start of the previous word to the
        /// cursor.  The cleared text is placed in the kill ring.
        /// </summary>
        public static void ShellBackwardKillWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            var token = _singleton.FindToken(_singleton._current, FindTokenMode.Previous);
            var start = token?.Extent.StartOffset ?? 0;
            _singleton.Kill(start, _singleton._current - start, true);
        }

        /// <summary>
        /// Kill the text between the cursor and the mark.
        /// </summary>
        public static void KillRegion(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.GetRegion(out var start, out var length);
            _singleton.Kill(start, length, true);
        }

        private void YankImpl()
        {
            if (_killRing.Count == 0)
                return;

            // Starting a yank session, yank the last thing killed and
            // remember where we started.
            _mark = _yankStartPoint = _current;
            Insert(_killRing[_killIndex]);

            _yankCommandCount += 1;
        }

        /// <summary>
        /// Add the most recently killed text to the input.
        /// </summary>
        public static void Yank(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.YankImpl();
        }

        private void YankPopImpl()
        {
            if (_yankCommandCount == 0)
                return;

            _killIndex -= 1;
            if (_killIndex < 0)
            {
                _killIndex = _killRing.Count - 1;
            }
            var yankText = _killRing[_killIndex];
            Replace(_yankStartPoint, _current - _yankStartPoint, yankText);
            _yankCommandCount += 1;
        }

        /// <summary>
        /// If the previous operation was Yank or YankPop, replace the previously yanked
        /// text with the next killed text from the kill ring.
        /// </summary>
        public static void YankPop(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.YankPopImpl();
        }

        void YankArgImpl(YankLastArgState yankLastArgState)
        {
            if (yankLastArgState.historyIndex < 0 || yankLastArgState.historyIndex >= _history.Count)
            {
                Ding();
                return;
            }

            var buffer = _history[yankLastArgState.historyIndex];
            Parser.ParseInput(buffer.CommandLine, out var tokens, out var unused);

            int arg = (yankLastArgState.argument < 0)
                          ? tokens.Length + yankLastArgState.argument - 1
                          : yankLastArgState.argument;
            if (arg < 0 || arg >= tokens.Length)
            {
                Ding();
                return;
            }

            var argText = tokens[arg].Text;
            if (yankLastArgState.startPoint < 0)
            {
                yankLastArgState.startPoint = _current;
                Insert(argText);
            }
            else
            {
                Replace(yankLastArgState.startPoint, _current - yankLastArgState.startPoint, argText);
            }
        }

        /// <summary>
        /// Yank the first argument (after the command) from the previous history line.
        /// With an argument, yank the nth argument (starting from 0), if the argument
        /// is negative, start from the last argument.
        /// </summary>
        public static void YankNthArg(ConsoleKeyInfo? key = null, object arg = null)
        {
            var yankLastArgState = new YankLastArgState
            {
                argument = arg as int? ?? 1,
                historyIndex = _singleton._currentHistoryIndex - 1,
            };
            _singleton.YankArgImpl(yankLastArgState);
        }

        /// <summary>
        /// Yank the last argument from the previous history line.  With an argument,
        /// the first time it is invoked, behaves just like YankNthArg.  If invoked
        /// multiple times, instead it iterates through history and arg sets the direction
        /// (negative reverses the direction.)
        /// </summary>
        public static void YankLastArg(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (arg != null && !(arg is int))
            {
                Ding();
                return;
            }

            _singleton._yankLastArgCommandCount += 1;

            if (_singleton._yankLastArgCommandCount == 1)
            {
                _singleton._yankLastArgState = new YankLastArgState
                {
                    argument = (int?) arg ?? -1,
                    historyIncrement = -1,
                    historyIndex = _singleton._currentHistoryIndex - 1
                };

                _singleton.YankArgImpl(_singleton._yankLastArgState);
                return;
            }

            var yankLastArgState = _singleton._yankLastArgState;

            if ((int?) arg < 0)
            {
                yankLastArgState.historyIncrement = -yankLastArgState.historyIncrement;
            }

            yankLastArgState.historyIndex += yankLastArgState.historyIncrement;

            // Don't increment more than 1 out of range so it's quick to get back to being in range.
            if (yankLastArgState.historyIndex < 0)
            {
                Ding();
                yankLastArgState.historyIndex = 0;
            }
            else if (yankLastArgState.historyIndex >= _singleton._history.Count)
            {
                Ding();
                yankLastArgState.historyIndex = _singleton._history.Count - 1;
            }
            else
            {
                _singleton.YankArgImpl(yankLastArgState);
            }
        }

        private void VisualSelectionCommon(Action action, bool forceSetMark = false)
        {
            if (_singleton._visualSelectionCommandCount == 0 || forceSetMark)
            {
                SetMark();
            }
            _singleton._visualSelectionCommandCount += 1;
            action();
            _singleton.RenderWithPredictionQueryPaused();
        }

        /// <summary>
        /// Adjust the current selection to include the previous character.
        /// </summary>
        public static void SelectBackwardChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => BackwardChar(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next character.
        /// </summary>
        public static void SelectForwardChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => ForwardChar(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the previous word.
        /// </summary>
        public static void SelectBackwardWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => BackwardWord(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next word.
        /// </summary>
        public static void SelectNextWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => NextWord(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next word using ForwardWord.
        /// </summary>
        public static void SelectForwardWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => ForwardWord(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next word using ShellForwardWord.
        /// </summary>
        public static void SelectShellForwardWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => ShellForwardWord(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the next word using ShellNextWord.
        /// </summary>
        public static void SelectShellNextWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => ShellNextWord(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include the previous word using ShellBackwardWord.
        /// </summary>
        public static void SelectShellBackwardWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => ShellBackwardWord(key, arg));
        }

        /// <summary>
        /// Select the entire line.
        /// </summary>
        public static void SelectAll(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton._visualSelectionCommandCount += 1;
            _singleton._mark = 0;
            _singleton._current = _singleton._buffer.Length;
            _singleton.RenderWithPredictionQueryPaused();
        }

        /// <summary>
        /// Adjust the current selection to include from the cursor to the end of the line.
        /// </summary>
        public static void SelectLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => EndOfLine(key, arg));
        }

        /// <summary>
        /// Adjust the current selection to include from the cursor to the start of the line.
        /// </summary>
        public static void SelectBackwardsLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            _singleton.VisualSelectionCommon(() => BeginningOfLine(key, arg));
        }

        /// <summary>
        /// Select the command argument that the cursor is at, or the previous/next Nth command arguments from the current cursor position.
        /// </summary>
        public static void SelectCommandArgument(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 0))
            {
                return;
            }

            _singleton.MaybeParseInput();

            int cursor = _singleton._current;
            int prev = -1, curr = -1, next = -1;
            var sbAsts = _singleton._ast.FindAll(GetScriptBlockAst, searchNestedScriptBlocks: true).ToList();
            var arguments = new List<ExpressionAst>();

            // We start searching for command arguments from the most nested script block.
            for (int i = sbAsts.Count - 1; i >= 0; i --)
            {
                var sbAst = sbAsts[i];
                var cmdAsts = sbAst.FindAll(ast => ast is CommandAst, searchNestedScriptBlocks: false);

                foreach (CommandAst cmdAst in cmdAsts)
                {
                    for (int j = 1; j < cmdAst.CommandElements.Count; j++)
                    {
                        var argument = cmdAst.CommandElements[j] switch
                        {
                            CommandParameterAst paramAst => paramAst.Argument,
                            ExpressionAst expAst => expAst,
                            _ => null,
                        };

                        if (argument is not null)
                        {
                            arguments.Add(argument);

                            int start = argument.Extent.StartOffset;
                            int end = argument.Extent.EndOffset;

                            if (end <= cursor)
                            {
                                prev = arguments.Count - 1;
                            }
                            if (curr == -1 && start <= cursor && end > cursor)
                            {
                                curr = arguments.Count - 1;
                            }
                            else if (next == -1 && start > cursor)
                            {
                                next = arguments.Count - 1;
                            }
                        }
                    }
                }

                // Stop searching the outer script blocks if we find any command arguments within the current script block.
                if (arguments.Count > 0)
                {
                    break;
                }
            }

            // Simply return if we didn't find any command arguments.
            int count = arguments.Count;
            if (count == 0)
            {
                return;
            }

            if (prev == -1) { prev = count - 1; }
            if (next == -1) { next = 0; }
            if (curr == -1) { curr = numericArg > 0 ? prev : next; }

            int newStartCursor, newEndCursor;
            int selectCount = _singleton._visualSelectionCommandCount;

            // When an argument is already visually selected by the previous run of this function, the cursor would have past the selected argument.
            // In this case, if a user wants to move backward to an argument that is before the currently selected argument by having numericArg < 0,
            // we will need to adjust 'numericArg' to move to the expected argument.
            // Scenario:
            //   1) 'Alt+a' to select an argument;
            //   2) 'Alt+-' to make 'numericArg = -1';
            //   3) 'Alt+a' to select the argument that is right before the currently selected argument.
            if (count > 1 && numericArg < 0 && curr == next && selectCount > 0)
            {
                var prevArg = arguments[prev];
                if (_singleton._mark == prevArg.Extent.StartOffset && cursor == prevArg.Extent.EndOffset)
                {
                    numericArg--;
                }
            }

            while (true)
            {
                ExpressionAst targetAst = null;
                if (numericArg == 0)
                {
                    targetAst = arguments[curr];
                }
                else
                {
                    int index = curr + numericArg;
                    index = index >= 0 ? index % count : (count + index % count) % count;
                    targetAst = arguments[index];
                }

                // Handle quoted-string arguments specially, by leaving the quotes out of the visual selection.
                StringConstantType? constantType = null;
                if (targetAst is StringConstantExpressionAst conString)
                {
                    constantType = conString.StringConstantType;
                }
                else if (targetAst is ExpandableStringExpressionAst expString)
                {
                    constantType = expString.StringConstantType;
                }

                int startOffsetAdjustment = 0, endOffsetAdjustment = 0;
                switch (constantType)
                {
                    case StringConstantType.DoubleQuoted:
                    case StringConstantType.SingleQuoted:
                        startOffsetAdjustment = endOffsetAdjustment = 1;
                        break;
                    case StringConstantType.DoubleQuotedHereString:
                    case StringConstantType.SingleQuotedHereString:
                        startOffsetAdjustment = 2;
                        endOffsetAdjustment = 3;
                        break;
                    default: break;
                }

                newStartCursor = targetAst.Extent.StartOffset + startOffsetAdjustment;
                newEndCursor = targetAst.Extent.EndOffset - endOffsetAdjustment;

                // For quoted-string arguments, due to the special handling above, the cursor would always be
                // within the selected argument (cursor is placed at the ending quote), and thus when running
                // the 'SelectCommandArgument' action again, the same argument would be chosen.
                //
                // Below is how we detect this and move to the next argument when there is one:
                //  * the previous action was a visual selection command and the visual range was exactly
                //    what we are going to make. AND
                //  * count > 1, meaning that there are other arguments. AND
                //  * numericArg == 0. When 'numericArg' is not 0, the user is leaping among the available
                //    arguments, so it's possible that the same argument gets chosen.
                // In this case, we should select the next argument.
                if (numericArg == 0 && count > 1 && selectCount > 0 &&
                    _singleton._mark == newStartCursor && cursor == newEndCursor)
                {
                    curr = next;
                    continue;
                }

                break;
            }

            // Move cursor to the start of the argument.
            SetCursorPosition(newStartCursor);
            // Make the intended range visually selected.
            _singleton.VisualSelectionCommon(() => SetCursorPosition(newEndCursor), forceSetMark: true);


            // Get the script block AST's whose extent contains the cursor.
            bool GetScriptBlockAst(Ast ast)
            {
                if (ast is not ScriptBlockAst)
                {
                    return false;
                }

                if (ast.Parent is null)
                {
                    return true;
                }

                if (ast.Extent.StartOffset >= cursor)
                {
                    return false;
                }

                // If the script block is closed, then we want the script block only if the cursor is within the script block.
                // Otherwise, if the script block is not completed, then we want the script block even if the cursor is at the end.
                int textLength = ast.Extent.Text.Length;
                return ast.Extent.Text[textLength - 1] == '}'
                    ? ast.Extent.EndOffset - 1 > cursor
                    : ast.Extent.EndOffset >= cursor;
            }
        }

        /// <summary>
        /// Paste text from the system clipboard.
        /// </summary>
        public static void Paste(ConsoleKeyInfo? key = null, object arg = null)
        {
            string textToPaste = Clipboard.GetText();

            if (textToPaste != null)
            {
                textToPaste = textToPaste.Replace("\r", "");
                textToPaste = textToPaste.Replace("\t", "    ");
                if (_singleton._visualSelectionCommandCount > 0)
                {
                    _singleton.GetRegion(out var start, out var length);
                    Replace(start, length, textToPaste);
                }
                else
                {
                    Insert(textToPaste);
                }
            }
        }

        /// <summary>
        /// Copy selected region to the system clipboard.  If no region is selected, copy the whole line.
        /// </summary>
        public static void Copy(ConsoleKeyInfo? key = null, object arg = null)
        {
            string textToSet;
            if (_singleton._visualSelectionCommandCount > 0)
            {
                _singleton.GetRegion(out var start, out var length);
                textToSet = _singleton._buffer.ToString(start, length);
            }
            else
            {
                textToSet = _singleton._buffer.ToString();
            }
            if (!string.IsNullOrEmpty(textToSet))
            {
                Clipboard.SetText(textToSet);
            }
        }

        /// <summary>
        /// If text is selected, copy to the clipboard, otherwise cancel the line.
        /// </summary>
        public static void CopyOrCancelLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                Copy(key, arg);
            }
            else
            {
                CancelLine(key, arg);
            }
        }

        /// <summary>
        /// Delete selected region placing deleted text in the system clipboard.
        /// </summary>
        public static void Cut(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._visualSelectionCommandCount > 0)
            {
                _singleton.GetRegion(out var start, out var length);
                Clipboard.SetText(_singleton._buffer.ToString(start, length));
                Delete(start, length);
            }
        }
    }
}
