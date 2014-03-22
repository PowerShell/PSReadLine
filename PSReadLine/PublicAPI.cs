using System;
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSConsoleUtilities
{
    namespace Internal
    {
        public interface IPSConsoleReadLineMockableMethods
        {
            ConsoleKeyInfo ReadKey();
            bool KeyAvailable();
            void Ding();
            CommandCompletion CompleteInput(string input, int cursorIndex, Hashtable options, PowerShell powershell);
        }
    }

    public partial class PSConsoleReadLine : IModuleAssemblyInitializer
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
        public static void Replace( int start, int length, string replacement, Action<ConsoleKeyInfo?, object> instigator = null, object instigatorArg = null )
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
            _singleton.EndEditGroup(instigator, instigatorArg); // Instigator is needed for VI undo
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
        /// Set the position of the cursor.
        /// </summary>
        public static void SetCursorPosition(int cursor)
        {
            if (cursor > _singleton._buffer.Length)
            {
                cursor = _singleton._buffer.Length;
            }

            _singleton._current = cursor;
            _singleton.PlaceCursor();
        }

        void IModuleAssemblyInitializer.OnImport()
        {
            // The purpose of the PSReadline module is to give a better experience in
            // console-based hosts. If the host is not console-based, PSReadline can't do
            // anything. Rather than having a list of hosts which we know are
            // console-based, let's just check to see if the current process has a console
            // associated with it. It's a heuristic, but it should be good enough.
            IntPtr hwnd = NativeMethods.GetConsoleWindow();
            if (IntPtr.Zero == hwnd)
            {
                throw new NotSupportedException(PSReadLineResources.HostNotSupported);
            }
            else
            {
                // Just because it has a console window doesn't mean it's a console host.
                // For instance, if you run "ping" in the ISE, it will acquire a hidden
                // console window. Let's check to see if the window is hidden.
                if (!NativeMethods.IsWindowVisible(hwnd))
                {
                    // This is just a heuristic. For instance somebody could create a
                    // PowerShell.exe process with a hidden window. If we decide that we
                    // want to handle such cases, an alternative to a hard-coded list of
                    // known-good hosts is to keep the list in a separate text file, which
                    // end users could edit if necessary.
                    throw new NotSupportedException(PSReadLineResources.HostNotSupported);
                }
            }
        }
    }
}
