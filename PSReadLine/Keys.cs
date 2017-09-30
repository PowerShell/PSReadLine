/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

[assembly: DebuggerDisplay("Key={Key}, KeyChar={KeyChar,x}, Mods={Modifiers}", Target = typeof(ConsoleKeyInfo))]

namespace Microsoft.PowerShell
{
    internal static class Keys
    {
        static ConsoleKeyInfo Key(char c)
        {
            return new ConsoleKeyInfo(c, 0, shift: false, alt: false, control: false);
        }

        static ConsoleKeyInfo Key(ConsoleKey key)
        {
            return new ConsoleKeyInfo('\0', key, shift: false, alt: false, control: false);
        }

        static ConsoleKeyInfo Alt(char c)
        {
            return new ConsoleKeyInfo(c, 0, shift: false, alt: true, control: false);
        }

        static ConsoleKeyInfo Alt(ConsoleKey key)
        {
            return new ConsoleKeyInfo('\0', key, shift: false, alt: true, control: false);
        }

        static ConsoleKeyInfo Ctrl(char c)
        {
            return new ConsoleKeyInfo(c, 0, shift: false, alt: false, control: true);
        }

        static ConsoleKeyInfo Ctrl(ConsoleKey key)
        {
            return new ConsoleKeyInfo('\0', key, shift: false, alt: false, control: true);
        }

        static ConsoleKeyInfo Shift(ConsoleKey key)
        {
            return new ConsoleKeyInfo('\0', key, shift: true, alt: false, control: false);
        }

        static ConsoleKeyInfo CtrlShift(char c)
        {
            return new ConsoleKeyInfo(c, 0, shift: true, alt: false, control: true);
        }

        static ConsoleKeyInfo CtrlShift(ConsoleKey key)
        {
            return new ConsoleKeyInfo('\0', key, shift: true, alt: false, control: true);
        }

        static ConsoleKeyInfo CtrlAlt(char c)
        {
            ConsoleKey key = 0;
            bool shift = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var scan = VkKeyScan((short) c);
                if ((scan & 0x100) != 0) { shift = true; }
                key = (ConsoleKey)(scan & 0xff);
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
            return new ConsoleKeyInfo(c, key, shift, alt: true, control: true);
        }

