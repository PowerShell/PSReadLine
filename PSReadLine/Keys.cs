/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using PSKeyInfo = System.ConsoleKeyInfo;

[assembly: DebuggerDisplay("Key={Key}, KeyChar={KeyChar,x}, Mods={Modifiers}", Target = typeof(PSKeyInfo))]

namespace Microsoft.PowerShell
{
    internal static class Keys
    {
        static PSKeyInfo Key(char c)
        {
            return new PSKeyInfo(c, 0, shift: false, alt: false, control: false);
        }

        static PSKeyInfo Key(ConsoleKey key)
        {
            return new PSKeyInfo('\0', key, shift: false, alt: false, control: false);
        }

        static PSKeyInfo Alt(char c)
        {
            return new PSKeyInfo(c, 0, shift: false, alt: true, control: false);
        }

        static PSKeyInfo Alt(ConsoleKey key)
        {
            return new PSKeyInfo('\0', key, shift: false, alt: true, control: false);
        }

        static PSKeyInfo Ctrl(char c)
        {
            return new PSKeyInfo(c, 0, shift: false, alt: false, control: true);
        }

        static PSKeyInfo Ctrl(ConsoleKey key)
        {
            return new PSKeyInfo('\0', key, shift: false, alt: false, control: true);
        }

        static PSKeyInfo Shift(ConsoleKey key)
        {
            return new PSKeyInfo('\0', key, shift: true, alt: false, control: false);
        }

        static PSKeyInfo CtrlShift(char c)
        {
            return new PSKeyInfo(c, 0, shift: true, alt: false, control: true);
        }

        static PSKeyInfo CtrlShift(ConsoleKey key)
        {
            return new PSKeyInfo('\0', key, shift: true, alt: false, control: true);
        }

        static PSKeyInfo CtrlAlt(char c)
        {
            ConsoleKey key = 0;
            bool shift = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var keyWithMods = WindowsKeyScan(c);
                if (keyWithMods.HasValue)
                {
                    shift = (keyWithMods.Value.Modifiers & ConsoleModifiers.Shift) != 0;
                    key = keyWithMods.Value.Key;
                }
                c = '\0';
            }
            else
            {
                if (c >= 'a' && c <= 'z')
                {
                    c = (char)(c - 'a' + 1);
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    c = (char)(c - 'A' + 1);
                }
            }
            return new PSKeyInfo(c, key, shift, alt: true, control: true);
        }

        public static PSKeyInfo F1                  = Key(ConsoleKey.F1);
        public static PSKeyInfo F2                  = Key(ConsoleKey.F2);
        public static PSKeyInfo F3                  = Key(ConsoleKey.F3);
        public static PSKeyInfo F4                  = Key(ConsoleKey.F4);
        public static PSKeyInfo F5                  = Key(ConsoleKey.F5);
        public static PSKeyInfo F6                  = Key(ConsoleKey.F6);
        public static PSKeyInfo F7                  = Key(ConsoleKey.F7);
        public static PSKeyInfo F8                  = Key(ConsoleKey.F8);
        public static PSKeyInfo F9                  = Key(ConsoleKey.F9);
        public static PSKeyInfo Fl0                 = Key(ConsoleKey.F10);
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
        public static PSKeyInfo Space               = Key(' ');
        public static PSKeyInfo Backspace           = Key(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '\x08' : '\x7f');
        public static PSKeyInfo Delete              = Key(ConsoleKey.Delete);
        public static PSKeyInfo DownArrow           = Key(ConsoleKey.DownArrow);
        public static PSKeyInfo End                 = Key(ConsoleKey.End);
        public static PSKeyInfo Enter               = Key(ConsoleKey.Enter);
        public static PSKeyInfo Escape              = Key('\x1b');
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
        public static PSKeyInfo AltFl0              = Alt(ConsoleKey.F10);
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
        public static PSKeyInfo AltSpace            = Alt(' ');  // !Windows, system menu.
        public static PSKeyInfo AltPeriod           = Alt('.');  // !Linux, CLR bug
        public static PSKeyInfo AltEquals           = Alt('=');  // !Linux, CLR bug
        public static PSKeyInfo AltMinus            = Alt('-');
        public static PSKeyInfo AltUnderbar         = Alt('_');  // !Linux, CLR bug
        public static PSKeyInfo AltBackspace        = Alt(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '\x08' : '\x7f');
        public static PSKeyInfo AltLess             = Alt('<');  // !Linux, CLR bug
        public static PSKeyInfo AltGreater          = Alt('>');  // !Linux, CLR bug
        public static PSKeyInfo AltQuestion         = Alt('?');  // !Linux, CLR bug

