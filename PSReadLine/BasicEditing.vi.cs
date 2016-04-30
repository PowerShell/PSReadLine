using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {

        private void ViDeleteCharImpl(int qty, bool orExit)
        {
            qty = Math.Min(qty, _singleton._buffer.Length + 1 + ViEndOfLineFactor - _singleton._current);

            if (_visualSelectionCommandCount > 0)
            {
                int start, length;
                GetRegion(out start, out length);
                Delete(start, length);
                return;
            }

            if (_buffer.Length > 0)
            {
                if (_current < _buffer.Length)
                {
                    SaveEditItem(EditItemDelete.Create(_buffer.ToString(_current, qty), _current, DeleteChar, qty));
                    SaveToClipboard(_current, qty);
                    _buffer.Remove(_current, qty);
                    if (_current > _buffer.Length)
                    {
                        _current = Math.Max(0, _buffer.Length);
                    }
                    Render();
                }
            }
            else if (orExit)
            {
                throw new ExitException();
            }
        }

        /// <summary>
        /// Delete the character under the cursor.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void ViDeleteChar(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = (arg is int) ? (int)arg : 1;

            _singleton.ViDeleteCharImpl(qty, orExit: false);
        }
    }
}
