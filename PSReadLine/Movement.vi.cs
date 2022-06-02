/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    /// <summary>
    ///     Returns 0 if the cursor is allowed to go past the last character in the line, -1 otherwise.
    /// </summary>
    /// <seealso cref="ForwardChar" />
    public static int ViEndOfLineFactor => InViCommandMode() ? -1 : 0;

    /// <summary>
    ///     Move the cursor forward to the start of the next word.
    ///     Word boundaries are defined by a configurable set of characters.
    /// </summary>
    public static void ViNextWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        if (numericArg < 0)
        {
            ViBackwardWord(key, -numericArg);
            return;
        }

        while (numericArg-- > 0)
        {
            var i = Singleton.ViFindNextWordPoint(Singleton.Options.WordDelimiters);
            if (i >= Singleton.buffer.Length) i += ViEndOfLineFactor;
            _renderer.MoveCursor(Math.Max(i, 0));
        }
    }

    /// <summary>
    ///     Move the cursor back to the start of the current word, or if between words,
    ///     the start of the previous word.  Word boundaries are defined by a configurable
    ///     set of characters.
    /// </summary>
    public static void ViBackwardWord(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        if (numericArg < 0)
        {
            ViNextWord(key, -numericArg);
            return;
        }

        while (numericArg-- > 0)
            _renderer.MoveCursor(Singleton.ViFindPreviousWordPoint(Singleton.Options.WordDelimiters));
    }

    /// <summary>
    ///     Moves the cursor back to the beginning of the previous word, using only white space as delimiters.
    /// </summary>
    public static void ViBackwardGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var i = _renderer.Current;
        while (numericArg-- > 0) i = Singleton.ViFindPreviousGlob(i - 1);
        _renderer.MoveCursor(i);
    }

    /// <summary>
    ///     Moves to the next word, using only white space as a word delimiter.
    /// </summary>
    public static void ViNextGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        var i = _renderer.Current;
        while (numericArg-- > 0) i = Singleton.ViFindNextGlob(i);

        var newPosition = Math.Min(i, Math.Max(0, Singleton.buffer.Length - 1));
        if (newPosition != _renderer.Current)
            _renderer.MoveCursor(newPosition);
        else
            Ding();
    }

    /// <summary>
    ///     Moves the cursor to the end of the word, using only white space as delimiters.
    /// </summary>
    public static void ViEndOfGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        if (numericArg < 0)
        {
            ViEndOfPreviousGlob(key, -numericArg);
            return;
        }

        while (numericArg-- > 0) _renderer.MoveCursor(Singleton.ViFindEndOfGlob());
    }

    /// <summary>
    ///     Moves to the end of the previous word, using only white space as a word delimiter.
    /// </summary>
    public static void ViEndOfPreviousGlob(ConsoleKeyInfo? key = null, object arg = null)
    {
        if (!TryGetArgAsInt(arg, out var numericArg, 1)) return;

        if (numericArg < 0)
        {
            ViEndOfGlob(key, -numericArg);
            return;
        }

        while (numericArg-- > 0) _renderer.MoveCursor(Singleton.ViFindEndOfPreviousGlob());
    }

    /// <summary>
    ///     Move the cursor to the end of the current logical line.
    /// </summary>
    public static void MoveToEndOfLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        var eol = GetEndOfLogicalLinePos(_renderer.Current);
        if (eol != _renderer.Current) _renderer.MoveCursor(eol);
        Singleton._moveToEndOfLineCommandCount++;
        Singleton._moveToLineDesiredColumn = int.MaxValue;
    }

    /// <summary>
    ///     Move the cursor forward to the end of the current word, or if between words,
    ///     to the end of the next word.  Word boundaries are defined by a configurable
    ///     set of characters.
    /// </summary>
    public static void NextWordEnd(ConsoleKeyInfo? key = null, object arg = null)
    {
        var qty = arg as int? ?? 1;
        for (; qty > 0 && _renderer.Current < Singleton.buffer.Length - 1; qty--)
            _renderer.MoveCursor(Singleton.ViFindNextWordEnd(Singleton.Options.WordDelimiters));
    }

    /// <summary>
    ///     Move to the column indicated by arg.
    /// </summary>
    public static void GotoColumn(ConsoleKeyInfo? key = null, object arg = null)
    {
        var col = arg as int? ?? -1;
        if (col < 0)
        {
            Ding();
            return;
        }

        if (col < Singleton.buffer.Length + ViEndOfLineFactor)
        {
            _renderer.MoveCursor(Math.Min(col, Singleton.buffer.Length) - 1);
        }
        else
        {
            _renderer.MoveCursor(Singleton.buffer.Length + ViEndOfLineFactor);
            Ding();
        }
    }

    /// <summary>
    ///     Move the cursor to the first non-blank character in the line.
    /// </summary>
    public static void GotoFirstNonBlankOfLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        var newCurrent = GetFirstNonBlankOfLogicalLinePos(_renderer.Current);
        if (newCurrent != _renderer.Current) _renderer.MoveCursor(newCurrent);
    }

    /// <summary>
    ///     Similar to <see cref="GotoBrace" />, but is character based instead of token based.
    /// </summary>
    public static void ViGotoBrace(ConsoleKeyInfo? key = null, object arg = null)
    {
        var i = Singleton.ViFindBrace(_renderer.Current);
        if (i == _renderer.Current)
        {
            Ding();
            return;
        }

        _renderer.MoveCursor(i);
    }

    private int ViFindBrace(int i)
    {
        if (buffer.Length == 0) return i;

        switch (buffer[i])
        {
            case '{':
                return ViFindForward(i, '}', '{');
            case '[':
                return ViFindForward(i, ']', '[');
            case '(':
                return ViFindForward(i, ')', '(');
            case '}':
                return ViFindBackward(i, '{', '}');
            case ']':
                return ViFindBackward(i, '[', ']');
            case ')':
                return ViFindBackward(i, '(', ')');
            default:
                return i;
        }
    }

    private int ViFindBackward(int start, char target, char withoutPassing)
    {
        if (start == 0) return start;
        var i = start - 1;
        var withoutPassingCount = 0;
        while (i != 0 && !(buffer[i] == target && withoutPassingCount == 0))
        {
            if (buffer[i] == withoutPassing) withoutPassingCount++;
            if (buffer[i] == target) withoutPassingCount--;
            i--;
        }

        if (buffer[i] == target && withoutPassingCount == 0) return i;
        return start;
    }

    private int ViFindForward(int start, char target, char withoutPassing)
    {
        if (IsAtEndOfLine(start)) return start;
        var i = start + 1;
        var withoutPassingCount = 0;
        while (!IsAtEndOfLine(i) && !(buffer[i] == target && withoutPassingCount == 0))
        {
            if (buffer[i] == withoutPassing) withoutPassingCount++;
            if (buffer[i] == target) withoutPassingCount--;
            i++;
        }

        if (buffer[i] == target && withoutPassingCount == 0) return i;
        return start;
    }
}