        public static ConsoleKeyInfo F1                  = Key(ConsoleKey.F1);
        public static ConsoleKeyInfo F2                  = Key(ConsoleKey.F2);
        public static ConsoleKeyInfo F3                  = Key(ConsoleKey.F3);
        public static ConsoleKeyInfo F4                  = Key(ConsoleKey.F4);
        public static ConsoleKeyInfo F5                  = Key(ConsoleKey.F5);
        public static ConsoleKeyInfo F6                  = Key(ConsoleKey.F6);
        public static ConsoleKeyInfo F7                  = Key(ConsoleKey.F7);
        public static ConsoleKeyInfo F8                  = Key(ConsoleKey.F8);
        public static ConsoleKeyInfo F9                  = Key(ConsoleKey.F9);
        public static ConsoleKeyInfo Fl0                 = Key(ConsoleKey.F10);
        public static ConsoleKeyInfo F11                 = Key(ConsoleKey.F11);
        public static ConsoleKeyInfo F12                 = Key(ConsoleKey.F12);
        public static ConsoleKeyInfo F13                 = Key(ConsoleKey.F13);
        public static ConsoleKeyInfo F14                 = Key(ConsoleKey.F14);
        public static ConsoleKeyInfo F15                 = Key(ConsoleKey.F15);
        public static ConsoleKeyInfo F16                 = Key(ConsoleKey.F16);
        public static ConsoleKeyInfo F17                 = Key(ConsoleKey.F17);
        public static ConsoleKeyInfo F18                 = Key(ConsoleKey.F18);
        public static ConsoleKeyInfo F19                 = Key(ConsoleKey.F19);
        public static ConsoleKeyInfo F20                 = Key(ConsoleKey.F20);
        public static ConsoleKeyInfo F21                 = Key(ConsoleKey.F21);
        public static ConsoleKeyInfo F22                 = Key(ConsoleKey.F22);
        public static ConsoleKeyInfo F23                 = Key(ConsoleKey.F23);
        public static ConsoleKeyInfo F24                 = Key(ConsoleKey.F24);
        public static ConsoleKeyInfo _0                  = Key('0');
        public static ConsoleKeyInfo _1                  = Key('1');
        public static ConsoleKeyInfo _2                  = Key('2');
        public static ConsoleKeyInfo _3                  = Key('3');
        public static ConsoleKeyInfo _4                  = Key('4');
        public static ConsoleKeyInfo _5                  = Key('5');
        public static ConsoleKeyInfo _6                  = Key('6');
        public static ConsoleKeyInfo _7                  = Key('7');
        public static ConsoleKeyInfo _8                  = Key('8');
        public static ConsoleKeyInfo _9                  = Key('9');
        public static ConsoleKeyInfo A                   = Key('a');
        public static ConsoleKeyInfo B                   = Key('b');
        public static ConsoleKeyInfo C                   = Key('c');
        public static ConsoleKeyInfo D                   = Key('d');
        public static ConsoleKeyInfo E                   = Key('e');
        public static ConsoleKeyInfo F                   = Key('f');
        public static ConsoleKeyInfo G                   = Key('g');
        public static ConsoleKeyInfo H                   = Key('h');
        public static ConsoleKeyInfo I                   = Key('i');
        public static ConsoleKeyInfo J                   = Key('j');
        public static ConsoleKeyInfo K                   = Key('k');
        public static ConsoleKeyInfo L                   = Key('l');
        public static ConsoleKeyInfo M                   = Key('m');
        public static ConsoleKeyInfo N                   = Key('n');
        public static ConsoleKeyInfo O                   = Key('o');
        public static ConsoleKeyInfo P                   = Key('p');
        public static ConsoleKeyInfo Q                   = Key('q');
        public static ConsoleKeyInfo R                   = Key('r');
        public static ConsoleKeyInfo S                   = Key('s');
        public static ConsoleKeyInfo T                   = Key('t');
        public static ConsoleKeyInfo U                   = Key('u');
        public static ConsoleKeyInfo V                   = Key('v');
        public static ConsoleKeyInfo W                   = Key('w');
        public static ConsoleKeyInfo X                   = Key('x');
        public static ConsoleKeyInfo Y                   = Key('y');
        public static ConsoleKeyInfo Z                   = Key('z');
        public static ConsoleKeyInfo ucA                 = Key('A');
        public static ConsoleKeyInfo ucB                 = Key('B');
        public static ConsoleKeyInfo ucC                 = Key('C');
        public static ConsoleKeyInfo ucD                 = Key('D');
        public static ConsoleKeyInfo ucE                 = Key('E');
        public static ConsoleKeyInfo ucF                 = Key('F');
        public static ConsoleKeyInfo ucG                 = Key('G');
        public static ConsoleKeyInfo ucH                 = Key('H');
        public static ConsoleKeyInfo ucI                 = Key('I');
        public static ConsoleKeyInfo ucJ                 = Key('J');
        public static ConsoleKeyInfo ucK                 = Key('K');
        public static ConsoleKeyInfo ucL                 = Key('L');
        public static ConsoleKeyInfo ucM                 = Key('M');
        public static ConsoleKeyInfo ucN                 = Key('N');
        public static ConsoleKeyInfo ucO                 = Key('O');
        public static ConsoleKeyInfo ucP                 = Key('P');
        public static ConsoleKeyInfo ucQ                 = Key('Q');
        public static ConsoleKeyInfo ucR                 = Key('R');
        public static ConsoleKeyInfo ucS                 = Key('S');
        public static ConsoleKeyInfo ucT                 = Key('T');
        public static ConsoleKeyInfo ucU                 = Key('U');
        public static ConsoleKeyInfo ucV                 = Key('V');
        public static ConsoleKeyInfo ucW                 = Key('W');
        public static ConsoleKeyInfo ucX                 = Key('X');
        public static ConsoleKeyInfo ucY                 = Key('Y');
        public static ConsoleKeyInfo ucZ                 = Key('Z');
        public static ConsoleKeyInfo Bang                = Key('!');
        public static ConsoleKeyInfo At                  = Key('@');
        public static ConsoleKeyInfo Pound               = Key('#');
        public static ConsoleKeyInfo Dollar              = Key('$');
        public static ConsoleKeyInfo Percent             = Key('%');
        public static ConsoleKeyInfo Uphat               = Key('^');
        public static ConsoleKeyInfo Ampersand           = Key('&');
        public static ConsoleKeyInfo Star                = Key('*');
        public static ConsoleKeyInfo LParen              = Key('(');
        public static ConsoleKeyInfo RParen              = Key(')');
        public static ConsoleKeyInfo Colon               = Key(':');
        public static ConsoleKeyInfo Semicolon           = Key(';');
        public static ConsoleKeyInfo Question            = Key('?');
        public static ConsoleKeyInfo Slash               = Key('/');
        public static ConsoleKeyInfo Tilde               = Key('~');
        public static ConsoleKeyInfo Backtick            = Key('`');
        public static ConsoleKeyInfo LCurly              = Key('{');
        public static ConsoleKeyInfo LBracket            = Key('[');
        public static ConsoleKeyInfo Pipe                = Key('|');
        public static ConsoleKeyInfo Backslash           = Key('\\');
        public static ConsoleKeyInfo RCurly              = Key('}');
        public static ConsoleKeyInfo RBracket            = Key(']');
        public static ConsoleKeyInfo SQuote              = Key('\'');
        public static ConsoleKeyInfo DQuote              = Key('"');
        public static ConsoleKeyInfo LessThan            = Key('<');
        public static ConsoleKeyInfo Comma               = Key(',');
        public static ConsoleKeyInfo GreaterThan         = Key('>');
        public static ConsoleKeyInfo Period              = Key('.');
        public static ConsoleKeyInfo Underbar            = Key('_');
        public static ConsoleKeyInfo Minus               = Key('-');
        public static ConsoleKeyInfo Plus                = Key('+');
        public static ConsoleKeyInfo Eq                  = Key('=');
        public static ConsoleKeyInfo Space               = Key(' ');
        public static ConsoleKeyInfo Backspace           = Key(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '\x08' : '\x7f');
        public static ConsoleKeyInfo Delete              = Key(ConsoleKey.Delete);
        public static ConsoleKeyInfo DownArrow           = Key(ConsoleKey.DownArrow);
        public static ConsoleKeyInfo End                 = Key(ConsoleKey.End);
        public static ConsoleKeyInfo Enter               = Key(ConsoleKey.Enter);
        public static ConsoleKeyInfo Escape              = Key('\x1b');
        public static ConsoleKeyInfo Home                = Key(ConsoleKey.Home);
        public static ConsoleKeyInfo LeftArrow           = Key(ConsoleKey.LeftArrow);
        public static ConsoleKeyInfo PageUp              = Key(ConsoleKey.PageUp);
        public static ConsoleKeyInfo PageDown            = Key(ConsoleKey.PageDown);
        public static ConsoleKeyInfo RightArrow          = Key(ConsoleKey.RightArrow);
        public static ConsoleKeyInfo Tab                 = Key(ConsoleKey.Tab);
        public static ConsoleKeyInfo UpArrow             = Key(ConsoleKey.UpArrow);


