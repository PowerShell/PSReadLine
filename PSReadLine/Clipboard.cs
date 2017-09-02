/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.PowerShell.Internal
{
    static class Clipboard
    {
        public static string GetText()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PSConsoleReadLine.Ding();
                return "";
            }

            string clipboardText = "";
            ExecuteOnStaThread(() => GetTextImpl(out clipboardText));

            return clipboardText;
        }

        public static void SetText(string text)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PSConsoleReadLine.Ding();
                return;
            }

            ExecuteOnStaThread(() => SetClipboardData(text, CF_UNICODETEXT));
        }

        public static void SetRtf(string text)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PSConsoleReadLine.Ding();
                return;
            }

            if (CF_RTF == 0)
            {
                CF_RTF = RegisterClipboardFormat("Rich Text Format");
            }

            ExecuteOnStaThread(() => SetClipboardData(text, CF_RTF));
        }

        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint GMEM_ZEROINIT = 0x0040;
        const uint GHND = GMEM_MOVEABLE | GMEM_ZEROINIT;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll", SetLastError=true)]
        static extern uint RegisterClipboardFormat(string lpszFormat);

        private const uint CF_UNICODETEXT = 13;
        private static uint CF_RTF;

        private static bool GetTextImpl(out string text)
        {
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    var data = GetClipboardData(CF_UNICODETEXT);
                    if (data != IntPtr.Zero)
                    {
                        text = Marshal.PtrToStringUni(data);
                        return true;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                CloseClipboard();
            }

            text = "";
            return false;
        }

        private static bool SetClipboardData(string text, uint format)
        {
            IntPtr hGlobal = IntPtr.Zero;
            IntPtr data = IntPtr.Zero;

            try
            {
                if (!OpenClipboard(IntPtr.Zero)) return false;

                uint bytes;
                if (format == CF_RTF)
                {
                    bytes = (uint)(text.Length + 1);
                    data = Marshal.StringToHGlobalAnsi(text);
                }
                else if (format == CF_UNICODETEXT)
                {
                    bytes = (uint) ((text.Length + 1) * 2);
                    data = Marshal.StringToHGlobalUni(text);
                }
                else
                {
                    // Not yet supported format.
                    return false;
                }

                if (data == IntPtr.Zero) return false;

                hGlobal = GlobalAlloc(GHND, (UIntPtr) bytes);
                if (hGlobal == IntPtr.Zero) return false;

                IntPtr dataCopy = GlobalLock(hGlobal);
                if (dataCopy == IntPtr.Zero) return false;
                CopyMemory(dataCopy, data, bytes);
                GlobalUnlock(hGlobal);

                if (SetClipboardData(format, hGlobal) != IntPtr.Zero)
                {
                    // The clipboard owns this memory now, so don't free it.
                    hGlobal = IntPtr.Zero;
                }
            }
            catch
            {
            }
            finally
            {
                if (data != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(data);
                }
                if (hGlobal != IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                }

                CloseClipboard();
            }

            return false;
        }

        private static void ExecuteOnStaThread(Func<bool> action)
        {
            const int retryCount = 5;
            int tries = 0;

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                while (tries++ < retryCount && !action())
                    ;
                return;
            }

            Exception exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    while (tries++ < retryCount && !action())
                        ;
                }
                catch (Exception e)
                {
                    exception = e;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                throw exception;
            }
        }
    }
}
