using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell
{
    internal static class WindowsKeyboardLayoutUtil
    {
        /// <remarks>
        /// <para>
        ///     This method helps to find the active keyboard layout in a terminal process that controls the current
        ///     console application. The terminal process is not always the direct parent of the current process, but
        ///     may be higher in the process tree in case PowerShell is a child of some other console process.
        /// </para>
        /// <para>
        ///     Currently, we check up to 20 parent processes to see if their main window (as determined by the
        ///     <see cref="Process.MainWindowHandle"/>) is visible.
        /// </para>
        /// <para>
        ///     If this method returns <c>null</c>, it means it was unable to find the parent terminal process, and so
        ///     you have to call the <see cref="GetConsoleKeyboardLayoutFallback"/>, which is known to not work properly
        ///     in certain cases, as documented by https://github.com/PowerShell/PSReadLine/issues/1393
        /// </para>
        /// </remarks>
        public static IntPtr? GetConsoleKeyboardLayout()
        {
            // Define a limit not get stuck in case processed form a loop (possible in case pid reuse).
            const int iterationLimit = 20;

            var pbi = new PROCESS_BASIC_INFORMATION();
            var process = Process.GetCurrentProcess();
            for (var i = 0; i < iterationLimit; ++i)
            {
                var isVisible = IsWindowVisible(process.MainWindowHandle);
                if (!isVisible)
                {
                    // Main process window is invisible. This is not (likely) a terminal process.
                    var status = NtQueryInformationProcess(process.Handle, 0, ref pbi, Marshal.SizeOf(pbi), out var _);
                    if (status != 0 || pbi.InheritedFromUniqueProcessId == IntPtr.Zero)
                        break;

                    try
                    {
                        process = Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
                    }
                    catch (Exception)
                    {
                        // No access to the process, or the process is already dead. Either way, we cannot determine its
                        // keyboard layout.
                        return null;
                    }

                    continue;
                }

                var tid = GetWindowThreadProcessId(process.MainWindowHandle, out _);
                if (tid == 0) return null;
                return GetKeyboardLayout(tid);
            }

            return null;
        }

        public static IntPtr GetConsoleKeyboardLayoutFallback()
        {
            return GetKeyboardLayout(0);
        }

        [DllImport("User32.dll", SetLastError = true)]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("Ntdll.dll")]
        static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength,
            out int returnLength);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hwnd, out IntPtr proccess);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            internal IntPtr Reserved1;
            internal IntPtr PebBaseAddress;
            internal IntPtr Reserved2_0;
            internal IntPtr Reserved2_1;
            internal IntPtr UniqueProcessId;
            internal IntPtr InheritedFromUniqueProcessId;
        }
    }
}