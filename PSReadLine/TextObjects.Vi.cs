using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        internal enum TextObjectOperation
        {
            None,
            Change,
            Delete,
        }

        internal enum TextObjectSpan
        {
            None,
            Around,
            Inner,
        }

        private TextObjectOperation _textObjectOperation = TextObjectOperation.None;
        private TextObjectSpan _textObjectSpan = TextObjectSpan.None;

        private readonly IDictionary<TextObjectOperation, IDictionary<TextObjectSpan, KeyHandler>> _textObjectHandlers
            = new Dictionary<TextObjectOperation, IDictionary<TextObjectSpan, KeyHandler>>
            {
                {
                    TextObjectOperation.Delete,
                    new Dictionary<TextObjectSpan, KeyHandler>
                    {
                        {TextObjectSpan.Inner, MakeKeyHandler(ViDeleteInnerWord, "ViDeleteInnerWord")}
                    }
                }
            };

        private void ViChordDeleteTextObject(ConsoleKeyInfo? key = null, object arg = null)
        {
            _textObjectOperation = TextObjectOperation.Delete;
            ViChordTextObject(key, arg);
        }

        private void ViChordTextObject(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!key.HasValue)
            {
                ResetTextObjectState();
                throw new ArgumentNullException(nameof(key));
            }

            _textObjectSpan = GetRequestedTextObjectSpan(key.Value);

            // handle text object

            var textObjectKey = ReadKey();
            if (_viChordTextObjectsTable.TryGetValue(textObjectKey, out _))
            {
                _singleton.ProcessOneKey(textObjectKey, _viChordTextObjectsTable, ignoreIfNoAction: true, arg: arg);
            }
            else
            {
                ResetTextObjectState();
                Ding();
            }
        }

        private TextObjectSpan GetRequestedTextObjectSpan(ConsoleKeyInfo key)
        {
            if (key.KeyChar == 'i')
            {
                return TextObjectSpan.Inner;
            }
            else if (key.KeyChar == 'a')
            {
                return TextObjectSpan.Around;
            }
            else
            {
                System.Diagnostics.Debug.Assert(false);
                throw new NotSupportedException();
            }
        }

        private static void ViHandleTextObject(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (
                !_singleton._textObjectHandlers.TryGetValue(_singleton._textObjectOperation, out var textObjectHandler) ||
                !textObjectHandler.TryGetValue(_singleton._textObjectSpan, out var handler)
            )
            {
                ResetTextObjectState();
                Ding();
                return;
            }

            handler.Action(key, arg);
        }

        private static void ResetTextObjectState()
        {
            _singleton._textObjectOperation = TextObjectOperation.None;
            _singleton._textObjectSpan = TextObjectSpan.None;
        }

        private static void ViDeleteInnerWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            var delimiters = _singleton.Options.WordDelimiters;

            if (!TryGetArgAsInt(arg, out var numericArg, 1))
                return;

            if (_singleton._buffer.Length == 0)
            {
                if (numericArg > 1)
                {
                    Ding();
                }
                return;
            }

            // unless at the end of the buffer a single delete word should not delete backwards
            // so if the cursor is on an empty line, do nothing

            if (
                numericArg == 1 &&
                _singleton._current < _singleton._buffer.Length &&
                _singleton._buffer.IsLogigalLineEmpty(_singleton._current)
            )
            {
                return;
            }

            var start = _singleton._buffer.ViFindBeginningOfWordObjectBoundary(_singleton._current, delimiters);
            var end = _singleton._current;

            // attempting to find a valid position for multiple words
            // if no valid position is found, this is a no-op

            {
                while (numericArg-- > 0 && end < _singleton._buffer.Length)
                {
                    end = _singleton._buffer.ViFindBeginningOfNextWordObjectBoundary(end, delimiters);
                }

                // attempting to delete too many words should ding

                if (numericArg > 0)
                {
                    Ding();
                    return;
                }
            }

            if (end > 0 && _singleton._buffer.IsAtEndOfBuffer(end - 1) && _singleton._buffer.InWord(end - 1, delimiters))
            {
                _singleton._shouldAppend = true;
            }

            _singleton.RemoveTextToViRegister(start, end - start);
            _singleton.AdjustCursorPosition(start);
            _singleton.Render();
        }

        /// <summary>
        /// Attempt to set the cursor at the specified position.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        private int AdjustCursorPosition(int position)
        {
            // this method might prove useful in a more general case

            if (_buffer.Length == 0)
            {
                _current = 0;
                return 0;
            }

            var maxPosition = _buffer[_buffer.Length - 1] == '\n'
                ? _buffer.Length
                : _buffer.Length - 1
                ;

            var newCurrent = Math.Min(position, maxPosition);

            var beginning = GetBeginningOfLinePos(newCurrent);

            if (newCurrent < _buffer.Length && _buffer[newCurrent] == '\n' && (newCurrent + ViEndOfLineFactor > beginning))
            {
                newCurrent += ViEndOfLineFactor;
            }

            _current = newCurrent;

            return newCurrent;
        }
    }
}
