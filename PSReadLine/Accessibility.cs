/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.Internal
{
    internal class Accessibility
    {
        internal static bool IsScreenReaderActive()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return IsAnyWindowsScreenReaderEnabled();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return IsVoiceOverEnabled();
            }

            // TODO: Support Linux per https://code.visualstudio.com/docs/configure/accessibility/accessibility
            return false;
        }

        private static bool IsAnyWindowsScreenReaderEnabled()
        {
            // The supposedly official way to check for a screen reader on
            // Windows is SystemParametersInfo(SPI_GETSCREENREADER, ...) but it
            // doesn't detect the in-box Windows Narrator and is otherwise known
            // to be problematic.
            //
            // Unfortunately, the alternative method used by Electron and
            // Chromium, where the relevant screen reader libraries (modules)
            // are checked for does not work in the context of PowerShell
            // because it relies on those applications injecting themselves into
            // the app. Which they do not because PowerShell is not a windowed
            // app, so we're stuck using the known-to-be-buggy way.
            bool spiScreenReader = false;
            PlatformWindows.SystemParametersInfo(PlatformWindows.SPI_GETSCREENREADER, 0, ref spiScreenReader, 0);
            if (spiScreenReader)
            {
                return true;
            }

            // At least we can correctly check for Windows Narrator using the
            // NarratorRunning mutex. Windows Narrator is mostly not broken with
            // PSReadLine, not in the way that NVDA and VoiceOver are.
            if (PlatformWindows.IsMutexPresent("NarratorRunning"))
            {
                return true;
            }

            return false;
        }

        private static bool IsVoiceOverEnabled()
        {
            try
            {
                // Use the 'defaults' command to check if VoiceOver is enabled
                // This checks the com.apple.universalaccess preference for voiceOverOnOffKey
                ProcessStartInfo startInfo = new()
                {
                    FileName = "defaults",
                    Arguments = "read com.apple.universalaccess voiceOverOnOffKey",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(startInfo);
                process.WaitForExit(250);
                if (process.HasExited && process.ExitCode == 0)
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    // VoiceOver is enabled if the value is 1
                    return output == "1";
                }
            }
            catch
            {
                // If we can't determine the status, assume VoiceOver is not enabled
            }

            return false;
        }
    }
}
