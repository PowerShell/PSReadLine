/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    // *must* be initialized in the static ctor
    // because it depends on static member _singleton
    // being initialized first.
    private static readonly ViRegister _viRegister;

    /// <summary>
    ///     Paste the clipboard after the cursor, moving the cursor to the end of the pasted text.
    /// </summary>
    public static void PasteAfter(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (_viRegister.IsEmpty)
        {
            Ding();
            return;
        }

        Singleton.PasteAfterImpl();
    }

    /// <summary>
    ///     Paste the clipboard before the cursor, moving the cursor to the end of the pasted text.
    /// </summary>
    public static void PasteBefore(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (_viRegister.IsEmpty)
        {
            Ding();
            return;
        }

        Singleton.PasteBeforeImpl();
    }

    private void PasteAfterImpl()
    {
        _renderer.Current = _viRegister.PasteAfter(buffer, _renderer.Current);
        _renderer.Render();
    }

    private void PasteBeforeImpl()
    {
        _renderer.Current = _viRegister.PasteBefore(buffer, _renderer.Current);
        _renderer.Render();
    }

    private void SaveToClipboard(int startIndex, int length)
    {
        _viRegister.Record(buffer, startIndex, length);
    }

    /// <summary>
    ///     Saves a number of logical lines in the unnamed register
    ///     starting at the specified line number and specified count.
    /// </summary>
    /// <param name="lineIndex">The logical number of the current line, starting at 0.</param>
    /// <param name="lineCount">The number of lines to record to the unnamed register</param>
    private void SaveLinesToClipboard(int lineIndex, int lineCount)
    {
        var range = buffer.GetRange(lineIndex, lineCount);
        _viRegister.LinewiseRecord(buffer.ToString(range.Offset, range.Count));
    }

    /// <summary>
    ///     Remove a portion of text from the buffer, save it to the vi register
    ///     and also save it to the edit list to support undo.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="count"></param>
    /// <param name="instigator"></param>
    /// <param name="arg"></param>
    /// <param name="moveCursorToEndWhenUndoDelete">
    ///     Use 'false' as the default value because this method is used a lot by VI operations,
    ///     and for VI opeartions, we do NOT want to move the cursor to the end when undoing a
    ///     deletion.
    /// </param>
    public void RemoveTextToViRegister(
        int start,
        int count,
        Action<ConsoleKeyInfo?, object> instigator = null,
        object arg = null,
        bool moveCursorToEndWhenUndoDelete = false)
    {
        Singleton.SaveToClipboard(start, count);
        Singleton.SaveEditItem(EditItemDelete.Create(
            _viRegister.RawText,
            start,
            instigator,
            arg,
            moveCursorToEndWhenUndoDelete));
        Singleton.buffer.Remove(start, count);
    }

    /// <summary>
    ///     Yank the entire buffer.
    /// </summary>
    public static void ViYankLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        TryGetArgAsInt(arg, out var lineCount, 1);
        var lineIndex = _renderer.GetLogicalLineNumber() - 1;
        Singleton.SaveLinesToClipboard(lineIndex, lineCount);
    }

    /// <summary>
    ///     Yank character(s) under and to the right of the cursor.
    /// </summary>
    public static void ViYankRight(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var start = _renderer.Current;
        var length = 0;

        while (numericArg-- > 0) length++;

        Singleton.SaveToClipboard(start, length);
    }

    /// <summary>
    ///     Yank character(s) to the left of the cursor.
    /// </summary>
    public static void ViYankLeft(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var start = _renderer.Current;
        if (start == 0)
        {
            Singleton.SaveToClipboard(start, 1);
            return;
        }

        var length = 0;

        while (numericArg-- > 0)
            if (start > 0)
            {
                start--;
                length++;
            }

        Singleton.SaveToClipboard(start, length);
    }

    /// <summary>
    ///     Yank from the cursor to the end of the buffer.
    /// </summary>
    public static void ViYankToEndOfLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        var start = _renderer.Current;
        var end = GetEndOfLogicalLinePos(_renderer.Current);
        var length = end - start + 1;
        if (length > 0) Singleton.SaveToClipboard(start, length);
    }

    /// <summary>
    ///     Yank the word(s) before the cursor.
    /// </summary>
    public static void ViYankPreviousWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var start = _renderer.Current;

        while (numericArg-- > 0) start = Singleton.ViFindPreviousWordPoint(start, Singleton.Options.WordDelimiters);

        var length = _renderer.Current - start;
        if (length > 0) Singleton.SaveToClipboard(start, length);
    }

    /// <summary>
    ///     Yank the word(s) after the cursor.
    /// </summary>
    public static void ViYankNextWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var end = _renderer.Current;

        while (numericArg-- > 0) end = Singleton.ViFindNextWordPoint(end, Singleton.Options.WordDelimiters);

        var length = end - _renderer.Current;
        //if (_singleton.IsAtEndOfLine(end))
        //{
        //    length++;
        //}
        if (length > 0) Singleton.SaveToClipboard(_renderer.Current, length);
    }

    /// <summary>
    ///     Yank from the cursor to the end of the word(s).
    /// </summary>
    public static void ViYankEndOfWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var end = _renderer.Current;

        while (numericArg-- > 0) end = Singleton.ViFindNextWordEnd(end, Singleton.Options.WordDelimiters);

        var length = 1 + end - _renderer.Current;
        if (length > 0) Singleton.SaveToClipboard(_renderer.Current, length);
    }

    /// <summary>
    ///     Yank from the cursor to the end of the WORD(s).
    /// </summary>
    public static void ViYankEndOfGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var end = _renderer.Current;

        while (numericArg-- > 0) end = Singleton.ViFindGlobEnd(end);

        var length = 1 + end - _renderer.Current;
        if (length > 0) Singleton.SaveToClipboard(_renderer.Current, length);
    }

    /// <summary>
    ///     Yank from the beginning of the buffer to the cursor.
    /// </summary>
    public static void ViYankBeginningOfLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        var start = GetBeginningOfLinePos(_renderer.Current);
        var length = _renderer.Current - start;
        if (length > 0)
        {
            Singleton.SaveToClipboard(start, length);
            _renderer.MoveCursor(start);
        }
    }

    /// <summary>
    ///     Yank from the first non-whitespace character to the cursor.
    /// </summary>
    public static void ViYankToFirstChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        var start = GetFirstNonBlankOfLogicalLinePos(_renderer.Current);
        var length = _renderer.Current - start;
        if (length > 0)
        {
            Singleton.SaveToClipboard(start, length);
            _renderer.MoveCursor(start);
        }
    }

    /// <summary>
    ///     Yank to/from matching brace.
    /// </summary>
    public static void ViYankPercent(ConsoleKeyInfo? key = null, object arg = null)
    {
        var start = Singleton.ViFindBrace(_renderer.Current);
        if (_renderer.Current < start)
            Singleton.SaveToClipboard(_renderer.Current, start - _renderer.Current + 1);
        else if (start < _renderer.Current)
            Singleton.SaveToClipboard(start, _renderer.Current - start + 1);
        else
            Ding();
    }

    /// <summary>
    ///     Yank from beginning of the WORD(s) to cursor.
    /// </summary>
    public static void ViYankPreviousGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var start = _renderer.Current;
        while (numericArg-- > 0) start = Singleton.ViFindPreviousGlob(start - 1);
        if (start < _renderer.Current)
            Singleton.SaveToClipboard(start, _renderer.Current - start);
        else
            Ding();
    }

    /// <summary>
    ///     Yank from cursor to the start of the next WORD(s).
    /// </summary>
    public static void ViYankNextGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var end = _renderer.Current;
        while (numericArg-- > 0) end = Singleton.ViFindNextGlob(end);
        Singleton.SaveToClipboard(_renderer.Current, end - _renderer.Current);
    }
}