        // Alt+Fn is unavailable or doesn't work on Linux.
        //   If you boot to a TTY, Alt+Fn switches to TTYn for n=1-6,
        //       otherwise the key is ignored (showkey -a never receives the key)
        //   If you boot to X, many Alt+Fn keys are bound by the system,
        //       those that aren't don't work because .Net doesn't handle them.
        public static ConsoleKeyInfo AltF1               = Alt(ConsoleKey.F1);
        public static ConsoleKeyInfo AltF2               = Alt(ConsoleKey.F2);
        public static ConsoleKeyInfo AltF3               = Alt(ConsoleKey.F3);
        public static ConsoleKeyInfo AltF4               = Alt(ConsoleKey.F4);
        public static ConsoleKeyInfo AltF5               = Alt(ConsoleKey.F5);
        public static ConsoleKeyInfo AltF6               = Alt(ConsoleKey.F6);
        public static ConsoleKeyInfo AltF7               = Alt(ConsoleKey.F7);
        public static ConsoleKeyInfo AltF8               = Alt(ConsoleKey.F8);
        public static ConsoleKeyInfo AltF9               = Alt(ConsoleKey.F9);
        public static ConsoleKeyInfo AltFl0              = Alt(ConsoleKey.F10);
        public static ConsoleKeyInfo AltF11              = Alt(ConsoleKey.F11);
        public static ConsoleKeyInfo AltF12              = Alt(ConsoleKey.F12);
        public static ConsoleKeyInfo AltF13              = Alt(ConsoleKey.F13);
        public static ConsoleKeyInfo AltF14              = Alt(ConsoleKey.F14);
        public static ConsoleKeyInfo AltF15              = Alt(ConsoleKey.F15);
        public static ConsoleKeyInfo AltF16              = Alt(ConsoleKey.F16);
        public static ConsoleKeyInfo AltF17              = Alt(ConsoleKey.F17);
        public static ConsoleKeyInfo AltF18              = Alt(ConsoleKey.F18);
        public static ConsoleKeyInfo AltF19              = Alt(ConsoleKey.F19);
        public static ConsoleKeyInfo AltF20              = Alt(ConsoleKey.F20);
        public static ConsoleKeyInfo AltF21              = Alt(ConsoleKey.F21);
        public static ConsoleKeyInfo AltF22              = Alt(ConsoleKey.F22);
        public static ConsoleKeyInfo AltF23              = Alt(ConsoleKey.F23);
        public static ConsoleKeyInfo AltF24              = Alt(ConsoleKey.F24);
        public static ConsoleKeyInfo Alt0                = Alt('0');
        public static ConsoleKeyInfo Alt1                = Alt('1');
        public static ConsoleKeyInfo Alt2                = Alt('2');
        public static ConsoleKeyInfo Alt3                = Alt('3');
        public static ConsoleKeyInfo Alt4                = Alt('4');
        public static ConsoleKeyInfo Alt5                = Alt('5');
        public static ConsoleKeyInfo Alt6                = Alt('6');
        public static ConsoleKeyInfo Alt7                = Alt('7');
        public static ConsoleKeyInfo Alt8                = Alt('8');
        public static ConsoleKeyInfo Alt9                = Alt('9');
        public static ConsoleKeyInfo AltA                = Alt('a');
        public static ConsoleKeyInfo AltB                = Alt('b');
        public static ConsoleKeyInfo AltC                = Alt('c');
        public static ConsoleKeyInfo AltD                = Alt('d');
        public static ConsoleKeyInfo AltE                = Alt('e');
        public static ConsoleKeyInfo AltF                = Alt('f');
        public static ConsoleKeyInfo AltG                = Alt('g');
        public static ConsoleKeyInfo AltH                = Alt('h');
        public static ConsoleKeyInfo AltI                = Alt('i');
        public static ConsoleKeyInfo AltJ                = Alt('j');
        public static ConsoleKeyInfo AltK                = Alt('k');
        public static ConsoleKeyInfo AltL                = Alt('l');
        public static ConsoleKeyInfo AltM                = Alt('m');
        public static ConsoleKeyInfo AltN                = Alt('n');
        public static ConsoleKeyInfo AltO                = Alt('o');
        public static ConsoleKeyInfo AltP                = Alt('p');
        public static ConsoleKeyInfo AltQ                = Alt('q');
        public static ConsoleKeyInfo AltR                = Alt('r');
        public static ConsoleKeyInfo AltS                = Alt('s');
        public static ConsoleKeyInfo AltT                = Alt('t');
        public static ConsoleKeyInfo AltU                = Alt('u');
        public static ConsoleKeyInfo AltV                = Alt('v');
        public static ConsoleKeyInfo AltW                = Alt('w');
        public static ConsoleKeyInfo AltX                = Alt('x');
        public static ConsoleKeyInfo AltY                = Alt('y');
        public static ConsoleKeyInfo AltZ                = Alt('z');
        public static ConsoleKeyInfo AltShiftB           = Alt('B');
        public static ConsoleKeyInfo AltShiftF           = Alt('F');
        public static ConsoleKeyInfo AltSpace            = Alt(' ');  // !Windows, system menu.
        public static ConsoleKeyInfo AltPeriod           = Alt('.');  // !Linux, CLR bug
        public static ConsoleKeyInfo AltEquals           = Alt('=');  // !Linux, CLR bug
        public static ConsoleKeyInfo AltMinus            = Alt('-');
        public static ConsoleKeyInfo AltUnderbar         = Alt('_');  // !Linux, CLR bug
        public static ConsoleKeyInfo AltBackspace        = Alt(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '\x08' : '\x7f');
        public static ConsoleKeyInfo AltLess             = Alt('<');  // !Linux, CLR bug
        public static ConsoleKeyInfo AltGreater          = Alt('>');  // !Linux, CLR bug
        public static ConsoleKeyInfo AltQuestion         = Alt('?');  // !Linux, CLR bug

