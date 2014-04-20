using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Undo all previous edits for line.
        /// </summary>
        public static void UndoAll(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._undoEditIndex > 0)
            {
                while (_singleton._undoEditIndex > 0)
                {
                    _singleton._edits[_singleton._undoEditIndex - 1].Undo();
                    _singleton._undoEditIndex--;
                }
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }
    }
}
