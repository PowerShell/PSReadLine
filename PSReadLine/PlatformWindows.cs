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

    const uint ENABLE_PROCESSED_INPUT        = 0x0001;
    const uint ENABLE_LINE_INPUT             = 0x0002;
    const uint ENABLE_WINDOW_INPUT           = 0x0008;
    const uint ENABLE_MOUSE_INPUT            = 0x0010;
    const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

    static uint _prePSReadlineConsoleMode;
    internal static void Init(ref ICharMap charMap)
    {
        bool isRedirected = IsHandleRedirected(stdin: true);
        // This envvar will force VT mode on or off depending on the setting 1 or 0.
        // If input is redirected, we can't use console APIs and have to use VT
        // input, so it is ignored in that case.
        var envVtInput = Environment.GetEnvironmentVariable("PSREADLINE_VTINPUT");
        bool useVtInput = isRedirected || envVtInput == "1";
        if (useVtInput)
        {
            // For redirected input, we need to process VT sequences.
            EnableAnsiInput(ref charMap);
        }

        if (isRedirected)
        {
            return;
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
        if (useVtInput)
        {
            // If we're using VT input mode in the console, need to enable that too.
            // Since redirected input was handled above, this just handles the case
            // where the user requested VT input with the environment variable.
            // In this case the CharMap has already been set above.
            mode |= ENABLE_VIRTUAL_TERMINAL_INPUT;
        }
        else if (envVtInput == "0")
        {
            // Allow forcing VT input off with the environment variable.
            mode &= ~ENABLE_VIRTUAL_TERMINAL_INPUT;
        }
        else if ((_prePSReadlineConsoleMode & ENABLE_VIRTUAL_TERMINAL_INPUT) == ENABLE_VIRTUAL_TERMINAL_INPUT)
        {
            // If the console was already in VT mode, use the appropriate CharMap.
            // This handles the case where input was not redirected and the user
            // didn't specify a preference. The default is to use the pre-existing
            // console mode.
            EnableAnsiInput(ref charMap);
        }

        SetConsoleInputMode(mode);
    }

    private static void EnableAnsiInput(ref ICharMap charMap)
    {
        if (uint.TryParse(Environment.GetEnvironmentVariable("PSREADLINE_ESCTIMEOUT"), out var escTimeout))
        {
            // Don't let someone get themselves stuck here.
            if (escTimeout > 1000)
            {
                escTimeout = 1000;
            }
            charMap = new WindowsAnsiCharMap(escTimeout);
        }
        else
        {
            // Use the default timeout.
            charMap = new WindowsAnsiCharMap();
        }
    }

    internal static void Complete()
    {
        if (!IsHandleRedirected(stdin: true))
        {
            SetConsoleInputMode(_prePSReadlineConsoleMode);
        }
    }

    internal static T CallPossibleExternalApplication<T>(Func<T> func)
    {
        
        if (IsHandleRedirected(stdin: true))
        {
            // Don't bother with console modes if we're not in the console.
            return func();
        }

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