        public static ConsoleKeyInfo CtrlAt              = Ctrl('\x00');
        public static ConsoleKeyInfo CtrlSpace           = Ctrl(' '); // !Linux, same as CtrlAt
        public static ConsoleKeyInfo CtrlA               = Ctrl('\x01');
        public static ConsoleKeyInfo CtrlB               = Ctrl('\x02');
        public static ConsoleKeyInfo CtrlC               = Ctrl('\x03');
        public static ConsoleKeyInfo CtrlD               = Ctrl('\x04');
        public static ConsoleKeyInfo CtrlE               = Ctrl('\x05');
        public static ConsoleKeyInfo CtrlF               = Ctrl('\x06');
        public static ConsoleKeyInfo CtrlG               = Ctrl('\a');
        public static ConsoleKeyInfo CtrlH               = Ctrl('\b');
        public static ConsoleKeyInfo CtrlI               = Ctrl('\t');
        public static ConsoleKeyInfo CtrlJ               = Ctrl('\n');
        public static ConsoleKeyInfo CtrlK               = Ctrl('\v');
        public static ConsoleKeyInfo CtrlL               = Ctrl('\f');
        public static ConsoleKeyInfo CtrlM               = Ctrl('\r');
        public static ConsoleKeyInfo CtrlN               = Ctrl('\x0e');
        public static ConsoleKeyInfo CtrlO               = Ctrl('\x0f');
        public static ConsoleKeyInfo CtrlP               = Ctrl('\x10');
        public static ConsoleKeyInfo CtrlQ               = Ctrl('\x11');
        public static ConsoleKeyInfo CtrlR               = Ctrl('\x12');
        public static ConsoleKeyInfo CtrlS               = Ctrl('\x13');
        public static ConsoleKeyInfo CtrlT               = Ctrl('\x14');
        public static ConsoleKeyInfo CtrlU               = Ctrl('\x15');
        public static ConsoleKeyInfo CtrlV               = Ctrl('\x16');
        public static ConsoleKeyInfo CtrlW               = Ctrl('\x17');
        public static ConsoleKeyInfo CtrlX               = Ctrl('\x18');
        public static ConsoleKeyInfo CtrlY               = Ctrl('\x19');
        public static ConsoleKeyInfo CtrlZ               = Ctrl('\x1a');
        public static ConsoleKeyInfo CtrlLBracket        = Ctrl('\x1b');
        public static ConsoleKeyInfo CtrlBackslash       = Ctrl('\x1c');
        public static ConsoleKeyInfo CtrlRBracket        = Ctrl('\x1d');
        public static ConsoleKeyInfo CtrlUnderbar        = Ctrl('\x1f');
        public static ConsoleKeyInfo CtrlBackspace       = Ctrl(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '\x7f' : '\x08');
        public static ConsoleKeyInfo CtrlDelete          = Ctrl(ConsoleKey.Delete); // !Linux
        public static ConsoleKeyInfo CtrlEnd             = Ctrl(ConsoleKey.End); // !Linux
        public static ConsoleKeyInfo CtrlHome            = Ctrl(ConsoleKey.Home); // !Linux
        public static ConsoleKeyInfo CtrlPageUp          = Ctrl(ConsoleKey.PageUp); // !Linux
        public static ConsoleKeyInfo CtrlPageDown        = Ctrl(ConsoleKey.PageDown); // !Linux
        public static ConsoleKeyInfo CtrlLeftArrow       = Ctrl(ConsoleKey.LeftArrow); // !Linux
        public static ConsoleKeyInfo CtrlRightArrow      = Ctrl(ConsoleKey.RightArrow); // !Linux
        public static ConsoleKeyInfo CtrlEnter           = Ctrl(ConsoleKey.Enter); // !Linux


