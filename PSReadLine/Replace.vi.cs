/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Text;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    private static void ViReplaceUntilEsc(ConsoleKeyInfo? key, object arg)
    {
        if (_renderer.Current >= Singleton.buffer.Length)
        {
            Ding();
            return;
        }

        var startingCursor = _renderer.Current;
        var deletedStr = new StringBuilder();

        var nextKey = ReadKey();
        while (nextKey != Keys.Escape && nextKey != Keys.Enter)
        {
            if (nextKey == Keys.Backspace)
            {
                if (_renderer.Current == startingCursor)
                {
                    Ding();
                }
                else
                {
                    if (deletedStr.Length == _renderer.Current - startingCursor)
                    {
                        Singleton.buffer[_renderer.Current - 1] = deletedStr[deletedStr.Length - 1];
                        deletedStr.Remove(deletedStr.Length - 1, 1);
                    }
                    else
                    {
                        Singleton.buffer.Remove(_renderer.Current - 1, 1);
                    }

                    _renderer.Current--;
                    _renderer.Render();
                }
            }
            else
            {
                if (_renderer.Current >= Singleton.buffer.Length)
                {
                    Singleton.buffer.Append(nextKey.KeyChar);
                }
                else
                {
                    deletedStr.Append(Singleton.buffer[_renderer.Current]);
                    Singleton.buffer[_renderer.Current] = nextKey.KeyChar;
                }

                _renderer.Current++;
                _renderer.Render();
            }

            nextKey = ReadKey();
        }

        if (_renderer.Current > startingCursor)
        {
            Singleton.StartEditGroup();
            var insStr = Singleton.buffer.ToString(startingCursor, _renderer.Current - startingCursor);
            Singleton.SaveEditItem(EditItemDelete.Create(
                deletedStr.ToString(),
                startingCursor,
                ViReplaceUntilEsc,
                arg,
                false));

            Singleton.SaveEditItem(EditItemInsertString.Create(insStr, startingCursor));
            Singleton.EndEditGroup();
        }

        if (nextKey == Keys.Enter) ViAcceptLine(nextKey.AsConsoleKeyInfo());
    }

    private static void ViReplaceBrace(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViReplaceBrace, arg);
        ViDeleteBrace(key, arg);
        ViInsertMode(key, arg);
    }

    private static void ViBackwardReplaceLineToFirstChar(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViBackwardReplaceLineToFirstChar, arg);
        DeleteLineToFirstChar(key, arg);
        ViInsertMode(key, arg);
    }

    private static void ViBackwardReplaceLine(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViBackwardReplaceLine, arg);
        BackwardDeleteLine(key, arg);
        ViInsertMode(key, arg);
    }

    private static void BackwardReplaceChar(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(BackwardReplaceChar, arg);
        BackwardDeleteChar(key, arg);
        ViInsertMode(key, arg);
    }

    private static void ViBackwardReplaceWord(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViBackwardReplaceWord, arg);
        BackwardDeleteWord(key, arg);
        ViInsertMode(key, arg);
    }

    private static void ViBackwardReplaceGlob(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViBackwardReplaceGlob, arg);
        ViBackwardDeleteGlob(key, arg);
        ViInsertMode(key, arg);
    }

    private static void ViReplaceToEnd(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViReplaceToEnd, arg);
        DeleteToEnd(key, arg);
        _renderer.MoveCursor(Math.Min(Singleton.buffer.Length, _renderer.Current + 1));
        ViInsertMode(key, arg);
    }

    /// <summary>
    ///     Erase the entire command line.
    /// </summary>
    public static void ViReplaceLine(ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._groupUndoHelper.StartGroup(ViReplaceLine, arg);
        DeleteLine(key, arg);
        ViInsertMode(key, arg);
    }

    private static void ViReplaceWord(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViReplaceWord, arg);
        Singleton._lastWordDelimiter = char.MinValue;
        Singleton._shouldAppend = false;
        DeleteWord(key, arg);
        if (_renderer.Current < Singleton.buffer.Length - 1)
        {
            if (char.IsWhiteSpace(Singleton._lastWordDelimiter))
            {
                Insert(Singleton._lastWordDelimiter);
                _renderer.MoveCursor(_renderer.Current - 1);
            }

            Singleton._lastWordDelimiter = char.MinValue;
        }

        if (_renderer.Current == Singleton.buffer.Length - 1
            && !Singleton.IsDelimiter(Singleton._lastWordDelimiter, Singleton.Options.WordDelimiters)
            && Singleton._shouldAppend)
            ViInsertWithAppend(key, arg);
        else
            ViInsertMode(key, arg);
    }

    private static void ViReplaceGlob(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViReplaceGlob, arg);
        ViDeleteGlob(key, arg);
        if (_renderer.Current < Singleton.buffer.Length - 1)
        {
            Insert(' ');
            _renderer.MoveCursor(_renderer.Current - 1);
        }

        if (_renderer.Current == Singleton.buffer.Length - 1)
            ViInsertWithAppend(key, arg);
        else
            ViInsertMode(key, arg);
    }

    private static void ViReplaceEndOfWord(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViReplaceEndOfWord, arg);
        DeleteEndOfWord(key, arg);
        if (_renderer.Current == Singleton.buffer.Length - 1)
            ViInsertWithAppend(key, arg);
        else
            ViInsertMode(key, arg);
    }

    private static void ViReplaceEndOfGlob(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ViReplaceEndOfGlob, arg);
        ViDeleteEndOfGlob(key, arg);
        if (_renderer.Current == Singleton.buffer.Length - 1)
            ViInsertWithAppend(key, arg);
        else
            ViInsertMode(key, arg);
    }

    private static void ReplaceChar(ConsoleKeyInfo? key, object arg)
    {
        Singleton._groupUndoHelper.StartGroup(ReplaceChar, arg);
        ViInsertMode(key, arg);
        DeleteChar(key, arg);
    }

    /// <summary>
    ///     Replaces the current character with the next character typed.
    /// </summary>
    private static void ReplaceCharInPlace(ConsoleKeyInfo? key, object arg)
    {
        var nextKey = ReadKey();
        if (Singleton.buffer.Length > 0 && nextKey.KeyStr.Length == 1)
        {
            Singleton.StartEditGroup();
            Singleton.SaveEditItem(EditItemDelete.Create(
                Singleton.buffer[_renderer.Current].ToString(),
                _renderer.Current,
                ReplaceCharInPlace,
                arg,
                false));

            Singleton.SaveEditItem(EditItemInsertString.Create(nextKey.KeyStr, _renderer.Current));
            Singleton.EndEditGroup();

            Singleton.buffer[_renderer.Current] = nextKey.KeyChar;
            _renderer.Render();
        }
        else
        {
            Ding();
        }
    }

    /// <summary>
    ///     Deletes until given character.
    /// </summary>
    public static void ViReplaceToChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViReplaceToChar(keyChar, key, arg);
    }

    private static void ViReplaceToChar(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        var initialCurrent = _renderer.Current;

        Singleton._groupUndoHelper.StartGroup(ReplaceChar, arg);
        ViCharacterSearcher.Set(keyChar, false, false);
        if (ViCharacterSearcher.SearchDelete(keyChar, arg, false,
                (_key, _arg) => ViReplaceToChar(keyChar, _key, _arg)))
        {
            if (_renderer.Current < initialCurrent || _renderer.Current >= Singleton.buffer.Length)
                ViInsertWithAppend(key, arg);
            else
                ViInsertMode(key, arg);
        }
    }

    /// <summary>
    ///     Replaces until given character.
    /// </summary>
    public static void ViReplaceToCharBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViReplaceToCharBack(keyChar, key, arg);
    }

    private static void ViReplaceToCharBack(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._groupUndoHelper.StartGroup(ReplaceChar, arg);
        if (ViCharacterSearcher.SearchBackwardDelete(keyChar, arg, false,
                (_key, _arg) => ViReplaceToCharBack(keyChar, _key, _arg))) ViInsertMode(key, arg);
    }

    /// <summary>
    ///     Replaces until given character.
    /// </summary>
    public static void ViReplaceToBeforeChar(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViReplaceToBeforeChar(keyChar, key, arg);
    }

    private static void ViReplaceToBeforeChar(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._groupUndoHelper.StartGroup(ReplaceChar, arg);
        ViCharacterSearcher.Set(keyChar, false, true);
        if (ViCharacterSearcher.SearchDelete(keyChar, arg, true,
                (_key, _arg) => ViReplaceToBeforeChar(keyChar, _key, _arg))) ViInsertMode(key, arg);
    }

    /// <summary>
    ///     Replaces until given character.
    /// </summary>
    public static void ViReplaceToBeforeCharBackward(ConsoleKeyInfo? key = null, object arg = null)
    {
        var keyChar = ReadKey().KeyChar;
        ViReplaceToBeforeCharBack(keyChar, key, arg);
    }

    private static void ViReplaceToBeforeCharBack(char keyChar, ConsoleKeyInfo? key = null, object arg = null)
    {
        Singleton._groupUndoHelper.StartGroup(ReplaceChar, arg);
        if (ViCharacterSearcher.SearchBackwardDelete(keyChar, arg, true,
                (_key, _arg) => ViReplaceToBeforeCharBack(keyChar, _key, _arg))) ViInsertMode(key, arg);
    }
}