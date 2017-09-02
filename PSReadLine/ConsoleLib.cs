/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerShell.Internal
{
#pragma warning disable 1591

    internal static class NativeMethods
    {
        public const uint MAPVK_VK_TO_VSC   = 0x00;
        public const uint MAPVK_VSC_TO_VK   = 0x01;
        public const uint MAPVK_VK_TO_CHAR  = 0x02;

        public const byte VK_SHIFT          = 0x10;
        public const byte VK_CONTROL        = 0x11;
        public const byte VK_ALT            = 0x12;
        public const uint MENU_IS_ACTIVE    = 0x01;
        public const uint MENU_IS_INACTIVE  = 0x00; // windows key

        public const uint ENABLE_PROCESSED_INPUT = 0x0001;
        public const uint ENABLE_LINE_INPUT      = 0x0002;
        public const uint ENABLE_WINDOW_INPUT    = 0x0008;
        public const uint ENABLE_MOUSE_INPUT     = 0x0010;

        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);  // WinBase.h

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetStdHandle(uint handleId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr hConsoleOutput, out uint dwMode);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleOutput, uint dwMode);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(BreakHandler handlerRoutine, bool add);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicode(uint uVirtKey, uint uScanCode, byte[] lpKeyState,
           [MarshalAs(UnmanagedType.LPArray)] [Out] char[] chars, int charMaxCount, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern short VkKeyScan(char @char);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFile
        (
            string fileName,
            uint desiredAccess,
            uint ShareModes,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFileWin32Handle
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int GetFileType(IntPtr handle);

        internal const int FILE_TYPE_CHAR = 0x0002;
    }

    public delegate bool BreakHandler(ConsoleBreakSignal ConsoleBreakSignal);

    public enum ConsoleBreakSignal : uint
    {
        CtrlC     = 0,
        CtrlBreak = 1,
        Close     = 2,
        Logoff    = 5,
        Shutdown  = 6,
        None      = 255,
    }

    public enum StandardHandleId : uint
    {
        Error  = unchecked((uint)-12),
        Output = unchecked((uint)-11),
        Input  = unchecked((uint)-10),
    }

    [Flags]
    internal enum AccessQualifiers : uint
    {
        // From winnt.h
        GenericRead = 0x80000000,
        GenericWrite = 0x40000000
    }

    internal enum CreationDisposition : uint
    {
        // From winbase.h
        CreateNew = 1,
        CreateAlways = 2,
        OpenExisting = 3,
        OpenAlways = 4,
        TruncateExisting = 5
    }

    [Flags]
    internal enum ShareModes : uint
    {
        // From winnt.h
        ShareRead = 0x00000001,
        ShareWrite = 0x00000002
    }

    internal struct COORD
    {
        public short X;
        public short Y;

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0},{1}", X, Y);
        }
    }

    internal static class ConsoleKeyInfoExtension
    {
        public static string ToGestureString(this ConsoleKeyInfo key)
        {
            var mods = key.Modifiers;

            var sb = new StringBuilder();
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                sb.Append("Ctrl");
            }
            if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append("Alt");
            }

            char c = ConsoleKeyChordConverter.GetCharFromConsoleKey(key.Key,
                (mods & ConsoleModifiers.Shift) != 0 ? ConsoleModifiers.Shift : 0);
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                {
                    if (sb.Length > 0)
                        sb.Append("+");
                    sb.Append("Shift");
                }
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append(key.Key);
            }
            else
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    internal class ConhostConsole : IConsole
    {
        private readonly Lazy<SafeFileHandle> _outputHandle = new Lazy<SafeFileHandle>(() =>
        {
            // We use CreateFile here instead of GetStdWin32Handle, as GetStdWin32Handle will return redirected handles
            var handle = NativeMethods.CreateFile(
                "CONOUT$",
                (UInt32)(AccessQualifiers.GenericRead | AccessQualifiers.GenericWrite),
                (UInt32)ShareModes.ShareWrite,
                (IntPtr)0,
                (UInt32)CreationDisposition.OpenExisting,
                0,
                (IntPtr)0);

            if (handle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                int err = Marshal.GetLastWin32Error();
                Win32Exception innerException = new Win32Exception(err);
                throw new Exception("Failed to retreive the input console handle.", innerException);
            }

            return new SafeFileHandle(handle, true);
        }
        );

        private readonly Lazy<SafeFileHandle> _inputHandle = new Lazy<SafeFileHandle>(() =>
        {
            // We use CreateFile here instead of GetStdWin32Handle, as GetStdWin32Handle will return redirected handles
            var handle = NativeMethods.CreateFile(
                "CONIN$",
                (UInt32)(AccessQualifiers.GenericRead | AccessQualifiers.GenericWrite),
                (UInt32)ShareModes.ShareWrite,
                (IntPtr)0,
                (UInt32)CreationDisposition.OpenExisting,
                0,
                (IntPtr)0);

            if (handle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                int err = Marshal.GetLastWin32Error();
                Win32Exception innerException = new Win32Exception(err);
                throw new Exception("Failed to retreive the input console handle.", innerException);
            }

            return new SafeFileHandle(handle, true);
        });

        public uint GetConsoleInputMode()
        {
            var handle = _inputHandle.Value.DangerousGetHandle();
            NativeMethods.GetConsoleMode(handle, out var result);
            return result;
        }

        public void SetConsoleInputMode(uint mode)
        {
            var handle = _inputHandle.Value.DangerousGetHandle();
            NativeMethods.SetConsoleMode(handle, mode);
        }

        public ConsoleKeyInfo ReadKey()
        {
            return Console.ReadKey(true);
        }

        public bool KeyAvailable => Console.KeyAvailable;

        public int CursorLeft
        {
            get => Console.CursorLeft;
            set => Console.CursorLeft = value;
        }

        public int CursorTop
        {
            get => Console.CursorTop;
            set => Console.CursorTop = value;
        }

        public int CursorSize
        {
            get => Console.CursorSize;
            set => Console.CursorSize = value;
        }

        public bool CursorVisible
        {
            get => Console.CursorVisible;
            set => Console.CursorVisible = value;
        }

        public int BufferWidth
        {
            get => Console.BufferWidth;
            set => Console.BufferWidth = value;
        }

        public int BufferHeight
        {
            get => Console.BufferHeight;
            set => Console.BufferHeight = value;
        }

        public int WindowWidth
        {
            get => Console.WindowWidth;
            set => Console.WindowWidth = value;
        }

        public int WindowHeight
        {
            get => Console.WindowHeight;
            set => Console.WindowHeight = value;
        }

        public int WindowTop
        {
            get => Console.WindowTop;
            set => Console.WindowTop = value;
        }

        public ConsoleColor BackgroundColor
        {
            get => Console.BackgroundColor;
            set => Console.BackgroundColor = value;
        }

        public ConsoleColor ForegroundColor
        {
            get => Console.ForegroundColor;
            set => Console.ForegroundColor = value;
        }

        public void SetWindowPosition(int left, int top)
        {
            Console.SetWindowPosition(left, top);
        }

        public void SetCursorPosition(int left, int top)
        {
            Console.SetCursorPosition(left, top);
        }

        public void Write(string value)
        {
            Console.Write(value);
        }

        public void WriteLine(string value)
        {
            Console.WriteLine(value);
        }

        public void ScrollBuffer(int lines)
        {
            Console.Write("\x1b[" + lines + "S");
            /*
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var scrollRectangle = new SMALL_RECT
            {
                Top = (short) lines,
                Left = 0,
                Bottom = (short)(Console.BufferHeight - 1),
                Right = (short)Console.BufferWidth
            };
            var destinationOrigin = new COORD {X = 0, Y = 0};
            var fillChar = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            NativeMethods.ScrollConsoleScreenBuffer(handle, ref scrollRectangle, IntPtr.Zero, destinationOrigin, ref fillChar);
            */
        }

        public bool IsHandleRedirected(bool stdIn)
        {
            var handle = NativeMethods.GetStdHandle((uint)(stdIn ? StandardHandleId.Input : StandardHandleId.Output));

            // If handle is not to a character device, we must be redirected:
            int fileType = NativeMethods.GetFileType(handle);
            if ((fileType & NativeMethods.FILE_TYPE_CHAR) != NativeMethods.FILE_TYPE_CHAR)
                return true;

            // Char device - if GetConsoleMode succeeds, we are NOT redirected.
            return !NativeMethods.GetConsoleMode(handle, out var unused);
        }

        private int _savedX, _savedY;

        public void SaveCursor()
        {
            _savedX = Console.CursorLeft;
            _savedY = Console.CursorTop;
        }

        public void RestoreCursor()
        {
            Console.SetCursorPosition(_savedX, _savedY);
        }
    }

#pragma warning restore 1591
}
