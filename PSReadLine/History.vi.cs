/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Replace the current input with the 'previous' item from PSReadline history.
        /// </summary>
        public static void ViPreviousHistory(ConsoleKeyInfo? key = null, object arg = null)
        {
            ViCommandMode(key, arg);
            PreviousHistory(key, arg);
            ViInsertMode(key, arg);
        }
    }
}