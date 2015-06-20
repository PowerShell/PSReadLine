/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell
{
    namespace Internal
    {
        /// <summary/>
        [SuppressMessage("Microsoft.MSInternal", "CA903:InternalNamespaceShouldNotContainPublicTypes")]
        public interface IPSConsoleReadLineMockableMethods
        {
            /// <summary/>
            ConsoleKeyInfo ReadKey();
            /// <summary/>
            bool KeyAvailable();
            /// <summary/>
            void Ding();
            /// <summary/>
            CommandCompletion CompleteInput(string input, int cursorIndex, Hashtable options, System.Management.Automation.PowerShell powershell);
            /// <summary/>
            bool RunspaceIsRemote(Runspace runspace);
        }
    }

    /// <summary/>
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Insert a character at the current position.  Supports undo.
        /// </summary>
        /// <param name="c">Character to insert</param>
        public static void Insert(char c)
        {
            _singleton.SaveEditItem(EditItemInsertChar.Create(c, _singleton._current));

            // Use Append if possible because Insert at end makes StringBuilder quite slow.
            if (_singleton._current == _singleton._buffer.Length)
            {
                _singleton._buffer.Append(c);
            }
            else
            {
                _singleton._buffer.Insert(_singleton._current, c);
            }
            _singleton._current += 1;
            _singleton.Render();
        }

        /// <summary>
        /// Insert a string at the current position.  Supports undo.
        /// </summary>
        /// <param name="s">String to insert</param>
        public static void Insert(string s)
        {
            _singleton.SaveEditItem(EditItemInsertString.Create(s, _singleton._current));

            // Use Append if possible because Insert at end makes StringBuilder quite slow.
            if (_singleton._current == _singleton._buffer.Length)
            {
                _singleton._buffer.Append(s);
            }
            else
            {
                _singleton._buffer.Insert(_singleton._current, s);
            }
            _singleton._current += s.Length;
            _singleton.Render();
        }

        /// <summary>
        /// Delete some text at the given position.  Supports undo.
        /// </summary>
        /// <param name="start">The start position to delete</param>
        /// <param name="length">The length to delete</param>
        public static void Delete(int start, int length)
        {
            Replace(start, length, null);
        }

        /// <summary>
        /// Replace some text at the given position.  Supports undo.
        /// </summary>
        /// <param name="start">The start position to replace</param>
        /// <param name="length">The length to replace</param>
        /// <param name="replacement">The replacement text</param>
        public static void Replace(int start, int length, string replacement)
        {
            if (start < 0 || start > _singleton._buffer.Length)
            {
                throw new ArgumentException(PSReadLineResources.StartOutOfRange, "start");
            }
            if (length > (_singleton._buffer.Length - start))
            {
                throw new ArgumentException(PSReadLineResources.ReplacementLengthTooBig, "length");
            }

            _singleton.StartEditGroup();
            var str = _singleton._buffer.ToString(start, length);
            _singleton.SaveEditItem(EditItemDelete.Create(str, start));
            _singleton._buffer.Remove(start, length);
            if (replacement != null)
            {
                _singleton.SaveEditItem(EditItemInsertString.Create(replacement, start));
                _singleton._buffer.Insert(start, replacement);
                _singleton._current = start + replacement.Length;
            }
            else
            {
                _singleton._current = start;
            }
            _singleton.EndEditGroup();
            _singleton.Render();
        }

        /// <summary>
        /// Get the state of the buffer - the current input and the position of the cursor
        /// </summary>
        public static void GetBufferState(out string input, out int cursor)
        {
            input = _singleton._buffer.ToString();
            cursor = _singleton._current;
        }

        /// <summary>
        /// Get the state of the buffer - the ast, tokens, errors, and position of the cursor
        /// </summary>
        public static void GetBufferState(out Ast ast, out Token[] tokens, out ParseError[] parseErrors, out int cursor)
        {
            _singleton.ParseInput();
            ast = _singleton._ast;
            tokens = _singleton._tokens;
            parseErrors = _singleton._parseErrors;
            cursor = _singleton._current;
        }

        /// <summary>
        /// Get the selection state of the buffer
        /// </summary>
        /// <param name="start">The start of the current selection or -1 if nothing is selected.</param>
        /// <param name="length">The length of the current selection or -1 if nothing is selected.</param>
        public static void GetSelectionState(out int start, out int length)
        {
            if (_singleton._visualSelectionCommandCount == 0)
            {
                start = -1;
                length = -1;
            }
            else
            {
                _singleton.GetRegion(out start, out length);
            }
        }

        /// <summary>
        /// Set the position of the cursor.
        /// </summary>
        public static void SetCursorPosition(int cursor)
        {
            if (cursor > _singleton._buffer.Length)
            {
                cursor = _singleton._buffer.Length;
            }
            else if (cursor < 0)
            {
                cursor = 0;
            }

            _singleton._current = cursor;
            _singleton.PlaceCursor();
        }

        /// <summary>
        /// A helper method when your function expects an optional int argument (e.g. from DigitArgument)
        /// If there is not argument (it's null), returns true and sets numericArg to derfaultNumericArg.
        /// Dings and returns false if the argument is not an int (no conversion is attempted)
        /// Otherwise returns true, and numericArg has the result.
        /// </summary>
        public static bool TryGetArgAsInt(object arg, out int numericArg, int defaultNumericArg)
        {
            if (arg == null)
            {
                numericArg = defaultNumericArg;
                return true;
            }

            if (arg is int)
            {
                numericArg = (int)arg;
                return true;
            }

            Ding();
            numericArg = 0;
            return false;
        }
    }
}
