/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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

            // Is the TerminateOrphanedConsoleApps feature enabled?
            if (_allowedPids != null)
            {
                // We are about to disable Ctrl+C signals... so if there are still any
                // console-attached children, the shell will be broken until they are
                // gone, so we'll get rid of them:
                TerminateStragglers();
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

    private static SafeFileHandle OpenConsoleHandle(string name)
    {
        // We use CreateFile here instead of GetStdWin32Handle, as GetStdWin32Handle will return redirected handles
        var handle = CreateFile(
            name,
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
            throw new Exception($"Failed to retrieve the console handle ({name}).", innerException);
        }

        return new SafeFileHandle(handle, true);
    }

    private static readonly Lazy<SafeFileHandle>  _inputHandle = new Lazy<SafeFileHandle>(() => OpenConsoleHandle("CONIN$"));
    private static readonly Lazy<SafeFileHandle> _outputHandle = new Lazy<SafeFileHandle>(() => OpenConsoleHandle("CONOUT$"));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsole, out uint dwMode);

    private static uint GetConsoleInputMode()
    {
        var handle = _inputHandle.Value.DangerousGetHandle();
        GetConsoleMode(handle, out var result);
        return result;
    }

    private static uint GetConsoleOutputMode()
    {
        var handle = _outputHandle.Value.DangerousGetHandle();
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
        private static readonly ConsoleColor InitialFG = Console.ForegroundColor;
        private static readonly ConsoleColor InitialBG = Console.BackgroundColor;
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
            {49, () => Console.BackgroundColor = InitialBG},
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
            {39, () => Console.ForegroundColor = InitialFG},
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

            // Last step may result in scrolling.
            if (CursorTop != y+1) y -= 1;

            SetCursorPosition(x, y);
        }
    }

    internal const uint SPI_GETSCREENREADER = 0x0046;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    internal static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            out PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength,
            out int returnLength);

    internal const int InvalidProcessId = -1;

    internal static int GetParentPid(Process process)
    {
        // (This is how ProcessCodeMethods in pwsh does it.)
        var res = NtQueryInformationProcess(process.Handle, 0, out PROCESS_BASIC_INFORMATION pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);
        return res != 0 ? InvalidProcessId : pbi.InheritedFromUniqueProcessId.ToInt32();
    }

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetConsoleProcessList")]
    private static extern uint native_GetConsoleProcessList([In, Out] uint[] lpdwProcessList, uint dwProcessCount);

    private static uint[] GetConsoleProcessList()
    {
        int size = 100;
        uint[] pids = new uint[size];
        uint numPids = native_GetConsoleProcessList(pids, (uint) size);

        if (numPids > size)
        {
            size = (int) numPids + 10; // a bit extra, since we may be racing attaches.
            pids = new uint[size];
            numPids = native_GetConsoleProcessList(pids, (uint) size);
        }

        if (0 == numPids || numPids > size)
        {
            return null; // no TerminateOrphanedConsoleApps for you, sorry
        }

        Array.Resize(ref pids, (int) numPids);
        return pids;
    }

    // If the TerminateOrphanedConsoleApps option is enabled, this is the list of PIDs
    // that are allowed to stay attached to the console (effectively the current process
    // plus ancestors).
    private static uint[] _allowedPids;

    internal static void SetTerminateOrphanedConsoleApps(bool enabled)
    {
        if (enabled)
        {
            _allowedPids = GetConsoleProcessList();
        }
        else
        {
            _allowedPids = null;
        }
    }

    private static bool ItLooksLikeWeAreInTerminal()
    {
        return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    internal enum TaskbarStates
    {
        NoProgress    = 0,
        Indeterminate = 0x1,
        Normal        = 0x2,
        Error         = 0x4,
        Paused        = 0x8,
    }

    internal static class TaskbarProgress
    {
        [ComImport()]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            // ITaskbarList
            [PreserveSig]
            int HrInit();

            [PreserveSig]
            int AddTab(IntPtr hwnd);

            [PreserveSig]
            int DeleteTab(IntPtr hwnd);

            [PreserveSig]
            int ActivateTab(IntPtr hwnd);

            [PreserveSig]
            int SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            [PreserveSig]
            int MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ITaskbarList3
            [PreserveSig]
            int SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);

            [PreserveSig]
            int SetProgressState(IntPtr hwnd, TaskbarStates state);

            // N.B. for copy/pasters: we've left out the rest of the ITaskbarList3 methods...
        }

        [ComImport()]
        [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class TaskbarInstance
        {
        }

        private static Lazy<ITaskbarList3> _taskbarInstance = new Lazy<ITaskbarList3>(() => (ITaskbarList3) new TaskbarInstance());

        public static int SetProgressState(IntPtr windowHandle, TaskbarStates taskbarState)
        {
            return _taskbarInstance.Value.SetProgressState(windowHandle, taskbarState);
        }

        public static int SetProgressValue(IntPtr windowHandle, int progressValue, int progressMax)
        {
            return _taskbarInstance.Value.SetProgressValue(windowHandle, (ulong) progressValue, (ulong) progressMax);
        }
    }

    private static readonly Lazy<uint> _myPid = new(() =>
    {
        using var me = Process.GetCurrentProcess();
        return (uint)me.Id;
    });

    // Calculates what processes need to be terminated (populated into procsToTerminate),
    // and returns the count. A "straggler" is a console-attached process (so GUI
    // processes don't count) that is not in the _allowedPids list.
    private static int GatherStragglers(List<Process> procsToTerminate)
    {
        procsToTerminate.Clear();

        // These are the processes currently attached to this console. Note that GUI
        // processes will not be attached to the console.
        uint[] currentPids = GetConsoleProcessList();

        foreach (var pid in currentPids)
        {
            if (!_allowedPids.Contains(pid))
            {
                Process proc = null;
                try
                {
                    proc = Process.GetProcessById((int) pid);
                }
                catch (ArgumentException)
                {
                    // Ignore it: process could be gone, or something else that we
                    // likely can't do anything about it.
                }

                if (proc != null)
                {
                    // Q: Why the check against the parent pid (below)?
                    //
                    // A: The idea is that a user could do something like this:
                    //
                    //    $p = Start-Process pwsh -ArgumentList '-c Write-Host start $pid; sleep -seconds 30; Write-Host stop' -NoNewWindow -passThru
                    //
                    // Such a process *is* indeed _capable_ of wrecking the interactive prompt (all it has to do is to attempt to read input; and any output
                    // will be interleaved with your interactive session)... but MAYBE it won't. So the idea with letting such processes live is that perhaps
                    // the user did this on purpose, to do some sort of "background work" (even though it may not seem like the best way to do that); and we
                    // only want to kill *actually-orphaned* processes: processes whose parent is gone, so they should be gone, too.
                    //
                    // We only check the immediate children processes here for simplicity. However, an immediate child process may have children that accidentally
                    // derive the standard input (which technically is a wrong thing to do), so ideally we should check if the parent of a console-attached process
                    // is still alive -- the parent process id points to an alive process that was created earlier.
                    // We will wait for feedback to see if this check needs to be updated.

                    if (GetParentPid(proc) != _myPid.Value)
                    {
                        procsToTerminate.Add(proc);
                    }
                    else
                    {
                        proc.Dispose();
                    }
                }
            }
        }
        return procsToTerminate.Count;
    }

    [DllImport("kernel32.dll")]
    internal static extern ulong GetTickCount64();

    private static int MillisLeftUntilDeadline(ulong deadline)
    {
        long diff = (long) (deadline - GetTickCount64());

        if (diff < 0)
        {
            diff = 0;
        }
        else if (diff >= (long) Int32.MaxValue)
        {
            // Should not ever actually happen...
            diff = DefaultGraceMillis;
        }

        return (int) diff;
    }

    private const int DefaultGraceMillis = 1000;
    private const int MaxRounds = 2;

    //
    //                         TerminateOrphanedConsoleApps
    //
    // This feature works around a bad interaction on Windows between:
    //    * a race condition between ctrl+c and console attachment, and
    //    * poor behavior when multiple processes want console input.
    //
    // This bad interaction is most likely to happen when the user has launched a process
    // that is launching many, MANY more child processes (imagine a build system, for
    // example): if the user types ctrl+c to cancel, all processes *currently attached* to
    // the console will receive the ctrl+c signal (and presumably exit). However, there
    // *may* have been some processes that had been created, but are not yet attached to
    // the console--these grandchildren will have missed the ctrl+c signal (that's the
    // race condition). If those grandchildren do not somehow figure out on their own that
    // they should exit, the console enters a highly problematic state ("the borked
    // state"): because pwsh's immediate child has exited, the shell will return to the
    // prompt and wait for input. But those straggler granchildren are ALSO attached to
    // the console... so when the user starts typing, who gets the input?
    //
    // It turns out that the console will just sort of randomly distribute pieces of input
    // between all processes who want input--a straggler grandchild process might get a
    // "key down" record, and then PSReadLine might get the corresponding "key up". This
    // is obviously untenable; it makes the shell totally unusable. (The console team has
    // been made aware, and there are several ideas of how to Do Better, but who knows
    // when any of those will come to fruition.)
    //
    // To make matters worse: when returning to the prompt, PSReadLine disables ctrl+c
    // signals (we prefer to handle those keys specially ourselves). So if you hit this
    // situation with cmd.exe as your shell, you can just mash on ctrl+c for a while and
    // kill all the stragglers manually; but if you have PSReadLine loaded, your shell is
    // borked, and you are stuck. You CAN recover, IF you can track down and kill all the
    // straggler processes manually.
    //
    // So when enabled, this feature does that for you: it kills all those straggler
    // processes, right before we disable ctrl+c signals and wait for user input, ensuring
    // that the user has a usable shell.
    //
    // Note that GUI processes do not attach to the console, so if you have launched
    // notepad, for example, TerminateOrphanedConsoleApps will never even "see" it; they
    // are immune from getting terminated.
    //
    // Q: But isn't terminating processes that we know nothing about kind of risky and
    //    extreme?
    //
    // A: Perhaps so... but consider the alternative: by definition, if you get into a
    //    situation where the TerminateOrphanedConsoleApps feature would actually kill
    //    anything, your shell will be Completely Broken. It's "them or us": allow the
    //    stragglers to live, but leave the user without their shell; or kill the
    //    stragglers and give the user their shell back. There is no middle ground. So
    //    when the TerminateOrphanedConsoleApps feature is enabled, that means the user
    //    has opted for "give me back my shell".
    //
    //    Note that we do give stragglers a small grace period before terminating them, in
    //    case they are somehow just slow shutting down. But if you're wondering "should
    //    we make that grace period longer?", remember that another way to think of that
    //    period is "how long do I want the shell to potentially be unusable after
    //    displaying the prompt?"
    //
    // Q: What if the user *didn't* type ctrl+c?
    //
    // A: We don't care. When TerminateOrphanedConsoleApps is called, all we know is that
    //    the shell has displayed the prompt and believes it is time to wait for user
    //    input.  Whether this situation came about because of a ctrl+c, or some other
    //    situation (for example, if the shell's immediate child crashed or was manually
    //    killed), if there are leftover straggler processes (console-attached
    //    grandchildren), the shell will be broken until they are gone, and thus we must
    //    take action (if the feature is enabled).
    //
    // Q: Should this really be baked into PSReadLine, or could we leave it to some other
    //    module to implement? (See: https://github.com/jazzdelightsme/ConsoleBouncer)
    //
    // A: We should have the option in PSReadLine. An external module can do something
    //    very *similar* to what we do here in PSReadLine, but not quite the same, and is
    //    strictly inferior. An external module would have to rely on receiving a ctrl+c
    //    signal, but "there was a ctrl+c signal" is NOT equivalent to "the shell is about
    //    to wait for input". For example, some child processes may depend on handling
    //    ctrl+c signals, *without* exiting (kd.exe / cdb.exe, for example). In such a
    //    case, control would not return to the shell, but an external module would have
    //    no way to know that (hence it is inferior). That could be worked around, but
    //    only clumsily--the user would have to have a way to tell the module "hey BTW
    //    please don't kill these ones, even though they will *look* like stragglers".
    //
    //    And in fact, an external module solution may still be attractive to some users
    //    (and could safely be used with TerminateOrphanedConsoleApps enabled). Because
    //    the (external solution) ConsoleBouncer module reacts to ctrl+c signals, that
    //    makes it a bit more aggressive than what we do here:
    //    TerminateOrphanedConsoleApps only comes into play when control has returned to
    //    the shell, which might not be right away after the user types ctrl+c--there
    //    might be "Terminate batch job (Y/N)?" messages, etc. So if the user understands
    //    the limitations of the ConsoleBouncer module and has an environment where it
    //    would be suitable, they could still opt to use it to get much more responsive
    //    ctrl+c behavior. (A metaphor with a club: the PSReadLine built-in feature
    //    patiently waits for the host of a private party to leave before kicking the rest
    //    of the guests out; whereas the ConsoleBouncer, upon receipt of a ctrl+c signal,
    //    just clears the whole place out right away (which *might* not be the right thing
    //    to do, but you're paying them to be tough, not smart).)
    //
    private static void TerminateStragglers()
    {
        var procsToTerminate = new List<Process>();

        // The theory for why more than one round might be needed is that the same race
        // between process creation and console attachment that could cause lingering
        // processes in the first place could cause us to need a second round of
        // cleanup... but I've never actually seen more than one round be needed. Probably
        // because in my specific scenario the process that was spawning processes got
        // taken out with the original ctrl+c signal.
        //
        // If it takes more than a few rounds of cleanup, we may be in some kind of
        // pathological situation, and we'll bow out.
        int round = 0;
        int killAttempts = 0;

        while (round++ < MaxRounds &&
               GatherStragglers(procsToTerminate) > 0)
        {
            // We'll give them up to GracePeriodMillis for them to exit on their
            // own, in case they actually did receive the original ctrl+c, and are
            // just a tad slow shutting down.
            ulong deadline = GetTickCount64() + (ulong) DefaultGraceMillis;

            var notDeadYet = procsToTerminate.Where(
                    (p) => !p.WaitForExit(MillisLeftUntilDeadline(deadline)));

            foreach (var process in notDeadYet)
            {
                try
                {
                    killAttempts++;
                    process.Kill();
                }
                // Ignore problems; maybe it's gone already, maybe something else;
                // whatever.
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
            }

            foreach (var process in procsToTerminate)
            {
                process.Dispose();
            }
        } // end retry loop

        // In forcible termination scenarios, if there was a child updating the terminal's
        // progress state, it may be left stuck that way... we can clear that out.
        //
        // The preferred way to do that is with a VT sequence, but there's no way to know
        // if the console we are attached to supports that sequence. If we are in Windows
        // Terminal, we know we can use the VT sequence; else we'll fall back to the old
        // (Win7-era?) COM API (which does the same thing).
        uint consoleMode = GetConsoleOutputMode();
        if (ItLooksLikeWeAreInTerminal())
        {
            // We can use the [semi-]standard OSC sequence:
            // https://conemu.github.io/en/AnsiEscapeCodes.html#ConEmu_specific_OSC
            if (0 != (consoleMode & (uint) ENABLE_VIRTUAL_TERMINAL_PROCESSING))
            {
                // Use "bell" if we actually tried to whack anything.
                string final = (killAttempts > 0) ? "\a" : "\x001b\\";
                Console.Write("\x001b]9;4;0;0" + final);
            }
        }
        else
        {
            IntPtr hwnd = GetConsoleWindow();
            if (hwnd != IntPtr.Zero)
            {
                int ret = TaskbarProgress.SetProgressState(hwnd, TaskbarStates.NoProgress);
            }
        }
    }

    /// <remarks>
    /// This method helps to find the active keyboard layout in a terminal process that controls the current console
    /// application. For now, we assume that at the moment when we are asked to process a keyboard shortcut, the
    /// process owning the foreground window has to be the terminal process controlling the current console.
    /// </remarks>
    public static IntPtr GetConsoleKeyboardLayout()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            uint tid = GetWindowThreadProcessId(foregroundWindow, out _);
            if (tid != 0)
            {
                return GetKeyboardLayout(tid);
            }
        }

        // Fall back to the default keyboard when we failed to find the parent terminal process.
        return GetKeyboardLayout(0);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("User32.dll", SetLastError = true)]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out IntPtr proccess);
}
