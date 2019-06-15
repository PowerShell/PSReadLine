/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;


namespace Microsoft.PowerShell
{
    internal class PSKeyInfo : IEquatable<PSKeyInfo>
    {
        private PSKeyInfo(string s)
        {
            KeyStr = s;
        }
        internal static PSKeyInfo From(char c) => new PSKeyInfo(c.ToString());
        internal static PSKeyInfo From(ConsoleKey key) => new PSKeyInfo(key.ToString());
        internal static PSKeyInfo WithAlt(char c) => new PSKeyInfo("Alt+" + c) { Alt = true };
        internal static PSKeyInfo WithAlt(ConsoleKey key) => new PSKeyInfo("Alt+" + key) { Alt = true };
        internal static PSKeyInfo WithCtrl(char c) => new PSKeyInfo("Ctrl+" + c) { Control = true };
        internal static PSKeyInfo WithCtrl(ConsoleKey key) => new PSKeyInfo("Ctrl+" + key) { Control = true };
        internal static PSKeyInfo WithShift(ConsoleKey key) => new PSKeyInfo("Shift+" + key) { Shift = true };
        internal static PSKeyInfo WithShiftCtrl(ConsoleKey key) => new PSKeyInfo("Shift+Ctrl+" + key) { Control = true, Shift = true };
        internal static PSKeyInfo WithCtrlAlt(char c) => new PSKeyInfo("Ctrl+Alt+" + c) { Control = true, Alt = true };

        public override string ToString() => KeyStr;

        public char KeyChar
        {
            get
            {
                var len = KeyStr.Length;
                if (len == 1)
                {
                    return KeyStr[0];
                }

                var last = KeyStr[len - 1];
                var nextToLast = KeyStr[len - 2];
                if (nextToLast == '+' && (last == '-' || (last >= '0' && last <= '9')))
                {
                    return last;
                }

                switch (KeyStr)
                {
                    case "Spacebar": return ' ';
                    case "Enter": return '\n';
                    case "Escape": return '\x1b';
                    case "Tab": return '\t';
                }

                return '\0';
            }
        }

        public string KeyStr { get; }
        public bool Shift { get; private set; }
        public bool Alt { get; private set; }
        public bool Control { get; private set; }

        public ConsoleKeyInfo AsConsoleKeyInfo()
        {
            return ConsoleKeyChordConverter.Convert(KeyStr)[0];
        }

        public static PSKeyInfo FromConsoleKeyInfo(ConsoleKeyInfo keyInfo)
        {
            var result = new PSKeyInfo(KeyInfoAsString(keyInfo))
            {
                Shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                Alt = (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                Control = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0,
            };
            return result;
        }

        public bool Equals(PSKeyInfo other)
        {
            return PSKeyInfo.Equals(this, other);
        }

        public override bool Equals(object obj)
        {
            return PSKeyInfo.Equals(this, obj as PSKeyInfo);
        }

        internal static bool Equals(PSKeyInfo left, PSKeyInfo right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) return false;

            return string.Equals(left.KeyStr, right.KeyStr);
        }

        public static bool operator ==(PSKeyInfo left, PSKeyInfo right)
        {
            return PSKeyInfo.Equals(left, right);
        }

        public static bool operator !=(PSKeyInfo left, PSKeyInfo right)
        {
            return !PSKeyInfo.Equals(left, right);
        }

