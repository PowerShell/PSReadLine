using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Windows;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        private string _clipboard = string.Empty;

        public static void PasteAfter(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (string.IsNullOrEmpty(_singleton._clipboard))
            {
                Ding();
                return;
            }

            _singleton.PasteAfterImpl();
        }

        public static void PasteBefore(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (string.IsNullOrEmpty(_singleton._clipboard))
            {
                Ding();
                return;
            }
            _singleton.PasteBeforeImpl();
        }

        private void PasteAfterImpl()
        {
            if (_current < _buffer.Length)
            {
                _current++;
            }
            Insert(_clipboard);
            _current--;
            Render();
        }

        private void PasteBeforeImpl()
        {
            Insert(_clipboard);
            _current--;
            Render();
        }

        private void SaveToClipboard(int startIndex, int length)
        {
            _clipboard = _buffer.ToString(startIndex, length);
        }
    }
}