        public static PSKeyInfo CtrlAt              = Ctrl('\x00');
        public static PSKeyInfo CtrlSpace           = Ctrl(' '); // !Linux, same as CtrlAt
        public static PSKeyInfo CtrlA               = Ctrl('\x01');
        public static PSKeyInfo CtrlB               = Ctrl('\x02');
        public static PSKeyInfo CtrlC               = Ctrl('\x03');
        public static PSKeyInfo CtrlD               = Ctrl('\x04');
        public static PSKeyInfo CtrlE               = Ctrl('\x05');
        public static PSKeyInfo CtrlF               = Ctrl('\x06');
        public static PSKeyInfo CtrlG               = Ctrl('\a');
        public static PSKeyInfo CtrlH               = Ctrl('\b'); // !Linux, generate (keychar: '\b', key: Backspace, mods: 0), same as CtrlBackspace
        public static PSKeyInfo CtrlI               = Ctrl('\t'); // !Linux, generate (keychar: '\t', key: Tab,       mods: 0)
        public static PSKeyInfo CtrlJ               = Ctrl('\n'); // !Linux, generate (keychar: '\n', key: Enter,     mods: 0)
        public static PSKeyInfo CtrlK               = Ctrl('\v');
        public static PSKeyInfo CtrlL               = Ctrl('\f');
        public static PSKeyInfo CtrlM               = Ctrl('\r'); // !Linux, same as CtrlJ but 'showkey -a' shows they are different, CLR bug
        public static PSKeyInfo CtrlN               = Ctrl('\x0e');
        public static PSKeyInfo CtrlO               = Ctrl('\x0f');
        public static PSKeyInfo CtrlP               = Ctrl('\x10');
        public static PSKeyInfo CtrlQ               = Ctrl('\x11');
        public static PSKeyInfo CtrlR               = Ctrl('\x12');
        public static PSKeyInfo CtrlS               = Ctrl('\x13');
        public static PSKeyInfo CtrlT               = Ctrl('\x14');
        public static PSKeyInfo CtrlU               = Ctrl('\x15');
        public static PSKeyInfo CtrlV               = Ctrl('\x16');
        public static PSKeyInfo CtrlW               = Ctrl('\x17');
        public static PSKeyInfo CtrlX               = Ctrl('\x18');
        public static PSKeyInfo CtrlY               = Ctrl('\x19');
        public static PSKeyInfo CtrlZ               = Ctrl('\x1a');
        public static PSKeyInfo CtrlLBracket        = Ctrl('\x1b');
        public static PSKeyInfo CtrlBackslash       = Ctrl('\x1c');
        public static PSKeyInfo CtrlRBracket        = Ctrl('\x1d');
        public static PSKeyInfo CtrlCaret           = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CtrlShift('\x1e') : Ctrl('\x1e');
        public static PSKeyInfo CtrlUnderbar        = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? CtrlShift('\x1f') : Ctrl('\x1f');
        public static PSKeyInfo CtrlBackspace       = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Ctrl('\x7f') : Key('\x08');
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
        public static PSKeyInfo ShiftTab            = Shift(ConsoleKey.Tab); // !Linux, same as Tab
        public static PSKeyInfo ShiftInsert         = Shift(ConsoleKey.Insert);

        public static PSKeyInfo CtrlShiftC          = CtrlShift('\x03'); // !Linux
        public static PSKeyInfo CtrlShiftEnter      = CtrlShift(ConsoleKey.Enter);
        public static PSKeyInfo CtrlShiftLeftArrow  = CtrlShift(ConsoleKey.LeftArrow);
        public static PSKeyInfo CtrlShiftRightArrow = CtrlShift(ConsoleKey.RightArrow);

        public static PSKeyInfo CtrlAltY            = CtrlAlt('y');
        public static PSKeyInfo CtrlAltRBracket     = CtrlAlt(']');
        public static PSKeyInfo CtrlAltQuestion     = CtrlAlt('?');

        public static PSKeyInfo VolumeUp   = Key(ConsoleKey.VolumeUp);
        public static PSKeyInfo VolumeDown = Key(ConsoleKey.VolumeDown);
        public static PSKeyInfo VolumeMute = Key(ConsoleKey.VolumeMute);

        [DllImport("user32.dll")]
        public static extern int VkKeyScan(short wAsciiVal);