        public override int GetHashCode()
        {
            return KeyStr.GetHashCode();
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint MapVirtualKey(ConsoleKey uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicode(
            ConsoleKey uVirtKey,
            uint uScanCode,
            byte[] lpKeyState,
            [MarshalAs(UnmanagedType.LPArray)] [Out] char[] chars,
            int charMaxCount,
            uint flags);

        static readonly ThreadLocal<char[]> toUnicodeBuffer = new ThreadLocal<char[]>(() => new char[2]);
        static readonly ThreadLocal<byte[]> toUnicodeStateBuffer = new ThreadLocal<byte[]>(() => new byte[256]);
        internal static void TryGetCharFromConsoleKey(ConsoleKeyInfo key, ref char result, ref bool isDeadKey)
        {
            var modifiers = key.Modifiers;
            var virtualKey = key.Key;

            // emulate GetKeyboardState bitmap - set high order bit for relevant modifier virtual keys
            var state = toUnicodeStateBuffer.Value;
            state[0x10 /*VK_SHIFT*/] = (byte)((modifiers & ConsoleModifiers.Shift) != 0 ? 0x80 : 0);
            state[0x11 /*VK_CONTROL*/] = (byte)((modifiers & ConsoleModifiers.Control) != 0 ? 0x80 : 0);
            state[0x12 /*VK_ALT*/] = (byte)((modifiers & ConsoleModifiers.Alt) != 0 ? 0x80 : 0);

            // get corresponding scan code
            uint scanCode = MapVirtualKey(virtualKey, 0x0 /*MAPVK_VK_TO_VSC*/);

            // get corresponding character  - maybe be 0, 1 or 2 in length (diacriticals)
            var chars = toUnicodeBuffer.Value;
            var flags = 0u; /* If bit 0 is set, a menu is active. */
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major == 10 && osVersion.Build >= 14393 || osVersion.MajorRevision > 10)
            {
                flags |= (1 << 2); /* If bit 2 is set, keyboard state is not changed (Windows 10, version 1607 and newer) */
            }
            int charCount = ToUnicode(virtualKey, scanCode, state, chars, chars.Length, flags);

            if (charCount == 1)
            {
                result = chars[0];
            }
            else if (charCount == -1 || charCount >=2)
            {
                // Quoted from https://docs.microsoft.com/en-us/windows/desktop/api/winuser/nf-winuser-tounicode#return-value:
                //  "Return value  -1 --
                //     The specified virtual key is a dead-key character (accent or diacritic).
                //   Return value >=2 --
                //     Two or more characters were written to the buffer specified by pwszBuff. The most common cause for this is that a dead-key character 
                //     (accent or diacritic) stored in the keyboard layout could not be combined with the specified virtual key to form a single character."
                isDeadKey = true;
            }
        }

        static readonly ThreadLocal<StringBuilder> keyInfoStringBuilder = new ThreadLocal<StringBuilder>(() => new StringBuilder());
        static string KeyInfoAsString(ConsoleKeyInfo key)
        {
            var sb = keyInfoStringBuilder.Value;
            sb.Clear();

            var mods = key.Modifiers;
            var isShift = (mods & ConsoleModifiers.Shift) != 0;
            var isCtrl = (mods & ConsoleModifiers.Control) != 0;
            var isAlt = (mods & ConsoleModifiers.Alt) != 0;

            void AppendPart(string str)
            {
                if (sb.Length > 0) sb.Append("+");
                sb.Append(str);
            }

            var consoleKey = key.Key;
            switch (consoleKey)
            {
                // Keys we definitely bind or might bind.
                case ConsoleKey.PageUp: case ConsoleKey.PageDown:
                case ConsoleKey.LeftArrow: case ConsoleKey.UpArrow: case ConsoleKey.RightArrow: case ConsoleKey.DownArrow:
                case ConsoleKey.F1: case ConsoleKey.F2: case ConsoleKey.F3: case ConsoleKey.F4:
                case ConsoleKey.F5: case ConsoleKey.F6: case ConsoleKey.F7: case ConsoleKey.F8:
                case ConsoleKey.F9: case ConsoleKey.F10: case ConsoleKey.F11: case ConsoleKey.F12:
                case ConsoleKey.F13: case ConsoleKey.F14: case ConsoleKey.F15: case ConsoleKey.F16:
                case ConsoleKey.F17: case ConsoleKey.F18: case ConsoleKey.F19: case ConsoleKey.F20:
                case ConsoleKey.F21: case ConsoleKey.F22: case ConsoleKey.F23: case ConsoleKey.F24:
                case ConsoleKey.Backspace:
                case ConsoleKey.Tab:
                case ConsoleKey.Enter:
                case ConsoleKey.Escape:
                case ConsoleKey.End:
                case ConsoleKey.Home:
                case ConsoleKey.Insert:
                case ConsoleKey.Delete:

                // Keys we want to ignore (and handling here will help to ignore)
                case ConsoleKey.BrowserBack: case ConsoleKey.BrowserForward: case ConsoleKey.BrowserRefresh: case ConsoleKey.BrowserStop:
                case ConsoleKey.BrowserSearch: case ConsoleKey.BrowserFavorites: case ConsoleKey.BrowserHome:
                case ConsoleKey.VolumeMute: case ConsoleKey.VolumeDown: case ConsoleKey.VolumeUp:
                case ConsoleKey.MediaNext: case ConsoleKey.MediaPrevious: case ConsoleKey.MediaStop: case ConsoleKey.MediaPlay:
                case ConsoleKey.LaunchMail: case ConsoleKey.LaunchMediaSelect: case ConsoleKey.LaunchApp1: case ConsoleKey.LaunchApp2:
                case ConsoleKey.LeftWindows: case ConsoleKey.RightWindows: case ConsoleKey.Applications:
                case ConsoleKey.PrintScreen:

                // Keys I'm not familiar with, presumably we'd want to ignore.
                case ConsoleKey.Clear: case ConsoleKey.Pause: case ConsoleKey.Select: case ConsoleKey.Print:
                case ConsoleKey.Execute: case ConsoleKey.Help: case ConsoleKey.Sleep: case ConsoleKey.Process:
                case ConsoleKey.Packet: case ConsoleKey.Attention: case ConsoleKey.CrSel: case ConsoleKey.ExSel:
                case ConsoleKey.EraseEndOfFile: case ConsoleKey.Play: case ConsoleKey.Zoom: case ConsoleKey.NoName:
                case ConsoleKey.Pa1:

                    if (isShift) { AppendPart("Shift"); }
                    if (isCtrl) { AppendPart("Ctrl"); }
                    if (isAlt) { AppendPart("Alt"); }
                    AppendPart(consoleKey.ToString());
                    return sb.ToString();
            }

            var c = key.KeyChar;
            var isDeadKey = false;
            if (char.IsControl(c))
            {
                // We have the virtual key code and Windows has a handy api to map that to the non-control
                // character that use for a friendly UI.
                //
                // If this fails, we rely on some hard coded control characters below.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // A heuristic to check for dead keys --
                    //   We got an 'OemXXX' ConsoleKey, '\0' key char, and no 'Ctrl' modifier. It's very likely generated by a dead key.
                    //   We check for 'Ctrl' modifier because it's easy to generate '\0' KeyChar and 'OemXXX' by combinding 'Ctrl' with
                    //   another special key, such as 'Ctrl+?' and 'Ctrl+;'.
                    isDeadKey = (c == '\0') && (consoleKey >= ConsoleKey.Oem1 && consoleKey <= ConsoleKey.Oem102) && !isCtrl;

                    if (!isDeadKey)
                    {
                        // A dead key could pass the above heuristic check, such as 'Shift+6' in US-INTL keyboard, which represents the
                        // diacritic '^' and generates 'D6' ConsoleKey, '\0' key char and 'Shift' modifier.
                        // For those dead keys we try again to identify them by calling the Win32 API 'ToUnicode'. This API doesn't work
                        // well with keyboards that are not natively supported by Windows, such as the Neo keyboard layout. Hopefully,
                        // dead keys of the non-natively-supported keyboard layouts are captured by our heuristic check above.
                        var keySansControl = new ConsoleKeyInfo(key.KeyChar, consoleKey, isShift, isAlt, control: false);
                        TryGetCharFromConsoleKey(keySansControl, ref c, ref isDeadKey);
                    }
                }
            }
            else if (isAlt && isCtrl)
            {
                // Not a control character, if we have Alt and Control both, then it was probably AltGr.
                isAlt = false;
                isCtrl = false;
            }

            string s;
            switch (c)
            {
                case ' ':
                    s = "Spacebar";
                    break;
                case '\x1b':
                    s = "Escape";
                    break;
                case '\x1c':
                    s = "\\";
                    isShift = false;
                    break;
                case '\x1d':
                    s = "]";
                    isShift = false;
                    break;
                case '\x1f':
                    s = "_";
                    isShift = false;
                    break;
                case '\x7f':
                    s = "Backspace";
                    break;

                // 'Ctrl+h' is represented as (keychar: 0x08, key: 0, mods: Control). In the case of 'Ctrl+h',
                // we don't want the keychar to be interpreted as 'Backspace'.
                case '\x08' when !isCtrl:
                    s = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Backspace" : "Ctrl+Backspace";
                    break;

                case '\0':
                    // This could be a dead key for a particular keyboard layout in Windows console.
                    // The dead key is not an issue when there is tty involved, so on non-Windows, `isDeadKey` is always false.
                    //
                    // When we believe it's a dead key, we use '\0' so it can be properly ignored during rendering.
                    // Otherwise, we use `@` in case `key.KeyChar = '\0'`. This is ugly but familiar.
                    s = isDeadKey ? c.ToString() : "@";
                    break;

                case char _ when (c >= 1 && c <= 26):
                    s = ((char)((isShift ? 'A' : 'a') + c - 1)).ToString();
                    isShift = false;
                    break;

                default:
                    s = c.ToString();
                    isShift = false;
                    break;
            }

            if (isShift) { AppendPart("Shift"); }
            if (isCtrl) { AppendPart("Ctrl"); }
            if (isAlt) { AppendPart("Alt"); }
            AppendPart(s);

            return sb.ToString();
        }
    }

