/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

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

            ExecuteOnStaThread(() => SetTextImpl(text));
        }

        public static void SetRtf(string text)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PSConsoleReadLine.Ding();
                return;
            }

            ExecuteOnStaThread(() => SetRtfImpl(text));
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

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

        private static bool GetTextImpl(out string text)
        {
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    var data = GetClipboardData(13 /*CF_UNICODETEXT*/);
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

        private static bool SetTextImpl(string text)
        {
            return false;
        }

        private static bool SetRtfImpl(string text)
        {
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

