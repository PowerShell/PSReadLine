/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Subsystem.Prediction;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    namespace Internal
    {
#pragma warning disable 1591

        [SuppressMessage("Microsoft.MSInternal", "CA903:InternalNamespaceShouldNotContainPublicTypes")]
        public interface IPSConsoleReadLineMockableMethods
        {
            void Ding();

            CommandCompletion CompleteInput(string input, int cursorIndex, Hashtable options,
                System.Management.Automation.PowerShell powershell);

            bool RunspaceIsRemote(Runspace runspace);
            Task<List<PredictionResult>> PredictInputAsync(Ast ast, Token[] tokens);
            void OnCommandLineAccepted(IReadOnlyList<string> history);
            void OnCommandLineExecuted(string commandLine, bool success);
            void OnSuggestionDisplayed(Guid predictorId, uint session, int countOrIndex);
            void OnSuggestionAccepted(Guid predictorId, uint session, string suggestionText);
            void RenderFullHelp(string content, string regexPatternToScrollTo);
            object GetDynamicHelpContent(string commandName, string parameterName, bool isFullHelp);
        }

        [SuppressMessage("Microsoft.MSInternal", "CA903:InternalNamespaceShouldNotContainPublicTypes")]
        public interface IConsole
        {
            bool KeyAvailable { get; }
            int CursorLeft { get; set; }
            int CursorTop { get; set; }
            int CursorSize { get; set; }
            bool CursorVisible { get; set; }
            int BufferWidth { get; set; }
            int BufferHeight { get; set; }
            int WindowWidth { get; set; }
            int WindowHeight { get; set; }
            int WindowTop { get; set; }
            ConsoleColor BackgroundColor { get; set; }
            ConsoleColor ForegroundColor { get; set; }
            Encoding OutputEncoding { get; set; }
            ConsoleKeyInfo ReadKey();
            void SetWindowPosition(int left, int top);
            void SetCursorPosition(int left, int top);
            void WriteLine(string s);
            void Write(string s);
            void BlankRestOfLine();
        }

#pragma warning restore 1591
    }

    /// <summary />
    public partial class PSConsoleReadLine
    {
        /// <summary>
        ///     Insert a character at the current position.  Supports undo.
        /// </summary>
        /// <param name="c">Character to insert</param>
        public static void Insert(char c)
        {
            Singleton.SaveEditItem(EditItemInsertChar.Create(c, _renderer.Current));

            // Use Append if possible because Insert at end makes StringBuilder quite slow.
            if (_renderer.Current == Singleton.buffer.Length)
                Singleton.buffer.Append(c);
            else
                Singleton.buffer.Insert(_renderer.Current, c);
            _renderer.Current = _renderer.Current + 1;
            _renderer.Render();
        }

        /// <summary>
        ///     Insert a string at the current position.  Supports undo.
        /// </summary>
        /// <param name="s">String to insert</param>
        public static void Insert(string s)
        {
            Singleton.SaveEditItem(EditItemInsertString.Create(s, _renderer.Current));

            // Use Append if possible because Insert at end makes StringBuilder quite slow.
            if (_renderer.Current == Singleton.buffer.Length)
                Singleton.buffer.Append(s);
            else
                Singleton.buffer.Insert(_renderer.Current, s);
            _renderer.Current = _renderer.Current + s.Length;
            _renderer.Render();
        }

        /// <summary>
        ///     Delete some text at the given position.  Supports undo.
        /// </summary>
        /// <param name="start">The start position to delete</param>
        /// <param name="length">The length to delete</param>
        public static void Delete(int start, int length)
        {
            Replace(start, length, null);
        }

        /// <summary>
        ///     Replace some text at the given position.  Supports undo.
        /// </summary>
        /// <param name="start">The start position to replace</param>
        /// <param name="length">The length to replace</param>
        /// <param name="replacement">The replacement text</param>
        /// <param name="instigator">The action that initiated the replace (used for undo)</param>
        /// <param name="instigatorArg">The argument to the action that initiated the replace (used for undo)</param>
        public static void Replace(int start, int length, string replacement,
            Action<ConsoleKeyInfo?, object> instigator = null, object instigatorArg = null)
        {
            if (start < 0 || start > Singleton.buffer.Length)
                throw new ArgumentException(PSReadLineResources.StartOutOfRange, nameof(start));
            if (length > Singleton.buffer.Length - start || length < 0)
                throw new ArgumentException(PSReadLineResources.ReplacementLengthInvalid, nameof(length));

            var useEditGroup = Singleton._editGroupStart == -1;

            if (useEditGroup) Singleton.StartEditGroup();

            var str = Singleton.buffer.ToString(start, length);
            Singleton.SaveEditItem(EditItemDelete.Create(str, start));
            Singleton.buffer.Remove(start, length);
            if (replacement != null)
            {
                Singleton.SaveEditItem(EditItemInsertString.Create(replacement, start));
                Singleton.buffer.Insert(start, replacement);
                _renderer.Current = start + replacement.Length;
            }
            else
            {
                _renderer.Current = start;
            }

            if (useEditGroup)
            {
                Singleton.EndEditGroup(instigator, instigatorArg); // Instigator is needed for VI undo
                _renderer.Render();
            }
        }

        /// <summary>
        ///     Get the state of the buffer - the current input and the position of the cursor
        /// </summary>
        public static void GetBufferState(out string input, out int cursor)
        {
            input = Singleton.buffer.ToString();
            cursor = _renderer.Current;
        }

        /// <summary>
        ///     Get the state of the buffer - the ast, tokens, errors, and position of the cursor
        /// </summary>
        public static void GetBufferState(out Ast ast, out Token[] tokens, out ParseError[] parseErrors, out int cursor)
        {
            var tempQualifier = Singleton;
            tempQualifier.buffer.ToString();
            ast = Singleton.RLAst;
            tokens = Singleton.Tokens;
            parseErrors = Singleton.ParseErrors;
            cursor = _renderer.Current;
        }

        /// <summary>
        ///     Get the selection state of the buffer
        /// </summary>
        /// <param name="start">The start of the current selection or -1 if nothing is selected.</param>
        /// <param name="length">The length of the current selection or -1 if nothing is selected.</param>
        public static void GetSelectionState(out int start, out int length)
        {
            if (Singleton._visualSelectionCommandCount == 0)
            {
                start = -1;
                length = -1;
            }
            else
            {
                _renderer.GetRegion(out start, out length);
            }
        }

        /// <summary>
        ///     Set the position of the cursor.
        /// </summary>
        public static void SetCursorPosition(int cursor)
        {
            if (cursor > Singleton.buffer.Length + ViEndOfLineFactor)
                cursor = Singleton.buffer.Length + ViEndOfLineFactor;
            if (cursor < 0) cursor = 0;

            _renderer.MoveCursor(cursor);
        }

        /// <summary>
        ///     A helper method when your function expects an optional int argument (e.g. from DigitArgument)
        ///     If there is not argument (it's null), returns true and sets numericArg to defaultNumericArg.
        ///     Dings and returns false if the argument is not an int (no conversion is attempted)
        ///     Otherwise returns true, and numericArg has the result.
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
                numericArg = (int) arg;
                return true;
            }

            Ding();
            numericArg = 0;
            return false;
        }
    }
}