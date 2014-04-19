using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        private static void ViReplaceUntilEsc(ConsoleKeyInfo? key, object arg)
        {
            if (_singleton._current >= _singleton._buffer.Length)
            {
                Ding();
                return;
            }

            int startingCursor = _singleton._current;
            int maxDeleteLength = _singleton._buffer.Length - _singleton._current;
            StringBuilder deletedStr = new StringBuilder();

            ConsoleKeyInfo nextKey = ReadKey();
            while (nextKey.Key != ConsoleKey.Escape && nextKey.Key != ConsoleKey.Enter)
            {
                if (nextKey.Key != ConsoleKey.Backspace && nextKey.KeyChar != '\u0000')
                {
                    if (_singleton._current >= _singleton._buffer.Length)
                    {
                        _singleton._buffer.Append(nextKey.KeyChar);
                    }
                    else
                    {
                        deletedStr.Append(_singleton._buffer[_singleton._current]);
                        _singleton._buffer[_singleton._current] = nextKey.KeyChar;
                    }
                    _singleton._current++;
                    _singleton.Render();
                }
                if (nextKey.Key == ConsoleKey.Backspace)
                {
                    if (_singleton._current == startingCursor)
                    {
                        Ding();
                    }
                    else
                    {
                        if (deletedStr.Length == _singleton._current - startingCursor)
                        {
                            _singleton._buffer[_singleton._current - 1] = deletedStr[deletedStr.Length - 1];
                            deletedStr.Remove(deletedStr.Length - 1, 1);
                        }
                        else
                        {
                            _singleton._buffer.Remove(_singleton._current - 1, 1);
                        }
                        _singleton._current--;
                        _singleton.Render();
                    }
                }
                nextKey = ReadKey();
            }

            if (_singleton._current > startingCursor)
            {
                _singleton.StartEditGroup();
                string insStr = _singleton._buffer.ToString(startingCursor, _singleton._current - startingCursor);
                _singleton.SaveEditItem(EditItemDelete.Create(deletedStr.ToString(), startingCursor));
                _singleton.SaveEditItem(EditItemInsertString.Create(insStr, startingCursor));
                _singleton.EndEditGroup();
            }

            if (nextKey.Key == ConsoleKey.Enter)
            {
                ViAcceptLine(nextKey);
            }
        }

        private static void ViReplaceBrace(ConsoleKeyInfo? key, object arg)
        {
            DeleteBrace(key, arg);
            ViInsertMode(key, arg);
        }

        private static void ViBackwardReplaceLineToFirstChar(ConsoleKeyInfo? key, object arg)
        {
            DeleteLineToFirstChar(key, arg);
            ViInsertMode(key, arg);
        }

        private static void ViBackwardReplaceLine(ConsoleKeyInfo? key, object arg)
        {
            BackwardDeleteLine(key, arg);
            ViInsertMode(key, arg);
        }

        private static void BackwardReplaceChar(ConsoleKeyInfo? key, object arg)
        {
            BackwardDeleteChar(key, arg);
            InsertCharacter(arg);
        }

        private static void ViBackwardReplaceWord(ConsoleKeyInfo? key, object arg)
        {
            BackwardDeleteWord(key, arg);
            ViInsertMode(key, arg);
        }

        private static void ViReplaceToEnd(ConsoleKeyInfo? key, object arg)
        {
            DeleteToEnd(key, arg);
            _singleton._current++;
            _singleton.PlaceCursor();
            ViInsertMode(key, arg);
        }

        private static void ViReplaceLine(ConsoleKeyInfo? key, object arg)
        {
            DeleteLine(key, arg);
            ViInsertMode(key, arg);
        }

        private static void ViReplaceWord(ConsoleKeyInfo? key, object arg)
        {
            DeleteWord(key, arg);
            if (_singleton._current < _singleton._buffer.Length - 1)
            {
                Insert(' ');
                _singleton._current--;
                _singleton.PlaceCursor();
            }
            ViInsertMode(key, arg);
        }

        private static void ReplaceChar(ConsoleKeyInfo? key, object arg)
        {
            DeleteChar(key, arg);
            InsertCharacter(arg);
        }

        /// <summary>
        /// Replaces the current character with the next character typed.
        /// </summary>
        private static void ReplaceCharInPlace(ConsoleKeyInfo? key, object arg)
        {
            ConsoleKeyInfo nextKey = ReadKey();
            if (nextKey.KeyChar > 0 && nextKey.Key != ConsoleKey.Escape && nextKey.Key != ConsoleKey.Enter)
            {
                _singleton.StartEditGroup();
                _singleton.SaveEditItem(EditItemDelete.Create(_singleton._buffer[_singleton._current].ToString(), _singleton._current));
                _singleton.SaveEditItem(EditItemInsertString.Create(nextKey.KeyChar.ToString(), _singleton._current));
                _singleton.EndEditGroup();

                _singleton._buffer[_singleton._current] = nextKey.KeyChar;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }
    }
}