    internal static class Keys
    {
        static PSKeyInfo Key(char c)               => PSKeyInfo.From(c);
        static PSKeyInfo Key(ConsoleKey key)       => PSKeyInfo.From(key);
        static PSKeyInfo Alt(char c)               => PSKeyInfo.WithAlt(c);
        static PSKeyInfo Alt(ConsoleKey key)       => PSKeyInfo.WithAlt(key);
        static PSKeyInfo Ctrl(char c)              => PSKeyInfo.WithCtrl(c);
        static PSKeyInfo Ctrl(ConsoleKey key)      => PSKeyInfo.WithCtrl(key);
        static PSKeyInfo Shift(ConsoleKey key)     => PSKeyInfo.WithShift(key);
        static PSKeyInfo CtrlShift(ConsoleKey key) => PSKeyInfo.WithShiftCtrl(key);
        static PSKeyInfo CtrlAlt(char c)           => PSKeyInfo.WithCtrlAlt(c);

        public static PSKeyInfo F1                  = Key(ConsoleKey.F1);
        public static PSKeyInfo F2                  = Key(ConsoleKey.F2);
        public static PSKeyInfo F3                  = Key(ConsoleKey.F3);
        public static PSKeyInfo F4                  = Key(ConsoleKey.F4);
        public static PSKeyInfo F5                  = Key(ConsoleKey.F5);
        public static PSKeyInfo F6                  = Key(ConsoleKey.F6);
        public static PSKeyInfo F7                  = Key(ConsoleKey.F7);
        public static PSKeyInfo F8                  = Key(ConsoleKey.F8);
        public static PSKeyInfo F9                  = Key(ConsoleKey.F9);
        public static PSKeyInfo F10                 = Key(ConsoleKey.F10);
        public static PSKeyInfo F11                 = Key(ConsoleKey.F11);
        public static PSKeyInfo F12                 = Key(ConsoleKey.F12);
        public static PSKeyInfo F13                 = Key(ConsoleKey.F13);
        public static PSKeyInfo F14                 = Key(ConsoleKey.F14);
        public static PSKeyInfo F15                 = Key(ConsoleKey.F15);
        public static PSKeyInfo F16                 = Key(ConsoleKey.F16);
        public static PSKeyInfo F17                 = Key(ConsoleKey.F17);
        public static PSKeyInfo F18                 = Key(ConsoleKey.F18);
        public static PSKeyInfo F19                 = Key(ConsoleKey.F19);
        public static PSKeyInfo F20                 = Key(ConsoleKey.F20);
        public static PSKeyInfo F21                 = Key(ConsoleKey.F21);
        public static PSKeyInfo F22                 = Key(ConsoleKey.F22);
        public static PSKeyInfo F23                 = Key(ConsoleKey.F23);
        public static PSKeyInfo F24                 = Key(ConsoleKey.F24);
        public static PSKeyInfo _0                  = Key('0');
        public static PSKeyInfo _1                  = Key('1');
        public static PSKeyInfo _2                  = Key('2');
        public static PSKeyInfo _3                  = Key('3');
        public static PSKeyInfo _4                  = Key('4');
        public static PSKeyInfo _5                  = Key('5');
        public static PSKeyInfo _6                  = Key('6');
        public static PSKeyInfo _7                  = Key('7');
        public static PSKeyInfo _8                  = Key('8');
        public static PSKeyInfo _9                  = Key('9');
        public static PSKeyInfo A                   = Key('a');
        public static PSKeyInfo B                   = Key('b');
        public static PSKeyInfo C                   = Key('c');
        public static PSKeyInfo D                   = Key('d');
        public static PSKeyInfo E                   = Key('e');
        public static PSKeyInfo F                   = Key('f');
        public static PSKeyInfo G                   = Key('g');
        public static PSKeyInfo H                   = Key('h');
        public static PSKeyInfo I                   = Key('i');
        public static PSKeyInfo J                   = Key('j');
        public static PSKeyInfo K                   = Key('k');
        public static PSKeyInfo L                   = Key('l');
        public static PSKeyInfo M                   = Key('m');
        public static PSKeyInfo N                   = Key('n');
        public static PSKeyInfo O                   = Key('o');
        public static PSKeyInfo P                   = Key('p');
        public static PSKeyInfo Q                   = Key('q');
        public static PSKeyInfo R                   = Key('r');
        public static PSKeyInfo S                   = Key('s');
        public static PSKeyInfo T                   = Key('t');
        public static PSKeyInfo U                   = Key('u');
        public static PSKeyInfo V                   = Key('v');
        public static PSKeyInfo W                   = Key('w');
        public static PSKeyInfo X                   = Key('x');
        public static PSKeyInfo Y                   = Key('y');
        public static PSKeyInfo Z                   = Key('z');
        public static PSKeyInfo ucA                 = Key('A');
        public static PSKeyInfo ucB                 = Key('B');
        public static PSKeyInfo ucC                 = Key('C');
        public static PSKeyInfo ucD                 = Key('D');
        public static PSKeyInfo ucE                 = Key('E');
        public static PSKeyInfo ucF                 = Key('F');
        public static PSKeyInfo ucG                 = Key('G');
        public static PSKeyInfo ucH                 = Key('H');
        public static PSKeyInfo ucI                 = Key('I');
        public static PSKeyInfo ucJ                 = Key('J');
        public static PSKeyInfo ucK                 = Key('K');
        public static PSKeyInfo ucL                 = Key('L');
        public static PSKeyInfo ucM                 = Key('M');
        public static PSKeyInfo ucN                 = Key('N');
        public static PSKeyInfo ucO                 = Key('O');
        public static PSKeyInfo ucP                 = Key('P');
        public static PSKeyInfo ucQ                 = Key('Q');
        public static PSKeyInfo ucR                 = Key('R');
        public static PSKeyInfo ucS                 = Key('S');
        public static PSKeyInfo ucT                 = Key('T');
        public static PSKeyInfo ucU                 = Key('U');
        public static PSKeyInfo ucV                 = Key('V');
        public static PSKeyInfo ucW                 = Key('W');
        public static PSKeyInfo ucX                 = Key('X');
        public static PSKeyInfo ucY                 = Key('Y');
        public static PSKeyInfo ucZ                 = Key('Z');
        public static PSKeyInfo Bang                = Key('!');
        public static PSKeyInfo At                  = Key('@');
        public static PSKeyInfo Pound               = Key('#');
        public static PSKeyInfo Dollar              = Key('$');
        public static PSKeyInfo Percent             = Key('%');
        public static PSKeyInfo Uphat               = Key('^');
        public static PSKeyInfo Ampersand           = Key('&');
        public static PSKeyInfo Star                = Key('*');
        public static PSKeyInfo LParen              = Key('(');
        public static PSKeyInfo RParen              = Key(')');
        public static PSKeyInfo Colon               = Key(':');
        public static PSKeyInfo Semicolon           = Key(';');
        public static PSKeyInfo Question            = Key('?');
        public static PSKeyInfo Slash               = Key('/');
        public static PSKeyInfo Tilde               = Key('~');
        public static PSKeyInfo Backtick            = Key('`');
        public static PSKeyInfo LCurly              = Key('{');
        public static PSKeyInfo LBracket            = Key('[');
        public static PSKeyInfo Pipe                = Key('|');
        public static PSKeyInfo Backslash           = Key('\\');
        public static PSKeyInfo RCurly              = Key('}');
        public static PSKeyInfo RBracket            = Key(']');
        public static PSKeyInfo SQuote              = Key('\'');
        public static PSKeyInfo DQuote              = Key('"');
        public static PSKeyInfo LessThan            = Key('<');
        public static PSKeyInfo Comma               = Key(',');
        public static PSKeyInfo GreaterThan         = Key('>');
        public static PSKeyInfo Period              = Key('.');
        public static PSKeyInfo Underbar            = Key('_');
        public static PSKeyInfo Minus               = Key('-');
        public static PSKeyInfo Plus                = Key('+');
        public static PSKeyInfo Eq                  = Key('=');
        public static PSKeyInfo Space               = Key(ConsoleKey.Spacebar);
        public static PSKeyInfo Backspace           = Key(ConsoleKey.Backspace);
        public static PSKeyInfo Delete              = Key(ConsoleKey.Delete);
        public static PSKeyInfo DownArrow           = Key(ConsoleKey.DownArrow);
        public static PSKeyInfo End                 = Key(ConsoleKey.End);
        public static PSKeyInfo Enter               = Key(ConsoleKey.Enter);
        public static PSKeyInfo Escape              = Key(ConsoleKey.Escape);
        public static PSKeyInfo Home                = Key(ConsoleKey.Home);
        public static PSKeyInfo LeftArrow           = Key(ConsoleKey.LeftArrow);
        public static PSKeyInfo PageUp              = Key(ConsoleKey.PageUp);
        public static PSKeyInfo PageDown            = Key(ConsoleKey.PageDown);
        public static PSKeyInfo RightArrow          = Key(ConsoleKey.RightArrow);
        public static PSKeyInfo Tab                 = Key(ConsoleKey.Tab);
        public static PSKeyInfo UpArrow             = Key(ConsoleKey.UpArrow);

