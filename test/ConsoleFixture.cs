
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace Test
{
    class WindowsConsoleFixtureHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        // For set:
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // For get:
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetForegroundWindow();

        const int WM_INPUTLANGCHANGEREQUEST = 0x0050;

        public static IntPtr SetKeyboardLayout(string lang)
        {
            var layoutId = (new CultureInfo(lang)).KeyboardLayoutId;
            var layout = LoadKeyboardLayout(layoutId.ToString("x8"), 0x80);
            // Hacky, but tests are probably running in a console app and the layout change
            // is ignored, so just post the layout change to the foreground window.
            PostMessage(GetForegroundWindow(), WM_INPUTLANGCHANGEREQUEST, 0, layoutId);
            Thread.Sleep(500);
            return layout;
        }
    }

    public class ConsoleFixture : IDisposable
    {
        public KeyboardLayout KbLayout { get; private set; }
        private string _lang;
        private string _os;

        public ConsoleFixture()
        {
            _lang = "";
            _os = "";
        }

        public void Initialize(string lang, string os)
        {
            if (!string.Equals(lang, _lang) || !string.Equals(os, _os))
            {
                _lang = lang;
                _os = os;
                KbLayout = new KeyboardLayout(lang, "windows");
                WindowsConsoleFixtureHelper.SetKeyboardLayout(lang);
            }
        }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            return _lang + "-" + _os;
        }
    }
}

