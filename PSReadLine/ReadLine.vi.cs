/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    /// <summary>
    ///     Remembers last history search direction.
    /// </summary>
    private bool _searchHistoryBackward = true;

    /// <summary>
    ///     Repeat the last recorded character search.
    /// </summary>
    public static void RepeatLastCharSearch(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!ViCharacterSearcher.IsRepeatable)
        {
            Ding();
            return;
        }

        if (ViCharacterSearcher.WasBackward)
            ViCharacterSearcher.SearchBackward(ViCharacterSearcher.SearchChar, null,
                ViCharacterSearcher.WasBackoff);
        else
            ViCharacterSearcher.Search(ViCharacterSearcher.SearchChar, null, ViCharacterSearcher.WasBackoff);
    }

    /// <summary>
    ///     Repeat the last recorded character search, but in the opposite direction.
    /// </summary>
    public static void RepeatLastCharSearchBackwards(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!ViCharacterSearcher.IsRepeatable)
        {
            Ding();
            return;
        }

        if (ViCharacterSearcher.WasBackward)
            ViCharacterSearcher.Search(ViCharacterSearcher.SearchChar, null, ViCharacterSearcher.WasBackoff);
        else
            ViCharacterSearcher.SearchBackward(ViCharacterSearcher.SearchChar, null,
                ViCharacterSearcher.WasBackoff);
    }

    /// <summary>
    ///     Read the next character and then find it, going forward, and then back off a character.
    ///     This is for 't' functionality.
    /// </summary>
    public static void SearchChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViCharacterSearcher.Set(keyChar, false, false);
        ViCharacterSearcher.Search(keyChar, arg, false);
    }

    /// <summary>
    ///     Read the next character and then find it, going backward, and then back off a character.
    ///     This is for 'T' functionality.
    /// </summary>
    public static void SearchCharBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViCharacterSearcher.Set(keyChar, true, false);
        ViCharacterSearcher.SearchBackward(keyChar, arg, false);
    }

    /// <summary>
    ///     Read the next character and then find it, going forward, and then back off a character.
    ///     This is for 't' functionality.
    /// </summary>
    public static void SearchCharWithBackoff(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViCharacterSearcher.Set(keyChar, false, true);
        ViCharacterSearcher.Search(keyChar, arg, true);
    }

    /// <summary>
    ///     Read the next character and then find it, going backward, and then back off a character.
    ///     This is for 'T' functionality.
    /// </summary>
    public static void SearchCharBackwardWithBackoff(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViCharacterSearcher.Set(keyChar, true, true);
        ViCharacterSearcher.SearchBackward(keyChar, arg, true);
    }

    /// <summary>
    ///     Exits the shell.
    /// </summary>
    public static void ViExit(ConsoleKeyInfo? key = null, object arg = null)
    {
        throw new ExitException();
    }

    /// <summary>
    ///     Delete to the end of the line.
    /// </summary>
    public static void DeleteToEnd(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (_renderer.Current >= Singleton.buffer.Length)
        {
            Ding();
            return;
        }

        var lineCount = _renderer.GetLogicalLineCount();
        var lineIndex = _renderer.GetLogicalLineNumber() - 1;

        if (TryGetArgAsInt(arg, out var requestedLineCount, 1))
        {
            var targetLineIndex = lineIndex + requestedLineCount - 1;
            if (targetLineIndex >= lineCount) targetLineIndex = lineCount - 1;

            var startPosition = _renderer.Current;
            var endPosition = GetEndOfNthLogicalLinePos(targetLineIndex);

            var length = endPosition - startPosition + 1;
            if (length > 0)
            {
                Singleton.RemoveTextToViRegister(
                    startPosition,
                    length,
                    DeleteToEnd,
                    arg);

                // the cursor will go back one character, unless at the beginning of the line
                var endOfLineCursorPos = GetEndOfLogicalLinePos(_renderer.Current) - 1;
                var beginningOfLinePos = GetBeginningOfLinePos(_renderer.Current);

                _renderer.Current = Math.Max(
                    beginningOfLinePos,
                    Math.Min(_renderer.Current, endOfLineCursorPos));

                _renderer.Render();
            }
        }
    }

    /// <summary>
    ///     Delete the next word.
    /// </summary>
    public static void DeleteWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        var qty = arg as int? ?? 1;
        var endPoint = _renderer.Current;
        for (var i = 0; i < qty; i++)
            endPoint = Singleton.ViFindNextWordPoint(endPoint, Singleton.Options.WordDelimiters);

        if (endPoint <= _renderer.Current)
        {
            Ding();
            return;
        }

        DeleteToEndPoint(arg, endPoint, DeleteWord);
    }

    private static void DeleteToEndPoint(object arg, int endPoint, Action<ConsoleKeyInfo?, object> instigator)
    {
        Singleton.RemoveTextToViRegister(
            _renderer.Current,
            endPoint - _renderer.Current,
            instigator,
            arg);

        if (_renderer.Current >= Singleton.buffer.Length)
            _renderer.Current = Math.Max(0, Singleton.buffer.Length - 1);
        _renderer.Render();
    }

    private static void DeleteBackwardToEndPoint(object arg, int endPoint,
        Action<ConsoleKeyInfo?, object> instigator)
    {
        var deleteLength = _renderer.Current - endPoint;

        Singleton.RemoveTextToViRegister(
            endPoint,
            deleteLength,
            instigator,
            arg);

        _renderer.Current = endPoint;
        _renderer.Render();
    }

    /// <summary>
    ///     Delete the next glob (white space delimited word).
    /// </summary>
    public static void ViDeleteGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        var qty = arg as int? ?? 1;
        var endPoint = _renderer.Current;
        while (qty-- > 0) endPoint = Singleton.ViFindNextGlob(endPoint);

        DeleteToEndPoint(arg, endPoint, ViDeleteGlob);
    }

    /// <summary>
    ///     Delete to the end of the word.
    /// </summary>
    public static void DeleteEndOfWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        var qty = arg as int? ?? 1;
        var endPoint = _renderer.Current;
        for (var i = 0; i < qty; i++)
            endPoint = Singleton.ViFindNextWordEnd(endPoint, Singleton.Options.WordDelimiters);

        if (endPoint <= _renderer.Current)
        {
            Ding();
            return;
        }

        DeleteToEndPoint(arg, 1 + endPoint, DeleteEndOfWord);
    }

    /// <summary>
    ///     Delete to the end of the word.
    /// </summary>
    public static void ViDeleteEndOfGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        var qty = arg as int? ?? 1;
        var endPoint = _renderer.Current;
        for (var i = 0; i < qty; i++) endPoint = Singleton.ViFindGlobEnd(endPoint);

        DeleteToEndPoint(arg, 1 + endPoint, ViDeleteEndOfGlob);
    }

    /// <summary>
    ///     Deletes until given character.
    /// </summary>
    public static void ViDeleteToChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViDeleteToChar(keyChar, key, arg);
    }

    /// <summary>
    ///     Deletes until given character.
    /// </summary>
    public static void ViDeleteToChar(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        ViCharacterSearcher.Set(keyChar, false, false);
        ViCharacterSearcher.SearchDelete(keyChar, arg, false, (_key, _arg) => ViDeleteToChar(keyChar, _key, _arg));
    }

    /// <summary>
    ///     Deletes backwards until given character.
    /// </summary>
    public static void ViDeleteToCharBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViDeleteToCharBack(keyChar, key, arg);
    }

    /// <summary>
    ///     Deletes backwards until given character.
    /// </summary>
    private static void ViDeleteToCharBack(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        ViCharacterSearcher.SearchBackwardDelete(keyChar, arg, false,
            (_key, _arg) => ViDeleteToCharBack(keyChar, _key, _arg));
    }

    /// <summary>
    ///     Deletes until given character.
    /// </summary>
    public static void ViDeleteToBeforeChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViDeleteToBeforeChar(keyChar, key, arg);
    }

    /// <summary>
    ///     Deletes until given character.
    /// </summary>
    public static void ViDeleteToBeforeChar(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        ViCharacterSearcher.Set(keyChar, false, true);
        ViCharacterSearcher.SearchDelete(keyChar, arg, true,
            (_key, _arg) => ViDeleteToBeforeChar(keyChar, _key, _arg));
    }

    /// <summary>
    ///     Deletes until given character.
    /// </summary>
    public static void ViDeleteToBeforeCharBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViDeleteToBeforeCharBack(keyChar, key, arg);
    }

    private static void ViDeleteToBeforeCharBack(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        ViCharacterSearcher.Set(keyChar, true, true);
        ViCharacterSearcher.SearchBackwardDelete(keyChar, arg, true,
            (_key, _arg) => ViDeleteToBeforeCharBack(keyChar, _key, _arg));
    }

    /// <summary>
    ///     Ring the bell.
    /// </summary>
    private static void Ding(ConsoleKeyInfo? key = null, object arg = null)
    {
        Ding();
    }

    /// <summary>
    ///     Switch the current operating mode from Vi-Insert to Vi-Command.
    /// </summary>
    public static void ViCommandMode(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (Singleton._editGroupStart >= 0) Singleton._groupUndoHelper.EndGroup();
        Singleton._dispatchTable = _viCmdKeyMap;
        Singleton._chordDispatchTable = _viCmdChordTable;
        ViBackwardChar();
        Singleton.ViIndicateCommandMode();
    }

    /// <summary>
    ///     Switch to Insert mode.
    /// </summary>
    public static void ViInsertMode(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._dispatchTable = _viInsKeyMap;
        Singleton._chordDispatchTable = _viInsChordTable;
        Singleton.ViIndicateInsertMode();
    }

    /// <summary>
    ///     Returns true if in Vi edit mode, otherwise false.
    /// </summary>
    public static bool InViEditMode()
    {
        return Singleton.Options.EditMode == EditMode.Vi;
    }

    /// <summary>
    ///     Returns true if in Vi Command mode, otherwise false.
    /// </summary>
    public static bool InViCommandMode()
    {
        return Singleton._dispatchTable == _viCmdKeyMap;
    }

    /// <summary>
    ///     Returns true if in Vi Insert mode, otherwise false.
    /// </summary>
    private static bool InViInsertMode()
    {
        return Singleton._dispatchTable == _viInsKeyMap;
    }

    /// <summary>
    ///     Temporarily swap in Vi-Command dispatch tables. Used for setting handlers.
    /// </summary>
    internal static IDisposable UseViCommandModeTables()
    {
        var oldDispatchTable = Singleton._dispatchTable;
        var oldChordDispatchTable = Singleton._chordDispatchTable;

        Singleton._dispatchTable = _viCmdKeyMap;
        Singleton._chordDispatchTable = _viCmdChordTable;

        return new Disposable(() =>
        {
            Singleton._dispatchTable = oldDispatchTable;
            Singleton._chordDispatchTable = oldChordDispatchTable;
        });
    }

    /// <summary>
    ///     Temporarily swap in Vi-Insert dispatch tables. Used for setting handlers.
    /// </summary>
    internal static IDisposable UseViInsertModeTables()
    {
        var oldDispatchTable = Singleton._dispatchTable;
        var oldChordDispatchTable = Singleton._chordDispatchTable;

        Singleton._dispatchTable = _viInsKeyMap;
        Singleton._chordDispatchTable = _viInsChordTable;

        return new Disposable(() =>
        {
            Singleton._dispatchTable = oldDispatchTable;
            Singleton._chordDispatchTable = oldChordDispatchTable;
        });
    }

    private void ViIndicateCommandMode()
    {
        // Show suggestion in 'InsertMode' but not 'CommandMode'.
        _Prediction.DisableGlobal(false);

        if (Options.ViModeIndicator == ViModeStyle.Cursor)
        {
            Renderer.Console.CursorSize = _normalCursorSize < 50 ? 100 : 25;
        }
        else if (Options.ViModeIndicator == ViModeStyle.Prompt)
        {
            var savedBackground = Renderer.Console.BackgroundColor;
            Renderer.Console.BackgroundColor = AlternateBackground(Renderer.Console.BackgroundColor);
            InvokePrompt();
            Renderer.Console.BackgroundColor = savedBackground;
        }
        else if (Options.ViModeIndicator == ViModeStyle.Script && Options.ViModeChangeHandler != null)
        {
            Options.ViModeChangeHandler.InvokeReturnAsIs(ViMode.Command);
        }
    }

    private void ViIndicateInsertMode()
    {
        // Show suggestion in 'InsertMode' but not 'CommandMode'.
        _Prediction.EnableGlobal();

        if (Options.ViModeIndicator == ViModeStyle.Cursor)
            Renderer.Console.CursorSize = _normalCursorSize;
        else if (Options.ViModeIndicator == ViModeStyle.Prompt)
            InvokePrompt();
        else if (Options.ViModeIndicator == ViModeStyle.Script && Options.ViModeChangeHandler != null)
            Options.ViModeChangeHandler.InvokeReturnAsIs(ViMode.Insert);
    }

    /// <summary>
    ///     Switch to Insert mode and position the cursor at the beginning of the line.
    /// </summary>
    public static void ViInsertAtBegining(ConsoleKeyInfo? key = null, object arg = null)
    {
        ViInsertMode(key, arg);
        BeginningOfLine(key, arg);
    }

    /// <summary>
    ///     Switch to Insert mode and position the cursor at the end of the line.
    /// </summary>
    public static void ViInsertAtEnd(ConsoleKeyInfo? key = null, object arg = null)
    {
        ViInsertMode(key, arg);
        EndOfLine(key, arg);
    }

    /// <summary>
    ///     Append from the current line position.
    /// </summary>
    public static void ViInsertWithAppend(ConsoleKeyInfo? key = null, object arg = null)
    {
        ViInsertMode(key, arg);
        ForwardChar(key, arg);
    }

    /// <summary>
    ///     Delete the current character and switch to Insert mode.
    /// </summary>
    public static void ViInsertWithDelete(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._groupUndoHelper.StartGroup(ViInsertWithDelete, arg);

        ViInsertMode(key, arg);
        DeleteChar(key, arg);
    }

    /// <summary>
    ///     Accept the line and switch to Insert mode.
    /// </summary>
    public static void ViAcceptLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        ViInsertMode(key, arg);
        AcceptLine(key, arg);
    }

    /// <summary>
    ///     Prepend a '#' and accept the line.
    /// </summary>
    public static void PrependAndAccept(ConsoleKeyInfo? key = null, object arg = null)
    {
        BeginningOfLine(key, arg);
        SelfInsert(key, arg);
        ViAcceptLine(key, arg);
    }

    /// <summary>
    ///     Invert the case of the current character and move to the next one.
    /// </summary>
    public static void InvertCase(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (_renderer.Current >= Singleton.buffer.Length)
        {
            Ding();
            return;
        }

        var qty = arg as int? ?? 1;

        for (; qty > 0 && _renderer.Current < Singleton.buffer.Length; qty--)
        {
            var c = Singleton.buffer[_renderer.Current];
            if (char.IsLetter(c))
            {
                var newChar = char.IsUpper(c)
                    ? char.ToLower(c, CultureInfo.CurrentCulture)
                    : char.ToUpper(c, CultureInfo.CurrentCulture);
                var delEditItem = EditItemDelete.Create(
                    c.ToString(),
                    _renderer.Current,
                    InvertCase,
                    arg,
                    false);

                var insEditItem = EditItemInsertChar.Create(newChar, _renderer.Current);
                Singleton.SaveEditItem(GroupedEdit.Create(new List<EditItem>
                    {
                        delEditItem,
                        insEditItem
                    },
                    InvertCase,
                    arg
                ));

                Singleton.buffer[_renderer.Current] = newChar;
            }

            _renderer.MoveCursor(Math.Min(_renderer.Current + 1, Singleton.buffer.Length));
        }

        _renderer.Render();
    }

    /// <summary>
    ///     Swap the current character and the one before it.
    /// </summary>
    public static void SwapCharacters(ConsoleKeyInfo? key = null, object arg = null)
    {
        // if in vi command mode, the cursor can't go as far
        var bufferLength = Singleton.buffer.Length;
        var cursorRightLimit = bufferLength + ViEndOfLineFactor;
        if (_renderer.Current <= 0 || bufferLength < 2 || _renderer.Current > cursorRightLimit)
        {
            Ding();
            return;
        }

        var cursor = _renderer.Current;
        if (cursor == bufferLength)
            --cursor; // if at end of line, swap previous two chars

        Singleton.SaveEditItem(EditItemSwapCharacters.Create(cursor));
        Singleton.SwapCharactersImpl(cursor);

        _renderer.MoveCursor(Math.Min(cursor + 1, cursorRightLimit));
        _renderer.Render();
    }

    private void SwapCharactersImpl(int cursor)
    {
        var current = buffer[cursor];
        var previous = buffer[cursor - 1];

        buffer[cursor] = previous;
        buffer[cursor - 1] = current;
    }

    /// <summary>
    ///     Deletes text from the cursor to the first non-blank character of the line.
    /// </summary>
    public static void DeleteLineToFirstChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (_renderer.Current > 0)
        {
            var i = GetFirstNonBlankOfLogicalLinePos(_renderer.Current);

            Singleton.RemoveTextToViRegister(
                i,
                _renderer.Current - i,
                DeleteLineToFirstChar,
                arg);

            _renderer.Current = i;
            _renderer.Render();
        }
        else
        {
            Ding();
        }
    }

    /// <summary>
    ///     Deletes the current line, enabling undo.
    /// </summary>
    public static void DeleteLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        var lineCount = _renderer.GetLogicalLineCount();
        var lineIndex = _renderer.GetLogicalLineNumber() - 1;

        TryGetArgAsInt(arg, out var requestedLineCount, 1);

        var deletePosition = DeleteLineImpl(lineIndex, requestedLineCount);

        // goto the first character of the first remaining logical line
        var newCurrent = deletePosition + 1;

        if (lineIndex + requestedLineCount >= lineCount)
            // if the delete operation has removed all the remaining lines
            // goto the first character of the previous logical line
            newCurrent = GetBeginningOfLinePos(deletePosition);

        _renderer.Current = newCurrent;
        _renderer.Render();
    }

    /// <summary>
    ///     Deletes as many requested lines from the buffer
    ///     starting from the specified line index, and
    ///     return the offset to the deleted position.
    /// </summary>
    /// <returns></returns>
    private static int DeleteLineImpl(int lineIndex, int lineCount)
    {
        var range = Singleton.buffer.GetRange(lineIndex, lineCount);

        var deleteText = Singleton.buffer.ToString(range.Offset, range.Count);

        _viRegister.LinewiseRecord(deleteText);

        var deletePosition = range.Offset;
        var anchor = _renderer.Current;

        Singleton.buffer.Remove(range.Offset, range.Count);

        Singleton.SaveEditItem(
            EditItemDeleteLines.Create(
                deleteText,
                deletePosition,
                anchor));

        return deletePosition;
    }

    /// <summary>
    ///     Deletes from the current logical line to the end of the buffer.
    /// </summary>
    public static void DeleteEndOfBuffer(ConsoleKeyInfo? key = null, object arg = null)
    {
        var lineIndex = _renderer.GetLogicalLineNumber() - 1;
        var lineCount = _renderer.GetLogicalLineCount() - lineIndex;

        DeleteLineImpl(lineIndex, lineCount);

        // move the cursor to the beginning of the previous line
        var previousLineIndex = Math.Max(0, lineIndex - 1);
        var newPosition = GetBeginningOfNthLinePos(previousLineIndex);

        _renderer.Current = newPosition;
        _renderer.Render();
    }

    /// <summary>
    ///     Deletes the current and next n logical lines.
    /// </summary>
    private static void DeleteNextLines(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (TryGetArgAsInt(arg, out var requestedLineCount, 1)) DeleteLine(key, requestedLineCount + 1);
    }

    /// <summary>
    ///     Deletes from the previous n logical lines to the current logical line included.
    /// </summary>
    public static void DeletePreviousLines(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (TryGetArgAsInt(arg, out var requestedLineCount, 1))
        {
            var currentLineIndex = _renderer.GetLogicalLineNumber() - 1;
            var startLineIndex = Math.Max(0, currentLineIndex - requestedLineCount);

            DeleteLineImpl(startLineIndex, currentLineIndex - startLineIndex + 1);

            // go the beginning of the line at index 'startLineIndex'
            // or at the beginning of the last line
            startLineIndex = Math.Min(startLineIndex, _renderer.GetLogicalLineCount() - 1);
            var newCurrent = GetBeginningOfNthLinePos(startLineIndex);

            _renderer.Current = newCurrent;
            _renderer.Render();
        }
    }

    /// <summary>
    ///     Delete from the current logical line to the n-th requested logical line in a multiline buffer
    /// </summary>
    private static void DeleteRelativeLines(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (TryGetArgAsInt(arg, out var requestedLineNumber, 1))
        {
            var currentLineIndex = _renderer.GetLogicalLineNumber() - 1;
            var requestedLineIndex = requestedLineNumber - 1;
            if (requestedLineIndex < 0) requestedLineIndex = 0;

            var logicalLineCount = _renderer.GetLogicalLineCount();
            if (requestedLineIndex >= logicalLineCount) requestedLineIndex = logicalLineCount - 1;

            var requestedLineCount = requestedLineIndex - currentLineIndex;
            if (requestedLineCount < 0)
                DeletePreviousLines(null, -requestedLineCount);
            else
                DeleteNextLines(null, requestedLineCount);
        }
    }

    /// <summary>
    ///     Deletes the previous word.
    /// </summary>
    public static void BackwardDeleteWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        var qty = arg as int? ?? 1;
        var deletePoint = _renderer.Current;
        for (var i = 0; i < qty; i++)
            deletePoint = Singleton.ViFindPreviousWordPoint(deletePoint, Singleton.Options.WordDelimiters);
        if (deletePoint == _renderer.Current)
        {
            Ding();
            return;
        }

        Singleton.RemoveTextToViRegister(
            deletePoint,
            _renderer.Current - deletePoint,
            BackwardDeleteWord,
            arg);

        _renderer.Current = deletePoint;
        _renderer.Render();
    }

    /// <summary>
    ///     Deletes the previous word, using only white space as the word delimiter.
    /// </summary>
    public static void ViBackwardDeleteGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (_renderer.Current == 0)
        {
            Ding();
            return;
        }

        var qty = arg as int? ?? 1;
        var deletePoint = _renderer.Current;
        for (var i = 0; i < qty && deletePoint > 0; i++)
            deletePoint = Singleton.ViFindPreviousGlob(deletePoint - 1);
        if (deletePoint == _renderer.Current)
        {
            Ding();
            return;
        }

        Singleton.RemoveTextToViRegister(
            deletePoint,
            _renderer.Current - deletePoint,
            BackwardDeleteWord,
            arg);

        _renderer.Current = deletePoint;
        _renderer.Render();
    }

    /// <summary>
    ///     Find the matching brace, paren, or square bracket and delete all contents within, including the brace.
    /// </summary>
    public static void ViDeleteBrace(ConsoleKeyInfo? key = null, object arg = null)
    {
        var newCursor = Singleton.ViFindBrace(_renderer.Current);

        if (_renderer.Current < newCursor)
            DeleteRange(_renderer.Current, newCursor, ViDeleteBrace);
        else if (newCursor < _renderer.Current)
            DeleteRange(newCursor, _renderer.Current, ViDeleteBrace);
        else
            Ding();
    }

    /// <summary>
    ///     Delete all characters included in the supplied range.
    /// </summary>
    /// <param name="first">Index of where to begin the delete.</param>
    /// <param name="last">Index of where to end the delete.</param>
    /// <param name="action">Action that generated this request, used for repeat command ('.').</param>
    private static void DeleteRange(int first, int last, Action<ConsoleKeyInfo?, object> action)
    {
        var length = last - first + 1;

        Singleton.RemoveTextToViRegister(
            first,
            length,
            action);

        _renderer.Current = first;
        _renderer.Render();
    }


    /// <summary>
    ///     Prompts for a search string and initiates search upon AcceptLine.
    /// </summary>
    public static void ViSearchHistoryBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        SearcherReadLine.SaveCurrentLine();
        Singleton.StartSearch(true);
    }

    /// <summary>
    ///     Prompts for a search string and initiates search upon AcceptLine.
    /// </summary>
    public static void SearchForward(ConsoleKeyInfo? key = null, object arg = null)
    {
        SearcherReadLine.SaveCurrentLine();
        Singleton.StartSearch(false);
    }

    /// <summary>
    ///     Repeat the last search in the same direction as before.
    /// </summary>
    public static void RepeatSearch(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (string.IsNullOrEmpty(_hs.SearchHistoryPrefix))
        {
            Ding();
            return;
        }

        _hs.AnyHistoryCommandCount++;
        Singleton.HistorySearch();
    }

    /// <summary>
    ///     Repeat the last search in the same direction as before.
    /// </summary>
    public static void RepeatSearchBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._searchHistoryBackward = !Singleton._searchHistoryBackward;
        RepeatSearch();
        Singleton._searchHistoryBackward = !Singleton._searchHistoryBackward;
    }

    /// <summary>
    ///     Prompts for a string for history searching.
    /// </summary>
    /// <param name="backward">True for searching backward in the history.</param>
    private void StartSearch(bool backward)
    {
        _renderer.StatusLinePrompt = "find: ";
        var argBuffer = _renderer.StatusBuffer;
        _renderer.Render(); // Render prompt

        while (true)
        {
            var nextKey = ReadKey();
            if (nextKey == Keys.Enter || nextKey == Keys.Tab)
            {
                _hs.SearchHistoryPrefix = argBuffer.ToString();
                _searchHistoryBackward = backward;
                HistorySearch();
                break;
            }

            if (nextKey == Keys.Escape) break;
            if (nextKey == Keys.Backspace)
            {
                if (argBuffer.Length > 0)
                {
                    argBuffer.Remove(argBuffer.Length - 1, 1);
                    _renderer.Render(); // Render prompt
                    continue;
                }

                break;
            }

            argBuffer.Append(nextKey.KeyChar);
            _renderer.Render(); // Render prompt
        }

        // Remove our status line
        argBuffer.Clear();
        _renderer.StatusLinePrompt = null;
        _renderer.Render(); // Render prompt
    }

    /// <summary>
    ///     Searches line history.
    /// </summary>
    private void HistorySearch()
    {
        _hs.SearchHistoryCommandCount++;

        var incr = _searchHistoryBackward ? -1 : +1;
        var moveCursor = Options.HistorySearchCursorMovesToEnd
            ? HistorySearcherReadLine.HistoryMoveCursor.ToEnd
            : HistorySearcherReadLine.HistoryMoveCursor.DontMove;
        for (var i = SearcherReadLine.CurrentHistoryIndex + incr; i >= 0 && i < _hs.Historys.Count; i += incr)
            if (Options.HistoryStringComparison.HasFlag(StringComparison.OrdinalIgnoreCase))
            {
                if (_hs.Historys[i].CommandLine.ToLower().Contains(_hs.SearchHistoryPrefix.ToLower()))
                {
                    SearcherReadLine.CurrentHistoryIndex = i;
                    SearcherReadLine.UpdateBufferFromHistory(moveCursor);
                    return;
                }
            }
            else
            {
                if (_hs.Historys[i].CommandLine.Contains(_hs.SearchHistoryPrefix))
                {
                    SearcherReadLine.CurrentHistoryIndex = i;
                    SearcherReadLine.UpdateBufferFromHistory(moveCursor);
                    return;
                }
            }

        Ding();
    }

    /// <summary>
    ///     Repeat the last text modification.
    /// </summary>
    public static void RepeatLastCommand(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (Singleton._undoEditIndex > 0)
        {
            var editItem = Singleton._edits[Singleton._undoEditIndex - 1];
            if (editItem._instigator != null)
            {
                editItem._instigator(key, editItem._instigatorArg);
                return;
            }
        }

        Ding();
    }

    /// <summary>
    ///     Chords in vi needs special handling because a numeric argument can be input between the 1st and 2nd key.
    /// </summary>
    private static void ViChord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!key.HasValue) throw new ArgumentNullException(nameof(key));
        if (arg != null)
        {
            Chord(key, arg);
            return;
        }

        if (Singleton._chordDispatchTable.TryGetValue(PSKeyInfo.FromConsoleKeyInfo(key.Value),
                out var secondKeyDispatchTable)) ViChordHandler(secondKeyDispatchTable, arg);
    }

    private static void ViChordHandler(Dictionary<PSKeyInfo, KeyHandler> secondKeyDispatchTable, object arg = null)
    {
        var secondKey = ReadKey();
        if (secondKeyDispatchTable.TryGetValue(secondKey, out var handler))
        {
            Singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, true, arg);
        }
        else if (!IsNumeric(secondKey))
        {
            Singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, true, arg);
        }
        else
        {
            var argBuffer = _renderer.StatusBuffer;
            argBuffer.Clear();
            _renderer.StatusLinePrompt = "digit-argument: ";
            while (IsNumeric(secondKey))
            {
                argBuffer.Append(secondKey.KeyChar);
                _renderer.Render();
                secondKey = ReadKey();
            }

            var numericArg = int.Parse(argBuffer.ToString());
            if (secondKeyDispatchTable.TryGetValue(secondKey, out handler))
                Singleton.ProcessOneKey(secondKey, secondKeyDispatchTable, true, numericArg);
            else
                Ding();
            argBuffer.Clear();
            Singleton.ClearStatusMessage(true);
        }
    }

    private static void ViDGChord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!key.HasValue) throw new ArgumentNullException(nameof(key));

        ViChordHandler(_viChordDGTable, arg);
    }

    private static bool IsNumeric(PSKeyInfo key)
    {
        return key.KeyChar >= '0' && key.KeyChar <= '9' && !key.Control && !key.Alt;
    }

    /// <summary>
    ///     Start a new digit argument to pass to other functions while in one of vi's chords.
    /// </summary>
    public static void ViDigitArgumentInChord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!key.HasValue || char.IsControl(key.Value.KeyChar))
        {
            Ding();
            return;
        }

        if (Singleton.Options.EditMode == EditMode.Vi && key.Value.KeyChar == '0')
        {
            BeginningOfLine();
            return;
        }

        var sawDigit = false;
        _renderer.StatusLinePrompt = "digit-argument: ";
        var argBuffer = _renderer.StatusBuffer;
        argBuffer.Append(key.Value.KeyChar);
        if (key.Value.KeyChar == '-')
            argBuffer.Append('1');
        else
            sawDigit = true;

        _renderer.Render(); // Render prompt
        while (true)
        {
            var nextKey = ReadKey();
            if (Singleton._dispatchTable.TryGetValue(nextKey, out var handler) && handler.Action == DigitArgument)
            {
                if (nextKey == Keys.Minus)
                {
                    if (argBuffer[0] == '-')
                        argBuffer.Remove(0, 1);
                    else
                        argBuffer.Insert(0, '-');
                    _renderer.Render(); // Render prompt
                    continue;
                }

                if (IsNumeric(nextKey))
                {
                    if (!sawDigit && argBuffer.Length > 0)
                        // Buffer is either '-1' or '1' from one or more Alt+- keys
                        // but no digits yet.  Remove the '1'.
                        argBuffer.Length -= 1;
                    sawDigit = true;
                    argBuffer.Append(nextKey.KeyChar);
                    _renderer.Render(); // Render prompt
                    continue;
                }
            }

            if (int.TryParse(argBuffer.ToString(), out var intArg))
                Singleton.ProcessOneKey(nextKey, Singleton._dispatchTable, false, intArg);
            else
                Ding();
            break;
        }
    }

    /// <summary>
    ///     Like DeleteCharOrExit in Emacs mode, but accepts the line instead of deleting a character.
    /// </summary>
    public static void ViAcceptLineOrExit(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (Singleton.buffer.Length > 0)
        {
            ViInsertMode(key, arg);
            Singleton.AcceptLineImpl(false);
        }
        else
        {
            ViExit(key, arg);
        }
    }

    /// <summary>
    ///     A new line is inserted above the current line.
    /// </summary>
    public static void ViInsertLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._groupUndoHelper.StartGroup(ViInsertLine, arg);
        Singleton.MoveToBeginningOfPhrase();
        Singleton.buffer.Insert(_renderer.Current, '\n');
        //_singleton._current = Math.Max(0, _singleton._current - 1);
        Singleton.SaveEditItem(EditItemInsertChar.Create('\n', _renderer.Current));
        _renderer.Render();
        ViInsertMode();
    }

    private void MoveToBeginningOfPhrase()
    {
        while (!IsAtBeginningOfPhrase()) _renderer.Current--;
    }

    private bool IsAtBeginningOfPhrase()
    {
        if (_renderer.Current == 0) return true;
        if (buffer[_renderer.Current - 1] == '\n') return true;
        return false;
    }

    /// <summary>
    ///     A new line is inserted below the current line.
    /// </summary>
    public static void ViAppendLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._groupUndoHelper.StartGroup(ViInsertLine, arg);
        Singleton.MoveToEndOfPhrase();
        var insertPoint = 0;
        if (Singleton.IsAtEndOfLine(_renderer.Current))
        {
            insertPoint = Singleton.buffer.Length;
            Singleton.buffer.Append('\n');
            _renderer.Current = insertPoint;
        }
        else
        {
            insertPoint = _renderer.Current + 1;
            Singleton.buffer.Insert(insertPoint, '\n');
        }

        Singleton.SaveEditItem(EditItemInsertChar.Create('\n', insertPoint));
        _renderer.Render();
        ViInsertWithAppend();
    }

    private void MoveToEndOfPhrase()
    {
        while (!IsAtEndOfPhrase()) _renderer.Current++;
    }

    private bool IsAtEndOfPhrase()
    {
        if (buffer.Length == 0 || _renderer.Current == buffer.Length + ViEndOfLineFactor) return true;
        if (_renderer.Current == buffer.Length && buffer[_renderer.Current - 1] == '\n') return true;
        if (buffer[_renderer.Current] == '\n') return true;
        return false;
    }

    /// <summary>
    ///     Joins the current line and the next line.
    /// </summary>
    public static void ViJoinLines(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton.MoveToEndOfPhrase();
        if (Singleton.IsAtEndOfLine(_renderer.Current))
        {
            Ding();
        }
        else
        {
            Singleton.buffer[_renderer.Current] = ' ';
            Singleton._groupUndoHelper.StartGroup(ViJoinLines, arg);
            Singleton.SaveEditItem(EditItemDelete.Create(
                "\n",
                _renderer.Current,
                ViJoinLines,
                arg,
                false));

            Singleton.SaveEditItem(EditItemInsertChar.Create(' ', _renderer.Current));
            Singleton._groupUndoHelper.EndGroup();
            _renderer.Render();
        }
    }

    private class ViCharacterSearcher
    {
        private static readonly ViCharacterSearcher instance = new();
        private char searchChar = '\0';
        private bool wasBackoff;
        private bool wasBackward;

        public static bool IsRepeatable => instance.searchChar != '\0';
        public static char SearchChar => instance.searchChar;
        public static bool WasBackward => instance.wasBackward;
        public static bool WasBackoff => instance.wasBackoff;

        public static void Set(char theChar, bool isBackward, bool isBackoff)
        {
            instance.searchChar = theChar;
            instance.wasBackward = isBackward;
            instance.wasBackoff = isBackoff;
        }

        public static void Search(char keyChar, object arg, bool backoff)
        {
            var qty = arg as int? ?? 1;

            for (var i = _renderer.Current + 1; i < Singleton.buffer.Length; i++)
                if (Singleton.buffer[i] == keyChar)
                {
                    qty -= 1;
                    if (qty == 0)
                    {
                        _renderer.MoveCursor(backoff ? i - 1 : i);
                        return;
                    }
                }

            Ding();
        }

        public static bool SearchDelete(char keyChar, object arg, bool backoff,
            Action<ConsoleKeyInfo?, object> instigator)
        {
            var qty = arg as int? ?? 1;

            for (var i = _renderer.Current + 1; i < Singleton.buffer.Length; i++)
                if (Singleton.buffer[i] == keyChar)
                {
                    qty -= 1;
                    if (qty == 0)
                    {
                        DeleteToEndPoint(arg, backoff ? i : i + 1, instigator);
                        return true;
                    }
                }

            Ding();
            return false;
        }

        public static void SearchBackward(char keyChar, object arg, bool backoff)
        {
            var qty = arg as int? ?? 1;

            for (var i = _renderer.Current - 1; i >= 0; i--)
                if (Singleton.buffer[i] == keyChar)
                {
                    qty -= 1;
                    if (qty == 0)
                    {
                        _renderer.MoveCursor(backoff ? i + 1 : i);
                        return;
                    }
                }

            Ding();
        }

        public static bool SearchBackwardDelete(char keyChar, object arg, bool backoff,
            Action<ConsoleKeyInfo?, object> instigator)
        {
            Set(keyChar, true, backoff);
            var qty = arg as int? ?? 1;

            for (var i = _renderer.Current - 1; i >= 0; i--)
                if (Singleton.buffer[i] == keyChar)
                {
                    qty -= 1;
                    if (qty == 0)
                    {
                        DeleteBackwardToEndPoint(arg, backoff ? i + 1 : i, instigator);
                        return true;
                    }
                }

            Ding();
            return false;
        }
    }
}