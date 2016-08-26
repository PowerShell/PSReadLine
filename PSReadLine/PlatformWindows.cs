/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.PowerShell;
using Microsoft.PowerShell.Internal;
using Microsoft.Win32.SafeHandles;

static class PlatformWindows
{
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

    internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);  // WinBase.h

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetStdHandle(uint handleId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetConsoleCtrlHandler(BreakHandler handlerRoutine, bool add);

    delegate bool BreakHandler(ConsoleBreakSignal ConsoleBreakSignal);

    private static bool OnBreak(ConsoleBreakSignal signal)
    {
        if (signal == ConsoleBreakSignal.Close || signal == ConsoleBreakSignal.Shutdown)
        {
            // Set the event so ReadKey throws an exception to unwind.
            _singleton._closingWaitHandle.Set();
        }

        return false;
    }

    private static PSConsoleReadLine _singleton;
    internal static void OneTimeInit(PSConsoleReadLine singleton)
    {
        _singleton = singleton;
        var breakHandlerGcHandle = GCHandle.Alloc(new BreakHandler(OnBreak));
        SetConsoleCtrlHandler((BreakHandler)breakHandlerGcHandle.Target, true);
    }
    
    const uint ENABLE_PROCESSED_INPUT = 0x0001;
    const uint ENABLE_LINE_INPUT      = 0x0002;
    const uint ENABLE_WINDOW_INPUT    = 0x0008;
    const uint ENABLE_MOUSE_INPUT     = 0x0010;

    static uint _prePSReadlineConsoleMode;
    internal static void Init()
    {
        // If either stdin or stdout is redirected, PSReadline doesn't really work, so throw
        // and let PowerShell call Console.ReadLine or do whatever else it decides to do.
        if (IsHandleRedirected(stdin: false) || IsHandleRedirected(stdin: true))
        {
            throw new NotSupportedException();
        }

        _prePSReadlineConsoleMode = GetConsoleInputMode();

        // Clear a couple flags so we can actually receive certain keys:
        //     ENABLE_PROCESSED_INPUT - enables Ctrl+C
        //     ENABLE_LINE_INPUT - enables Ctrl+S
        // Also clear a couple flags so we don't mask the input that we ignore:
        //     ENABLE_MOUSE_INPUT - mouse events
        //     ENABLE_WINDOW_INPUT - window resize events
        var mode = _prePSReadlineConsoleMode &
                   ~(ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_WINDOW_INPUT | ENABLE_MOUSE_INPUT);
        SetConsoleInputMode(mode);
    }

    internal static void Complete()
    {
        SetConsoleInputMode(_prePSReadlineConsoleMode);
    }

    internal static T CallPossibleExternalApplication<T>(Func<T> func)
    {
        uint psReadlineConsoleMode = GetConsoleInputMode();
        try
        {
            SetConsoleInputMode(_prePSReadlineConsoleMode);
            return func();
        }
        finally
        {
            SetConsoleInputMode(psReadlineConsoleMode);
        }
    }

    private static readonly Lazy<SafeFileHandle> _inputHandle = new Lazy<SafeFileHandle>(() =>
    {
        // We use CreateFile here instead of GetStdWin32Handle, as GetStdWin32Handle will return redirected handles
        var handle = CreateFile(
            "CONIN$",
            (UInt32)(AccessQualifiers.GenericRead | AccessQualifiers.GenericWrite),
            (UInt32)ShareModes.ShareWrite,
            (IntPtr)0,
            (UInt32)CreationDisposition.OpenExisting,
            0,
            (IntPtr)0);

        if (handle == INVALID_HANDLE_VALUE)
        {
            int err = Marshal.GetLastWin32Error();
            Win32Exception innerException = new Win32Exception(err);
            throw new Exception("Failed to retrieve the input console handle.", innerException);
        }

        return new SafeFileHandle(handle, true);
    });

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleOutput, out uint dwMode);

    private static uint GetConsoleInputMode()
    {
        var handle = _inputHandle.Value.DangerousGetHandle();
        GetConsoleMode(handle, out var result);
        return result;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleOutput, uint dwMode);

    private static void SetConsoleInputMode(uint mode)
    {
        var handle = _inputHandle.Value.DangerousGetHandle();
        SetConsoleMode(handle, mode);
    }

    private static bool IsHandleRedirected(bool stdin)
    {
        var handle = GetStdHandle((uint)(stdin ? StandardHandleId.Input : StandardHandleId.Output));

        // If handle is not to a character device, we must be redirected:
        int fileType = GetFileType(handle);
        if ((fileType & FILE_TYPE_CHAR) != FILE_TYPE_CHAR)
            return true;

        // Char device - if GetConsoleMode succeeds, we are NOT redirected.
        return !GetConsoleMode(handle, out var unused);
    }
}