        public static ConsoleKeyInfo ShiftF3             = Shift(ConsoleKey.F3);
        public static ConsoleKeyInfo ShiftF8             = Shift(ConsoleKey.F8);
        public static ConsoleKeyInfo ShiftEnd            = Shift(ConsoleKey.End);
        public static ConsoleKeyInfo ShiftEnter          = Shift(ConsoleKey.Enter);
        public static ConsoleKeyInfo ShiftHome           = Shift(ConsoleKey.Home);
        public static ConsoleKeyInfo ShiftPageUp         = Shift(ConsoleKey.PageUp);
        public static ConsoleKeyInfo ShiftPageDown       = Shift(ConsoleKey.PageDown);
        public static ConsoleKeyInfo ShiftLeftArrow      = Shift(ConsoleKey.LeftArrow);
        public static ConsoleKeyInfo ShiftRightArrow     = Shift(ConsoleKey.RightArrow);
        public static ConsoleKeyInfo ShiftTab            = Shift(ConsoleKey.Tab); // !Linux, same as Tab

        public static ConsoleKeyInfo CtrlShiftC          = CtrlShift('\x03'); // !Linux
        public static ConsoleKeyInfo CtrlShiftEnter      = CtrlShift(ConsoleKey.Enter);
        public static ConsoleKeyInfo CtrlShiftLeftArrow  = CtrlShift(ConsoleKey.LeftArrow);
        public static ConsoleKeyInfo CtrlShiftRightArrow = CtrlShift(ConsoleKey.RightArrow);

