﻿/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.PowerShell.Internal
{
    static class Clipboard
    {
        private static bool? _clipboardSupported;
        // Used if an external clipboard is not available, e.g. if xclip is missing.
        // This is useful for testing in CI as well.
        private static string _internalClipboard;

        private static string StartProcess(
            string tool,
            string args,
            string stdin = ""
        )
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.FileName = tool;
            startInfo.Arguments = args;
            string stdout;

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                try
                {
                    process.Start();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    _clipboardSupported = false;
                    PSConsoleReadLine.Ding();
                    return "";
                }

                if (stdin != "")
                {
                    process.StandardInput.Write(stdin);
                    process.StandardInput.Close();
                }
                stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit(250);

                _clipboardSupported = process.ExitCode == 0;
            }

            return stdout;
        }

        public static string GetText()
        {
            if (_clipboardSupported == false)
            {
                PSConsoleReadLine.Ding();
                return _internalClipboard ?? "";
            }

            string tool = "";
            string args = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string clipboardText = "";
                ExecuteOnStaThread(() => GetTextImpl(out clipboardText));
                return clipboardText;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                tool = "xclip";
                args = "-selection clipboard -out";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                tool = "pbpaste";
            }
            else
            {
                _clipboardSupported = false;
                PSConsoleReadLine.Ding();
                return "";
            }

            return StartProcess(tool, args);
        }

        public static void SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_clipboardSupported == false)
            {
                _internalClipboard = text;
                PSConsoleReadLine.Ding();
                return;
            }

            string tool = "";
            string args = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExecuteOnStaThread(() => SetClipboardData(Tuple.Create(text, CF_UNICODETEXT)));
                return;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                tool = "xclip";
                args = "-selection clipboard -in";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                tool = "pbcopy";
            }
            else
            {
                _clipboardSupported = false;
                PSConsoleReadLine.Ding();
                return;
            }

            StartProcess(tool, args, text);
            if (_clipboardSupported == false)
            {
                _internalClipboard = text;
            }
        }

        public static void SetRtf(string plainText, string rtfText)
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

            ExecuteOnStaThread(() => SetClipboardData(
                Tuple.Create(plainText, CF_UNICODETEXT),
                Tuple.Create(rtfText, CF_RTF)));
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

        [DllImport("kernel32.dll", ExactSpelling = true, EntryPoint = "RtlMoveMemory", SetLastError = true)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        [DllImport("user32.dll", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsClipboardFormatAvailable(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll", SetLastError=true)]
        static extern uint RegisterClipboardFormat(string lpszFormat);

        private const uint CF_TEXT = 1;
        private const uint CF_UNICODETEXT = 13;
        private static uint CF_RTF;

        private static bool GetTextImpl(out string text)
        {
            try
            {
                if (IsClipboardFormatAvailable(CF_UNICODETEXT))
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        var data = GetClipboardData(CF_UNICODETEXT);
                        if (data != IntPtr.Zero)
                        {
                            data = GlobalLock(data);
                            text = Marshal.PtrToStringUni(data);
                            GlobalUnlock(data);
                            return true;
                        }
                    }
                }
                else if (IsClipboardFormatAvailable(CF_TEXT))
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        var data = GetClipboardData(CF_TEXT);
                        if (data != IntPtr.Zero)
                        {
                            data = GlobalLock(data);
                            text = Marshal.PtrToStringAnsi(data);
                            GlobalUnlock(data);
                            return true;
                        }
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

        private static bool SetClipboardData(params Tuple<string, uint>[] data)
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero)) return false;
                EmptyClipboard();

                foreach (var d in data)
                {
                    if (!SetSingleClipboardData(d.Item1, d.Item2))
                        return false;
                }
            }
            finally
            {
                CloseClipboard();
            }

            return true;
        }

        private static bool SetSingleClipboardData(string text, uint format)
        {
            IntPtr hGlobal = IntPtr.Zero;
            IntPtr data = IntPtr.Zero;

            try
            {

                uint bytes;
                if (format == CF_RTF || format == CF_TEXT)
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
            }

            return true;
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
