/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
            _singleton?._closingWaitHandle?.Set();
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CONSOLE_FONT_INFO_EX
    {
        internal int cbSize;
        internal int nFont;
        internal short FontWidth;
        internal short FontHeight;
        internal FontFamily FontFamily;
        internal uint FontWeight;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string FontFace;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetCurrentConsoleFontEx(IntPtr consoleOutput, bool bMaximumWindow, ref CONSOLE_FONT_INFO_EX consoleFontInfo);

    [Flags]
    internal enum FontFamily : uint
    {
        // If this bit is set the font is a variable pitch font.
        // If this bit is clear the font is a fixed pitch font.
        TMPF_FIXED_PITCH = 0x01,
        TMPF_VECTOR      = 0x02,
        TMPF_TRUETYPE    = 0x04,
        TMPF_DEVICE      = 0x08,
        LOWORDER_BITS    = TMPF_FIXED_PITCH | TMPF_VECTOR | TMPF_TRUETYPE | TMPF_DEVICE,
    }

    internal static bool IsUsingRasterFont()
    {
        var handle = _outputHandle.Value.DangerousGetHandle();
        var fontInfo = new CONSOLE_FONT_INFO_EX { cbSize = Marshal.SizeOf(typeof(CONSOLE_FONT_INFO_EX)) };
        bool result = GetCurrentConsoleFontEx(handle, false, ref fontInfo);
        // From https://docs.microsoft.com/windows/win32/api/wingdi/ns-wingdi-textmetrica
        // tmPitchAndFamily - A monospace bitmap font has all of these low-order bits clear;
        return result && (fontInfo.FontFamily & FontFamily.LOWORDER_BITS) == 0;
    }

    private static PSConsoleReadLine _singleton;
    internal static IConsole OneTimeInit(PSConsoleReadLine singleton)
    {
        _singleton = singleton;
        var breakHandlerGcHandle = GCHandle.Alloc(new BreakHandler(OnBreak));
        SetConsoleCtrlHandler((BreakHandler)breakHandlerGcHandle.Target, true);
        _enableVtOutput = !Console.IsOutputRedirected && SetConsoleOutputVirtualTerminalProcessing();

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

    internal static bool IsConsoleInput()
    {
        var handle = GetStdHandle((uint)StandardHandleId.Input);
        return GetFileType(handle) == FILE_TYPE_CHAR;
    }

    private static bool IsHandleRedirected(bool stdin)
    {
        var handle = GetStdHandle((uint)(stdin ? StandardHandleId.Input : StandardHandleId.Output));

        // If handle is not to a character device, we must be redirected:
        if (GetFileType(handle) != FILE_TYPE_CHAR)
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
        private static int maxTop = 0;

        private static readonly Dictionary<int, Action> VTColorAction = new Dictionary<int, Action> {
            {40, () => Console.BackgroundColor = ConsoleColor.Black},
            {44, () => Console.BackgroundColor = ConsoleColor.DarkBlue },
            {42, () => Console.BackgroundColor = ConsoleColor.DarkGreen},
            {46, () => Console.BackgroundColor = ConsoleColor.DarkCyan},
            {41, () => Console.BackgroundColor = ConsoleColor.DarkRed},
            {45, () => Console.BackgroundColor = ConsoleColor.DarkMagenta},
            {43, () => Console.BackgroundColor = ConsoleColor.DarkYellow},
            {47, () => Console.BackgroundColor = ConsoleColor.Gray},
            {100, () => Console.BackgroundColor = ConsoleColor.DarkGray},
            {104, () => Console.BackgroundColor = ConsoleColor.Blue},
            {102, () => Console.BackgroundColor = ConsoleColor.Green},
            {106, () => Console.BackgroundColor = ConsoleColor.Cyan},
            {101, () => Console.BackgroundColor = ConsoleColor.Red},
            {105, () => Console.BackgroundColor = ConsoleColor.Magenta},
            {103, () => Console.BackgroundColor = ConsoleColor.Yellow},
            {107, () => Console.BackgroundColor = ConsoleColor.White},
            {30, () => Console.ForegroundColor = ConsoleColor.Black},
            {34, () => Console.ForegroundColor = ConsoleColor.DarkBlue},
            {32, () => Console.ForegroundColor = ConsoleColor.DarkGreen},
            {36, () => Console.ForegroundColor = ConsoleColor.DarkCyan},
            {31, () => Console.ForegroundColor = ConsoleColor.DarkRed},
            {35, () => Console.ForegroundColor = ConsoleColor.DarkMagenta},
            {33, () => Console.ForegroundColor = ConsoleColor.DarkYellow},
            {37, () => Console.ForegroundColor = ConsoleColor.Gray},
            {90, () => Console.ForegroundColor = ConsoleColor.DarkGray},
            {94, () => Console.ForegroundColor = ConsoleColor.Blue},
            {92, () => Console.ForegroundColor = ConsoleColor.Green},
            {96, () => Console.ForegroundColor = ConsoleColor.Cyan},
            {91, () => Console.ForegroundColor = ConsoleColor.Red},
            {95, () => Console.ForegroundColor = ConsoleColor.Magenta},
            {93, () => Console.ForegroundColor = ConsoleColor.Yellow},
            {97, () => Console.ForegroundColor = ConsoleColor.White},
            {0, () => {
                Console.ForegroundColor = InitialFG;
                Console.BackgroundColor = InitialBG;
            }}
        };

        private void WriteHelper(string s, bool line)
        {
            var from = 0;
            for (int i = 0; i < s.Length; i++)
            {
                // Process escapes we understand, write out (likely garbage) ones we don't.
                // The shortest pattern is 4 characters, <ESC>[0m
                if (s[i] != '\x1b' || (i + 3) >= s.Length || s[i + 1] != '[') continue;

                var prefix = s.Substring(from, i - from);
                if (prefix.Length > 0)
                {
                    Console.Write(prefix);
                    maxTop = Console.CursorTop;
                }
                from = i;

                Action action1 = null;
                Action action2 = null;
                var j = i+2;
                var b = 1;
                var color = 0;
                var done = false;
                var invalidSequence = false;
                while (!done && j < s.Length)
                {
                    switch (s[j])
                    {
                        case '0': case '1': case '2': case '3': case '4':
                        case '5': case '6': case '7': case '8': case '9':
                            if (b > 100)
                            {
                                invalidSequence = true;
                                goto default;
                            }
                            color = color * b + (s[j] - '0');
                            b *= 10;
                            break;

                        case 'm':
                            done = true;
                            goto case ';';

                        case 'J':
                            // We'll only support entire display for ED (Erase in Display)
                            if (color == 2) {
                                var cursorVisible = Console.CursorVisible;
                                var left = Console.CursorLeft;
                                var toScroll = maxTop - Console.WindowTop + 1;
                                Console.CursorVisible = false;
                                Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);
                                for (int k = 0; k < toScroll; k++)
                                {
                                    Console.WriteLine();
                                }
                                Console.SetCursorPosition(left, Console.WindowTop + toScroll - 1);
                                Console.CursorVisible = cursorVisible;
                            }
                            break;

                        case ';':
                            if (VTColorAction.TryGetValue(color, out var action))
                            {
                                if (action1 == null) action1 = action;
                                else if (action2 == null) action2 = action;
                                else invalidSequence = true;
                                color = 0;
                                b = 1;
                                break;
                            }
                            else
                            {
                                invalidSequence = true;
                                goto default;
                            }

                        default:
                            done = true;
                            break;
                    }
                    j += 1;
                }

                if (!invalidSequence)
                {
                    action1?.Invoke();
                    action2?.Invoke();
                    from = j;
                    i = j - 1;
                }
            }

            var tailSegment = s.Substring(from);
            if (line)
            {
                Console.WriteLine(tailSegment);
                maxTop = Console.CursorTop;
            }
            else
            {
                Console.Write(tailSegment);
                if (tailSegment.Length > 0)
                {
                    maxTop = Console.CursorTop;
                }
            }
        }

        public override void Write(string s)
        {
            WriteHelper(s, false);
        }

        public override void WriteLine(string s)
        {
            WriteHelper(s, true);
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

    internal const uint SPI_GETSCREENREADER = 0x0046;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);
}
