using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    /// <summary>
    ///     Represents a named register.
    /// </summary>
    internal sealed class ViRegister
    {
        private readonly PSConsoleReadLine _singleton;

        /// <summary>
        ///     Initialize a new instance of the <see cref="ViRegister" /> class.
        /// </summary>
        /// <param name="singleton">
        ///     The <see cref="PSConsoleReadLine" /> object.
        ///     Used to hook into the undo / redo subsystem as part of
        ///     pasting the contents of the register into a buffer.
        /// </param>
        public ViRegister(PSConsoleReadLine singleton)
        {
            _singleton = singleton;
        }

        /// <summary>
        ///     Returns whether this register is empty.
        /// </summary>
        public bool IsEmpty
            => string.IsNullOrEmpty(RawText);

        /// <summary>
        ///     Returns whether this register contains
        ///     linewise yanked text.
        /// </summary>
        public bool HasLinewiseText { get; private set; }

        /// <summary>
        ///     Gets the raw text contained in the register
        /// </summary>
        public string RawText { get; private set; }

        /// <summary>
        ///     Records the entire buffer in the register.
        /// </summary>
        /// <param name="buffer"></param>
        public void Record(StringBuilder buffer)
        {
            Record(buffer, 0, buffer.Length);
        }

        /// <summary>
        ///     Records a piece of text in the register.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Record(StringBuilder buffer, int offset, int count)
        {
            Debug.Assert(
                offset >= 0 &&
                (buffer.Length == 0 || offset < buffer.Length)
            );
            Debug.Assert(offset + count <= buffer.Length);

            HasLinewiseText = false;
            RawText = buffer.ToString(offset, count);
        }

        /// <summary>
        ///     Records a block of lines in the register.
        /// </summary>
        /// <param name="text"></param>
        public void LinewiseRecord(string text)
        {
            HasLinewiseText = true;
            RawText = text;
        }

        public int PasteAfter(StringBuilder buffer, int position)
        {
            if (IsEmpty) return position;

            if (HasLinewiseText)
            {
                var text = RawText;

                if (text[0] != '\n') text = '\n' + text;

                // paste text after the next line

                var pastePosition = -1;
                var newCursorPosition = position;

                for (var index = position; index < buffer.Length; index++)
                    if (buffer[index] == '\n')
                    {
                        pastePosition = index;
                        break;
                    }

                if (pastePosition == -1) pastePosition = buffer.Length;

                InsertAt(buffer, text, pastePosition, position);

                return pastePosition + 1;
            }

            if (position < buffer.Length) position += 1;

            InsertAt(buffer, RawText, position, position);
            position += RawText.Length - 1;

            return position;
        }

        public int PasteBefore(StringBuilder buffer, int position)
        {
            if (HasLinewiseText)
            {
                // currently, in Vi Edit Mode, the cursor may be positioned
                // exactly one character past the end of the buffer.

                // we adjust the current position to prevent a crash

                position = Math.Max(0, Math.Min(position, buffer.Length - 1));

                var text = RawText;

                if (text[0] == '\n') text = text.Remove(0, 1);

                if (text[text.Length - 1] != '\n') text += '\n';

                // paste text before the current line

                var previousLinePos = -1;

                if (buffer.Length > 0)
                    for (var index = position; index >= 0; index--)
                        if (buffer[index] == '\n')
                        {
                            previousLinePos = index + 1;
                            break;
                        }

                if (previousLinePos == -1) previousLinePos = 0;

                InsertBefore(buffer, text, previousLinePos, position);

                return previousLinePos;
            }

            InsertAt(buffer, RawText, position, position);
            return position + RawText.Length - 1;
        }

        private void InsertBefore(StringBuilder buffer, string text, int pastePosition, int position)
        {
            RecordPaste(text, pastePosition, position);
            buffer.Insert(pastePosition, text);
        }

        private void InsertAt(StringBuilder buffer, string text, int pastePosition, int position)
        {
            RecordPaste(text, pastePosition, position);

            // Use Append if possible because Insert at end makes StringBuilder quite slow.
            if (pastePosition == buffer.Length)
                buffer.Append(text);
            else
                buffer.Insert(pastePosition, text);
        }

        /// <summary>
        ///     Called to record the paste operation in the undo subsystem.
        /// </summary>
        /// <param name="text">
        ///     The text being pasted.
        /// </param>
        /// <param name="position">
        ///     The position in the buffer at
        ///     which the pasted text will be inserted.
        /// </param>
        /// <param name="anchor">
        ///     The recorded position in the buffer
        ///     from which the paste operation originates.
        ///     This is usually the same as Position, but
        ///     not always. For instance, when pasting a
        ///     linewise selection before the current line,
        ///     the Anchor is the cursor position, whereas
        ///     the Position is the beginning of the previous line.
        /// </param>
        private void RecordPaste(string text, int position, int anchor)
        {
            if (_singleton != null)
            {
                var editItem = EditItemInsertLines.Create(
                    text,
                    position,
                    anchor
                );

                _singleton.SaveEditItem(editItem);
            }
        }

#if DEBUG
        public override string ToString()
        {
            var text = RawText.Replace("\n", "\\n");
            return (HasLinewiseText ? "line: " : "") + "\"" + text + "\"";
        }
#endif
    }
}