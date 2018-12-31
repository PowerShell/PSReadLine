
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace Test
{
    public class WindowsConsoleFixtureHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

        // For set:
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // For get:
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetForegroundWindow();

        const int WM_INPUTLANGCHANGEREQUEST = 0x0050;

        private static string GetLayoutNameFromHKL(IntPtr hkl)
        {
            var lcid = (int)((uint)hkl & 0xffff);
            return (new CultureInfo(lcid)).Name;
        }

        public static IEnumerable<string> GetKeyboardLayoutList()
        {
            int cnt = GetKeyboardLayoutList(0, null);
            var list = new IntPtr[cnt];
            GetKeyboardLayoutList(list.Length, list);

            foreach (var layout in list)
            {
                yield return GetLayoutNameFromHKL(layout);
            }
        }

        public static string GetKeyboardLayout()
        {
            var layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out var processId));
            return GetLayoutNameFromHKL(layout);
        }

        public static IntPtr SetKeyboardLayout(string lang)
        {
            var layoutId = (new CultureInfo(lang)).KeyboardLayoutId;
            var layout = LoadKeyboardLayout(layoutId.ToString("x8"), 0x80);
            // Hacky, but tests are probably running in a console app and the layout change
            // is ignored, so post the layout change to the foreground window.
            PostMessage(GetForegroundWindow(), WM_INPUTLANGCHANGEREQUEST, 0, layoutId);
            Thread.Sleep(500);
            return layout;
        }
    }

    public class ConsoleFixture : IDisposable
    {
        public KeyboardLayout KbLayout { get; private set; }
        public string Lang { get; private set; }
        public string Os { get; private set; }

        public ConsoleFixture()
        {
            Lang = "";
            Os = "";
        }

        public void Initialize(string lang, string os)
        {
            if (!string.Equals(lang, Lang) || !string.Equals(os, Os))
            {
                Lang = lang;
                Os = os;
                KbLayout = new KeyboardLayout(lang, "windows");
            }
        }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            return Lang + "-" + Os;
        }
    }
}