        // Alt+Fn is unavailable or doesn't work on Linux.
        //   If you boot to a TTY, Alt+Fn switches to TTYn for n=1-6,
        //       otherwise the key is ignored (showkey -a never receives the key)
        //   If you boot to X, many Alt+Fn keys are bound by the system,
        //       those that aren't don't work because .Net doesn't handle them.
        public static PSKeyInfo AltF1               = Alt(ConsoleKey.F1);
        public static PSKeyInfo AltF2               = Alt(ConsoleKey.F2);
        public static PSKeyInfo AltF3               = Alt(ConsoleKey.F3);
        public static PSKeyInfo AltF4               = Alt(ConsoleKey.F4);
        public static PSKeyInfo AltF5               = Alt(ConsoleKey.F5);
        public static PSKeyInfo AltF6               = Alt(ConsoleKey.F6);
        public static PSKeyInfo AltF7               = Alt(ConsoleKey.F7);
        public static PSKeyInfo AltF8               = Alt(ConsoleKey.F8);
        public static PSKeyInfo AltF9               = Alt(ConsoleKey.F9);
        public static PSKeyInfo AltF10              = Alt(ConsoleKey.F10);
        public static PSKeyInfo AltF11              = Alt(ConsoleKey.F11);
        public static PSKeyInfo AltF12              = Alt(ConsoleKey.F12);
        public static PSKeyInfo AltF13              = Alt(ConsoleKey.F13);
        public static PSKeyInfo AltF14              = Alt(ConsoleKey.F14);
        public static PSKeyInfo AltF15              = Alt(ConsoleKey.F15);
        public static PSKeyInfo AltF16              = Alt(ConsoleKey.F16);
        public static PSKeyInfo AltF17              = Alt(ConsoleKey.F17);
        public static PSKeyInfo AltF18              = Alt(ConsoleKey.F18);
        public static PSKeyInfo AltF19              = Alt(ConsoleKey.F19);
        public static PSKeyInfo AltF20              = Alt(ConsoleKey.F20);
        public static PSKeyInfo AltF21              = Alt(ConsoleKey.F21);
        public static PSKeyInfo AltF22              = Alt(ConsoleKey.F22);
        public static PSKeyInfo AltF23              = Alt(ConsoleKey.F23);
        public static PSKeyInfo AltF24              = Alt(ConsoleKey.F24);
        public static PSKeyInfo Alt0                = Alt('0');
        public static PSKeyInfo Alt1                = Alt('1');
        public static PSKeyInfo Alt2                = Alt('2');
        public static PSKeyInfo Alt3                = Alt('3');
        public static PSKeyInfo Alt4                = Alt('4');
        public static PSKeyInfo Alt5                = Alt('5');
        public static PSKeyInfo Alt6                = Alt('6');
        public static PSKeyInfo Alt7                = Alt('7');
        public static PSKeyInfo Alt8                = Alt('8');
        public static PSKeyInfo Alt9                = Alt('9');
        public static PSKeyInfo AltA                = Alt('a');
        public static PSKeyInfo AltB                = Alt('b');
        public static PSKeyInfo AltC                = Alt('c');
        public static PSKeyInfo AltD                = Alt('d');
        public static PSKeyInfo AltE                = Alt('e');
        public static PSKeyInfo AltF                = Alt('f');
        public static PSKeyInfo AltG                = Alt('g');
        public static PSKeyInfo AltH                = Alt('h');
        public static PSKeyInfo AltI                = Alt('i');
        public static PSKeyInfo AltJ                = Alt('j');
        public static PSKeyInfo AltK                = Alt('k');
        public static PSKeyInfo AltL                = Alt('l');
        public static PSKeyInfo AltM                = Alt('m');
        public static PSKeyInfo AltN                = Alt('n');
        public static PSKeyInfo AltO                = Alt('o');
        public static PSKeyInfo AltP                = Alt('p');
        public static PSKeyInfo AltQ                = Alt('q');
        public static PSKeyInfo AltR                = Alt('r');
        public static PSKeyInfo AltS                = Alt('s');
        public static PSKeyInfo AltT                = Alt('t');
        public static PSKeyInfo AltU                = Alt('u');
        public static PSKeyInfo AltV                = Alt('v');
        public static PSKeyInfo AltW                = Alt('w');
        public static PSKeyInfo AltX                = Alt('x');
        public static PSKeyInfo AltY                = Alt('y');
        public static PSKeyInfo AltZ                = Alt('z');
        public static PSKeyInfo AltShiftB           = Alt('B');
        public static PSKeyInfo AltShiftF           = Alt('F');
        public static PSKeyInfo AltSpace            = Alt(ConsoleKey.Spacebar);  // !Windows, system menu.
        public static PSKeyInfo AltPeriod           = Alt('.');  // !Linux, CLR bug
        public static PSKeyInfo AltEquals           = Alt('=');  // !Linux, CLR bug
        public static PSKeyInfo AltMinus            = Alt('-');
        public static PSKeyInfo AltUnderbar         = Alt('_');  // !Linux, CLR bug
        public static PSKeyInfo AltBackspace        = Alt(ConsoleKey.Backspace);
        public static PSKeyInfo AltLess             = Alt('<');  // !Linux, CLR bug
        public static PSKeyInfo AltGreater          = Alt('>');  // !Linux, CLR bug
        public static PSKeyInfo AltQuestion         = Alt('?');  // !Linux, CLR bug

