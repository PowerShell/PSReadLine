/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Ends the current edit group, if needed, and invokes TabCompleteNext.
        /// </summary>
        public static void ViTabCompleteNext(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._editGroupStart >= 0)
            {
                _singleton._groupUndoHelper.EndGroup();
            }
            TabCompleteNext(key, arg);
        }

        /// <summary>
        /// Ends the current edit group, if needed, and invokes TabCompletePrevious.
        /// </summary>
        public static void ViTabCompletePrevious(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._editGroupStart >= 0)
            {
                _singleton._groupUndoHelper.EndGroup();
            }
            TabCompletePrevious(key, arg);
        }
    }
}