        public static ConsoleKeyInfo CtrlAltY            = CtrlAlt('y');
        public static ConsoleKeyInfo CtrlAltRBracket     = CtrlAlt(']');
        public static ConsoleKeyInfo CtrlAltQuestion     = CtrlAlt('?');


        [DllImport("user32.dll")]
        public static extern int VkKeyScan(short wAsciiVal);

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

        private static readonly Dictionary<KeyWithModifiers, char> CtrlKeyToKeyCharMap
            = new Dictionary<KeyWithModifiers, char>();

        static Keys()
        {
            int i;
            for (i = 0; i < 26; i++)
            {
                CtrlKeyToKeyCharMap.Add(new KeyWithModifiers(ConsoleKey.A + i), (char)(i + 1));
            }

            for (i = 0; i < 10; i++)
            {
                char c = i == 2 ? '\0' : i == 6 ? '\x1e' : (char) ('0' + i);
                CtrlKeyToKeyCharMap.Add(new KeyWithModifiers(ConsoleKey.D0 + i), c);
                CtrlKeyToKeyCharMap.Add(new KeyWithModifiers(ConsoleKey.NumPad0 + i), c);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // These mapping are needed to support different keyboard
                // layouts, e.g. ']' is Oem6 with an English keyboard, but
                // Oem5 with a Portuguese keyboard.
                foreach (char c in "`~!@#$%^&*()-_=+[{]}\\|;:'\",<.>/?")
                {
                    var scan = VkKeyScan((short) c);
                    ConsoleModifiers mods = 0;
                    if ((scan & 0x100) != 0) { mods |= ConsoleModifiers.Shift; }
                    if ((scan & 0x200) != 0) { mods |= ConsoleModifiers.Control; }
                    if ((scan & 0x400) != 0) { mods |= ConsoleModifiers.Alt; }
                    var key = new KeyWithModifiers((ConsoleKey)(scan & 0xff), mods);
                    CtrlKeyToKeyCharMap.Add(key, c);
                }
            }
        }

