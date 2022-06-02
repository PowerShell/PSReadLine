/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.Internal
{
    internal class Accessibility
    {
        internal static bool IsScreenReaderActive()
        {
            var returnValue = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                PlatformWindows.SystemParametersInfo(PlatformWindows.SPI_GETSCREENREADER, 0, ref returnValue, 0);

            return returnValue;
        }
    }
}