        public static PSKeyInfo CtrlAt              = Ctrl('@');
        public static PSKeyInfo CtrlSpace           = Ctrl(ConsoleKey.Spacebar); // !Linux, same as CtrlAt
        public static PSKeyInfo CtrlA               = Ctrl('a');
        public static PSKeyInfo CtrlB               = Ctrl('b');
        public static PSKeyInfo CtrlC               = Ctrl('c');
        public static PSKeyInfo CtrlD               = Ctrl('d');
        public static PSKeyInfo CtrlE               = Ctrl('e');
        public static PSKeyInfo CtrlF               = Ctrl('f');
        public static PSKeyInfo CtrlG               = Ctrl('g');
        public static PSKeyInfo CtrlH               = Ctrl('h'); // !Linux, generate (keychar: '\b', key: Backspace, mods: 0), same as CtrlBackspace
        public static PSKeyInfo CtrlI               = Ctrl('i'); // !Linux, generate (keychar: '\t', key: Tab,       mods: 0)
        public static PSKeyInfo CtrlJ               = Ctrl('j'); // !Linux, generate (keychar: '\n', key: Enter,     mods: 0)
        public static PSKeyInfo CtrlK               = Ctrl('k');
        public static PSKeyInfo CtrlL               = Ctrl('l');
        public static PSKeyInfo CtrlM               = Ctrl('m'); // !Linux, same as CtrlJ but 'showkey -a' shows they are different, CLR bug
        public static PSKeyInfo CtrlN               = Ctrl('n');
        public static PSKeyInfo CtrlO               = Ctrl('o');
        public static PSKeyInfo CtrlP               = Ctrl('p');
        public static PSKeyInfo CtrlQ               = Ctrl('q');
        public static PSKeyInfo CtrlR               = Ctrl('r');
        public static PSKeyInfo CtrlS               = Ctrl('s');
        public static PSKeyInfo CtrlT               = Ctrl('t');
        public static PSKeyInfo CtrlU               = Ctrl('u');
        public static PSKeyInfo CtrlV               = Ctrl('v');
        public static PSKeyInfo CtrlW               = Ctrl('w');
        public static PSKeyInfo CtrlX               = Ctrl('x');
        public static PSKeyInfo CtrlY               = Ctrl('y');
        public static PSKeyInfo CtrlZ               = Ctrl('z');
        public static PSKeyInfo CtrlLBracket        = Ctrl('[');
        public static PSKeyInfo CtrlBackslash       = Ctrl('\\');
        public static PSKeyInfo CtrlRBracket        = Ctrl(']');
        public static PSKeyInfo CtrlCaret           = Ctrl('^');
        public static PSKeyInfo CtrlUnderbar        = Ctrl('_');
        public static PSKeyInfo CtrlBackspace       = Ctrl(ConsoleKey.Backspace);
        public static PSKeyInfo CtrlDelete          = Ctrl(ConsoleKey.Delete); // !Linux
        public static PSKeyInfo CtrlEnd             = Ctrl(ConsoleKey.End); // !Linux
        public static PSKeyInfo CtrlHome            = Ctrl(ConsoleKey.Home); // !Linux
        public static PSKeyInfo CtrlPageUp          = Ctrl(ConsoleKey.PageUp); // !Linux
        public static PSKeyInfo CtrlPageDown        = Ctrl(ConsoleKey.PageDown); // !Linux
        public static PSKeyInfo CtrlLeftArrow       = Ctrl(ConsoleKey.LeftArrow); // !Linux
        public static PSKeyInfo CtrlRightArrow      = Ctrl(ConsoleKey.RightArrow); // !Linux
        public static PSKeyInfo CtrlEnter           = Ctrl(ConsoleKey.Enter); // !Linux