        static KeyWithModifiers? WindowsKeyScan(char c)
        {
            var scan = VkKeyScan((short)c);
            if (scan == -1)
            {
                return null;
            }
            var key = (ConsoleKey)(scan & 0xff);
            var mods = default(ConsoleModifiers);
            if ((scan & 0x100) != 0) { mods |= ConsoleModifiers.Shift; }
            if ((scan & 0x200) != 0) { mods |= ConsoleModifiers.Control; }
            if ((scan & 0x400) != 0) { mods |= ConsoleModifiers.Alt; }
            return new KeyWithModifiers(key, mods);
        }

        struct KeyWithModifiers
        {
            public KeyWithModifiers(ConsoleKey key, ConsoleModifiers mods = 0)
            {
                Key = key;
                Modifiers = mods;
            }
            public ConsoleKey Key { get; }
            public ConsoleModifiers Modifiers { get; }

            public override bool Equals(object other)
            {
                if (!(other is KeyWithModifiers)) return false;
                var obj = (KeyWithModifiers)other;
                return Key == obj.Key && Modifiers == obj.Modifiers;
            }

            public override int GetHashCode()
            {
                int h1 = Key.GetHashCode();
                int h2 = Modifiers.GetHashCode();
                // This is based on Tuple.GetHashCode
                return unchecked(((h1 << 5) + h1) ^ h2);
            }
        }

        private static readonly Dictionary<KeyWithModifiers, char> CtrlKeyToKeyCharMap;

