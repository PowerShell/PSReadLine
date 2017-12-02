/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
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
    internal static IConsole OneTimeInit(PSConsoleReadLine singleton)
    {
        _singleton = singleton;
        var breakHandlerGcHandle = GCHandle.Alloc(new BreakHandler(OnBreak));
        SetConsoleCtrlHandler((BreakHandler)breakHandlerGcHandle.Target, true);
        _enableVtOutput = SetConsoleOutputVirtualTerminalProcessing();

        return _enableVtOutput ? new VirtualTerminal() : new LegacyWin32Console();
    }

    // Input modes
    const uint ENABLE_PROCESSED_INPUT        = 0x0001;
    const uint ENABLE_LINE_INPUT             = 0x0002;
    const uint ENABLE_WINDOW_INPUT           = 0x0008;
    const uint ENABLE_MOUSE_INPUT            = 0x0010;
    const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

    // Output modes
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    static uint _prePSReadLineConsoleInputMode;
    // Need to remember this decision for CallUsingOurInputMode.
    static bool _enableVtInput;
    static bool _enableVtOutput;
    internal static void Init(ref ICharMap charMap)
    {
        // If either stdin or stdout is redirected, PSReadLine doesn't really work, so throw
        // and let PowerShell call Console.ReadLine or do whatever else it decides to do.
        if (IsHandleRedirected(stdin: false) || IsHandleRedirected(stdin: true))
        {
            throw new NotSupportedException();
        }

        if (_enableVtOutput)
        {
            // This is needed because PowerShell does not restore the console mode
            // after running external applications, and some popular applications
            // clear VT, e.g. git.
            SetConsoleOutputVirtualTerminalProcessing();
        }

        // If input is redirected, we can't use console APIs and have to use VT input.
        if (IsHandleRedirected(stdin: true))
        {
            EnableAnsiInput(ref charMap);
        }
        else
        {
            _prePSReadLineConsoleInputMode = GetConsoleInputMode();

            // This envvar will force VT mode on or off depending on the setting 1 or 0.
            var overrideVtInput = Environment.GetEnvironmentVariable("PSREADLINE_VTINPUT");
            if (overrideVtInput == "1")
            {
                _enableVtInput = true;
            }
            else if (overrideVtInput == "0")
            {
                _enableVtInput = false;
            }
            else
            {
                // If the console was already in VT mode, use the appropriate CharMap.
                // This handles the case where input was not redirected and the user
                // didn't specify a preference. The default is to use the pre-existing
                // console mode.
                _enableVtInput = (_prePSReadLineConsoleInputMode & ENABLE_VIRTUAL_TERMINAL_INPUT) ==
                                 ENABLE_VIRTUAL_TERMINAL_INPUT;
            }

            if (_enableVtInput)
            {
                EnableAnsiInput(ref charMap);
            }

            SetOurInputMode();
        }
    }

    internal static void SetOurInputMode()
    {

        // Clear a couple flags so we can actually receive certain keys:
        //     ENABLE_PROCESSED_INPUT - enables Ctrl+C
        //     ENABLE_LINE_INPUT - enables Ctrl+S
        // Also clear a couple flags so we don't mask the input that we ignore:
        //     ENABLE_MOUSE_INPUT - mouse events
        //     ENABLE_WINDOW_INPUT - window resize events
        var mode = _prePSReadLineConsoleInputMode &
                   ~(ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_WINDOW_INPUT | ENABLE_MOUSE_INPUT);
        if (_enableVtInput)
        {
            // If we're using VT input mode in the console, need to enable that too.
            // Since redirected input was handled above, this just handles the case
            // where the user requested VT input with the environment variable.
            // In this case the CharMap has already been set above.
            mode |= ENABLE_VIRTUAL_TERMINAL_INPUT;
        }
        else
        {
            // We haven't enabled the ANSI escape processor, so turn this off so
            // the console doesn't spew escape sequences all over.
            mode &= ~ENABLE_VIRTUAL_TERMINAL_INPUT;
        }

        SetConsoleInputMode(mode);
    }

    private static void EnableAnsiInput(ref ICharMap charMap)
    {
        charMap = new WindowsAnsiCharMap(PSConsoleReadLine.GetOptions().AnsiEscapeTimeout);
    }

    internal static void Complete()
    {
        if (!IsHandleRedirected(stdin: true))
        {
            SetConsoleInputMode(_prePSReadLineConsoleInputMode);
        }
    }

    internal static T CallPossibleExternalApplication<T>(Func<T> func)
    {

        if (IsHandleRedirected(stdin: true))
        {
            // Don't bother with console modes if we're not in the console.
            return func();
        }

        uint psReadLineConsoleMode = GetConsoleInputMode();
        try
        {
            SetConsoleInputMode(_prePSReadLineConsoleInputMode);
            return func();
        }
        finally
        {
            SetConsoleInputMode(psReadLineConsoleMode);
        }
    }

    internal static void CallUsingOurInputMode(Action a)
    {
        if (IsHandleRedirected(stdin: true))
        {
            // Don't bother with console modes if we're not in the console.
            a();
            return;
        }

        uint psReadLineConsoleMode = GetConsoleInputMode();
        try
        {
            SetOurInputMode();
            a();
        }
        finally
        {
            SetConsoleInputMode(psReadLineConsoleMode);
        }
    }

    private static readonly Lazy<SafeFileHandle> _inputHandle = new Lazy<SafeFileHandle>(() =>
    {
        // We use CreateFile here instead of GetStdWin32Handle, as GetStdWin32Handle will return redirected handles
        var handle = CreateFile(
            "CONIN$",
            (uint)(AccessQualifiers.GenericRead | AccessQualifiers.GenericWrite),
            (uint)ShareModes.ShareWrite,
            (IntPtr)0,
            (uint)CreationDisposition.OpenExisting,
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

    private static readonly Lazy<SafeFileHandle> _outputHandle = new Lazy<SafeFileHandle>(() =>
    {
        // We use CreateFile here instead of GetStdWin32Handle, as GetStdWin32Handle will return redirected handles
        var handle = CreateFile(
            "CONOUT$",
            (uint)(AccessQualifiers.GenericRead | AccessQualifiers.GenericWrite),
            (uint)ShareModes.ShareWrite,
            (IntPtr)0,
            (uint)CreationDisposition.OpenExisting,
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
    private static extern bool GetConsoleMode(IntPtr hConsole, out uint dwMode);

    private static uint GetConsoleInputMode()
    {
        var handle = _inputHandle.Value.DangerousGetHandle();
        GetConsoleMode(handle, out var result);
        return result;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsole, uint dwMode);

    private static void SetConsoleInputMode(uint mode)
    {
        var handle = _inputHandle.Value.DangerousGetHandle();
        SetConsoleMode(handle, mode);
    }

    private static bool SetConsoleOutputVirtualTerminalProcessing()
    {
        var handle = _outputHandle.Value.DangerousGetHandle();
        return GetConsoleMode(handle, out uint mode)
            && SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
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

    public static bool IsConsoleApiAvailable(bool input, bool output)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }
        // If both input and output are false, we don't care about a specific
        // stream, just whether we're in a console or not.
        if (!input && !output)
        {
            return !IsHandleRedirected(stdin: true) || !IsHandleRedirected(stdin: false);
        }
        // Otherwise, we need to check the specific stream(s) that were requested.
        if (input && IsHandleRedirected(stdin: true))
        {
            return false;
        }
        if (output && IsHandleRedirected(stdin: false))
        {
            return false;
        }
        return true;
    }

    internal class LegacyWin32Console : VirtualTerminal
    {
        private static ConsoleColor InitialFG = Console.ForegroundColor;
        private static ConsoleColor InitialBG = Console.BackgroundColor;

        private static readonly Dictionary<string, Action> EscapeSequenceActions = new Dictionary<string, Action> {
            {"\x1b[30;47m", () => {
                Console.ForegroundColor = ConsoleColor.Black;
                Console.BackgroundColor = ConsoleColor.Gray; } },
            {"\x1b[40m", () => Console.BackgroundColor = ConsoleColor.Black},
            {"\x1b[44m", () => Console.BackgroundColor = ConsoleColor.DarkBlue },
            {"\x1b[42m", () => Console.BackgroundColor = ConsoleColor.DarkGreen},
            {"\x1b[46m", () => Console.BackgroundColor = ConsoleColor.DarkCyan},
            {"\x1b[41m", () => Console.BackgroundColor = ConsoleColor.DarkRed},
            {"\x1b[45m", () => Console.BackgroundColor = ConsoleColor.DarkMagenta},
            {"\x1b[43m", () => Console.BackgroundColor = ConsoleColor.DarkYellow},
            {"\x1b[47m", () => Console.BackgroundColor = ConsoleColor.Gray},
            {"\x1b[100m", () => Console.BackgroundColor = ConsoleColor.DarkGray},
            {"\x1b[104m", () => Console.BackgroundColor = ConsoleColor.Blue},
            {"\x1b[102m", () => Console.BackgroundColor = ConsoleColor.Green},
            {"\x1b[106m", () => Console.BackgroundColor = ConsoleColor.Cyan},
            {"\x1b[101m", () => Console.BackgroundColor = ConsoleColor.Red},
            {"\x1b[105m", () => Console.BackgroundColor = ConsoleColor.Magenta},
            {"\x1b[103m", () => Console.BackgroundColor = ConsoleColor.Yellow},
            {"\x1b[107m", () => Console.BackgroundColor = ConsoleColor.White},
            {"\x1b[30m", () => Console.ForegroundColor = ConsoleColor.Black},
            {"\x1b[34m", () => Console.ForegroundColor = ConsoleColor.DarkBlue},
            {"\x1b[32m", () => Console.ForegroundColor = ConsoleColor.DarkGreen},
            {"\x1b[36m", () => Console.ForegroundColor = ConsoleColor.DarkCyan},
            {"\x1b[31m", () => Console.ForegroundColor = ConsoleColor.DarkRed},
            {"\x1b[35m", () => Console.ForegroundColor = ConsoleColor.DarkMagenta},
            {"\x1b[33m", () => Console.ForegroundColor = ConsoleColor.DarkYellow},
            {"\x1b[37m", () => Console.ForegroundColor = ConsoleColor.Gray},
            {"\x1b[90m", () => Console.ForegroundColor = ConsoleColor.DarkGray},
            {"\x1b[94m", () => Console.ForegroundColor = ConsoleColor.Blue},
            {"\x1b[92m", () => Console.ForegroundColor = ConsoleColor.Green},
            {"\x1b[96m", () => Console.ForegroundColor = ConsoleColor.Cyan},
            {"\x1b[91m", () => Console.ForegroundColor = ConsoleColor.Red},
            {"\x1b[95m", () => Console.ForegroundColor = ConsoleColor.Magenta},
            {"\x1b[93m", () => Console.ForegroundColor = ConsoleColor.Yellow},
            {"\x1b[97m", () => Console.ForegroundColor = ConsoleColor.White},
            {"\x1b[0m", () => {
                Console.ForegroundColor = InitialFG;
                Console.BackgroundColor = InitialBG;
            }}
        };

        private void WriteHelper(string s, bool line)
        {
            var from = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\x1b')
                {
                    // Escape sequence - limited support here.
                    var endSequence = s.IndexOf("m", i, StringComparison.Ordinal);
                    if (endSequence > 0)
                    {
                        var escapeSequence = s.Substring(i, endSequence - i + 1);
                        if (EscapeSequenceActions.TryGetValue(escapeSequence, out var action))
                        {
                            Console.Write(s.Substring(from, i - from));
                            action();
                            i = endSequence;
                            from = i + 1;
                        }
                    }
                }
            }

            var tailSegment = s.Substring(from);
            if (line) Console.WriteLine(tailSegment);
            else Console.Write(tailSegment);
        }

        public override void Write(string s)
        {
            WriteHelper(s, false);
        }

        public override void WriteLine(string s)
        {
            WriteHelper(s, true);
        }

        public struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        internal struct COORD
        {
            public short X;
            public short Y;
        }

        public struct CHAR_INFO
        {
            public ushort UnicodeChar;
            public ushort Attributes;
            public CHAR_INFO(char c, ConsoleColor foreground, ConsoleColor background)
            {
                UnicodeChar = c;
                Attributes = (ushort)(((int)background << 4) | (int)foreground);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool ScrollConsoleScreenBuffer(IntPtr hConsoleOutput,
            ref SMALL_RECT lpScrollRectangle,
            IntPtr lpClipRectangle,
            COORD dwDestinationOrigin,
            ref CHAR_INFO lpFill);

        public override void ScrollBuffer(int lines)
        {
            var handle = GetStdHandle((uint) StandardHandleId.Output);
            var scrollRectangle = new SMALL_RECT
            {
                Top = (short) lines,
                Left = 0,
                Bottom = (short)(Console.BufferHeight - 1),
                Right = (short)Console.BufferWidth
            };
            var destinationOrigin = new COORD {X = 0, Y = 0};
            var fillChar = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            ScrollConsoleScreenBuffer(handle, ref scrollRectangle, IntPtr.Zero, destinationOrigin, ref fillChar);
        }

        public override int CursorSize
        {
            get => IsConsoleApiAvailable(input: false, output: true) ? Console.CursorSize : _unixCursorSize;
            set
            {
                if (IsConsoleApiAvailable(input: false, output: true))
                {
                    Console.CursorSize = value;
                }
                else
                {
                    // I'm not sure the cursor is even visible, at any rate, no escape sequences supported.
                    _unixCursorSize = value;
                }
            }
        }

        public override void BlankRestOfLine()
        {
            // This shouldn't scroll, but I'm lazy and don't feel like using a P/Invoke.
            var x = CursorLeft;
            var y = CursorTop;

            for (int i = 0; i < BufferWidth - x; i++) Console.Write(' ');
            if (CursorTop != y+1) y -= 1;

            SetCursorPosition(x, y);
        }
    }
}