        public static PSKeyInfo ShiftF3             = Shift(ConsoleKey.F3);
        public static PSKeyInfo ShiftF8             = Shift(ConsoleKey.F8);
        public static PSKeyInfo ShiftEnd            = Shift(ConsoleKey.End);
        public static PSKeyInfo ShiftEnter          = Shift(ConsoleKey.Enter);
        public static PSKeyInfo ShiftHome           = Shift(ConsoleKey.Home);
        public static PSKeyInfo ShiftPageUp         = Shift(ConsoleKey.PageUp);
        public static PSKeyInfo ShiftPageDown       = Shift(ConsoleKey.PageDown);
        public static PSKeyInfo ShiftLeftArrow      = Shift(ConsoleKey.LeftArrow);
        public static PSKeyInfo ShiftRightArrow     = Shift(ConsoleKey.RightArrow);
        public static PSKeyInfo ShiftUpArrow        = Shift(ConsoleKey.UpArrow);
        public static PSKeyInfo ShiftDownArrow      = Shift(ConsoleKey.DownArrow);
        public static PSKeyInfo ShiftTab            = Shift(ConsoleKey.Tab); // !Linux, same as Tab
        public static PSKeyInfo ShiftInsert         = Shift(ConsoleKey.Insert);

        public static PSKeyInfo CtrlShiftC          = Ctrl('C'); // !Linux
        public static PSKeyInfo CtrlShiftEnter      = CtrlShift(ConsoleKey.Enter);
        public static PSKeyInfo CtrlShiftLeftArrow  = CtrlShift(ConsoleKey.LeftArrow);
        public static PSKeyInfo CtrlShiftRightArrow = CtrlShift(ConsoleKey.RightArrow);

        public static PSKeyInfo CtrlAltY            = CtrlAlt('y');
        public static PSKeyInfo CtrlAltRBracket     = CtrlAlt(']');
        public static PSKeyInfo CtrlAltQuestion     = CtrlAlt('?');
    }
}