        static Keys()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CtrlKeyToKeyCharMap = new Dictionary<KeyWithModifiers, char>();
                // These mapping are needed to support different keyboard
                // layouts, e.g. ']' is Oem6 with an English keyboard, but
                // Oem5 with a Portuguese keyboard.
                foreach (char c in "`~!@#$%^&*()-_=+[{]}\\|;:'\",<.>/?")
                {
                    var keyWithMods = WindowsKeyScan(c);
                    if (keyWithMods.HasValue)
                    {
                        CtrlKeyToKeyCharMap.Add(keyWithMods.Value, c);
                    }
                }
            }
        }

        internal static bool IgnoreKeyChar(this PSKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.F1:
                case ConsoleKey.F2:
                case ConsoleKey.F3:
                case ConsoleKey.F4:
                case ConsoleKey.F5:
                case ConsoleKey.F6:
                case ConsoleKey.F7:
                case ConsoleKey.F8:
                case ConsoleKey.F9:
                case ConsoleKey.F10:
                case ConsoleKey.F11:
                case ConsoleKey.F12:
                case ConsoleKey.F13:
                case ConsoleKey.F14:
                case ConsoleKey.F15:
                case ConsoleKey.F16:
                case ConsoleKey.F17:
                case ConsoleKey.F18:
                case ConsoleKey.F19:
                case ConsoleKey.F20:
                case ConsoleKey.F21:
                case ConsoleKey.F22:
                case ConsoleKey.F23:
                case ConsoleKey.F24:
                case ConsoleKey.Delete:
                case ConsoleKey.DownArrow:
                case ConsoleKey.End:
                case ConsoleKey.Enter:
                case ConsoleKey.Home:
                case ConsoleKey.Insert:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                case ConsoleKey.RightArrow:
                case ConsoleKey.Tab:
                case ConsoleKey.UpArrow:
                case ConsoleKey.VolumeUp:
                case ConsoleKey.VolumeDown:
                case ConsoleKey.VolumeMute:
                    return true;
            }

            return false;
        }

        private static ConsoleModifiers NormalizeModifiers(this PSKeyInfo key)
        {
            var keyChar = key.IgnoreKeyChar() ? key.KeyChar : key.NormalizeKeyChar();
            var result = key.Modifiers;
            if (!char.IsControl(keyChar))
            {
                // Ignore Shift state unless it's a control character.
                result = result & ~ConsoleModifiers.Shift;
            }
            return result;
        }

        internal static char NormalizeKeyChar(this PSKeyInfo key)
        {
            if (key.KeyChar == '\0' && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                if (key.Key >= ConsoleKey.A && key.Key <= ConsoleKey.Z)
                {
                    return (char)(key.Key - ConsoleKey.A + 1);
                }

                int d = key.Key >= ConsoleKey.D0 && key.Key <= ConsoleKey.D9
                    ? key.Key - ConsoleKey.D0
                    : key.Key >= ConsoleKey.NumPad0 && key.Key <= ConsoleKey.NumPad9
                        ? key.Key - ConsoleKey.NumPad0
                        : -1;

                switch (d) {
                    case 2: return '\0';
                    case 6: return '\x1e';
                    case 0: case 1: case 3: case 4:
                    case 5: case 7: case 8: case 9:
                        return (char)('0' + d);
                }

                // On Windows, PSKeyInfo.Key is something we wanted to ignore, but
                // a few bindings force us to do something with it.
                var mods = key.Modifiers & ConsoleModifiers.Shift;
                var keyWithMods = new KeyWithModifiers(key.Key, mods);
                if (CtrlKeyToKeyCharMap != null && CtrlKeyToKeyCharMap.TryGetValue(keyWithMods, out var keyChar))
                    return keyChar;
            }
            return key.KeyChar;
        }

        internal static bool EqualsNormalized(this PSKeyInfo x, PSKeyInfo y)
        {
            // In the common case, we mask something out of the PSKeyInfo
            // e.g. Shift or somewhat meaningless Key (like Oem6) which might vary
            // in different keyboard layouts.
            //
            // That said, if all fields compare, it's a match and we return that.
            if (x.Key == y.Key && x.KeyChar == y.KeyChar && x.Modifiers == y.Modifiers)
                return true;

            // We ignore Shift state as that can vary in different keyboard layouts.
            var xMods = NormalizeModifiers(x);
            var yMods = NormalizeModifiers(y);

            if (xMods != yMods)
                return false;

            // If we don't have a character, we probably masked it out (except for Ctrl+@)
            // when building our key bindings, so compare Key instead.
            return x.IgnoreKeyChar() || y.IgnoreKeyChar()
                ? x.Key == y.Key
                : x.NormalizeKeyChar() == y.NormalizeKeyChar();
        }

        internal static int GetNormalizedHashCode(this PSKeyInfo obj)
        {
            // Because a comparison of two PSKeyInfo objects is a comparison of the
            // combination of the ConsoleKey and Modifiers, we must combine their hashes.
            // Note that if the ConsoleKey is default, we must fall back to the KeyChar,
            // otherwise every non-special key will compare as the same.
            int h1 = obj.IgnoreKeyChar()
                ? obj.Key.GetHashCode()
                : obj.NormalizeKeyChar().GetHashCode();

            int h2 = NormalizeModifiers(obj).GetHashCode();

            // This is based on Tuple.GetHashCode
            return unchecked(((h1 << 5) + h1) ^ h2);
        }

        internal static bool IsUnmodifiedChar(this PSKeyInfo key, char c)
        {
            return key.KeyChar == c &&
                   (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0;
        }

        public static string ToGestureString(this PSKeyInfo key)
        {
            var useKeyEnum = key.IgnoreKeyChar();

            var mods = key.Modifiers;

            var sb = new StringBuilder();

            var isShift = (mods & ConsoleModifiers.Shift) != 0;
            var isCtrl = (mods & ConsoleModifiers.Control) != 0;
            var isAlt = (mods & ConsoleModifiers.Alt) != 0;

            if (useKeyEnum && isShift)
            {
                sb.Append("Shift");
            }

            if (isCtrl)
            {
                if (sb.Length > 0) sb.Append("+");
                sb.Append("Ctrl");
            }

            if (isAlt)
            {
                if (sb.Length > 0) sb.Append("+");
                sb.Append("Alt");
            }

            if (sb.Length > 0) sb.Append("+");

            if (useKeyEnum)
            {
                sb.Append(key.Key);
            }
            else
            {
                var c = key.NormalizeKeyChar();
                string s;
                switch (c)
                {
                    case ' '   : s = "Space";     break;
                    case '\x1b': s = "Escape";    break;
                    case '\x1c': s = "\\";        break;
                    case '\x1d': s = "]";         break;
                    case '\x1f': s = "_";         break;
                    case '\x7f': s = "Backspace"; break;

                    // 'Ctrl+h' is represented as (keychar: 0x08, key: 0, mods: Control). In the case of 'Ctrl+h',
                    // we don't want the keychar to be interpreted as 'Backspace'.
                    case '\x08' when !isCtrl:
                        s = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Backspace" : "Ctrl+Backspace";
                        break;

                    case char _ when c <= 26:
                        s = ((char)((isShift ? 'A' : 'a') + c - 1)).ToString();
                        break;

                    default:
                        s = c.ToString();
                        break;
                }
                sb.Append(s);
            }

            return sb.ToString();
        }
    }
}