        internal static bool IgnoreKeyChar(this ConsoleKeyInfo key)
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
                case ConsoleKey.LeftArrow:
                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                case ConsoleKey.RightArrow:
                case ConsoleKey.Tab:
                case ConsoleKey.UpArrow:
                    return true;
            }

            return false;
        }

        private static ConsoleModifiers NormalizeModifiers(this ConsoleKeyInfo key)
        {
            var result = key.Modifiers;
            if (!char.IsControl(key.KeyChar))
            {
                // Ignore Shift state unless it's a control character.
                result = result & ~ConsoleModifiers.Shift;
            }

            return result;
        }

        internal static char NormalizeKeyChar(this ConsoleKeyInfo key)
        {
            if (key.KeyChar == '\0' && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                // On Windows, ConsoleKeyInfo.Key is something we wanted to ignore, but
                // a few bindings force us to do something with it.
                var mods = key.Modifiers & ConsoleModifiers.Shift;
                var keyWithMods = new KeyWithModifiers(key.Key, mods);
                if (CtrlKeyToKeyCharMap.TryGetValue(keyWithMods, out var keyChar))
                    return keyChar;

                if (key.Key >= ConsoleKey.A && key.Key <= ConsoleKey.Z)
                {
                    return (char)(key.Key - ConsoleKey.A + 1);
                }

            }
            return key.KeyChar;
        }

        internal static bool EqualsNormalized(this ConsoleKeyInfo x, ConsoleKeyInfo y)
        {
            // In the common case, we mask something out of the ConsoleKeyInfo
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

        internal static int GetNormalizedHashCode(this ConsoleKeyInfo obj)
        {
            // Because a comparison of two ConsoleKeyInfo objects is a comparison of the
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

        internal static bool ShouldInsert(this ConsoleKeyInfo key)
        {
            return key.KeyChar != '\0' &&
                   (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0;
        }

        internal static bool IsUnmodifiedChar(this ConsoleKeyInfo key, char c)
        {
            return key.KeyChar == c &&
                   (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0;
        }

        public static string ToGestureString(this ConsoleKeyInfo key)
        {
            var useKeyEnum = key.IgnoreKeyChar();

            var mods = key.Modifiers;

            var sb = new StringBuilder();

            if (useKeyEnum && (mods & ConsoleModifiers.Shift) != 0)
            {
                sb.Append("Shift");
            }

            if ((mods & ConsoleModifiers.Control) != 0)
            {
                if (sb.Length > 0) sb.Append("+");
                sb.Append("Ctrl");
            }

            if ((mods & ConsoleModifiers.Alt) != 0)
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
                    case '\x08':
                        s = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Backspace" : "Ctrl+Backspace";
                        break;

                    case char _ when c <= 26:
                        s = ((char) ('a' + c - 1)).ToString();
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
