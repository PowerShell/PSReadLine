/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{
    /// <summary>
    /// A helper class for converting strings to ConsoleKey chords.
    /// </summary>
    public static class ConsoleKeyChordConverter
    {
        private static Exception CantConvert(string s, params object[] args)
        {
            return new ArgumentException(string.Format(CultureInfo.CurrentCulture, s, args));
        }

        /// <summary>
        /// Converts a string to a sequence of ConsoleKeyInfo.
        /// Sequences are separated by ','.
        /// Modifiers appear before the modified key and are separated by '+'.
        /// Examples:
        ///     Ctrl+X
        ///     Ctrl+D,M
        ///     Escape,X
        /// </summary>
        public static ConsoleKeyInfo[] Convert(string chord)
        {
            if (string.IsNullOrEmpty(chord))
            {
                throw new ArgumentNullException(nameof(chord));
            }

            int start = 0;
            var first = GetOneKey(chord, ref start);

            if (start >= chord.Length)
                return new [] { first };

            if (chord[start++] != ',')
                throw CantConvert(PSReadLineResources.InvalidSequence, chord);

            var second = GetOneKey(chord, ref start);
            if (start < chord.Length)
                throw CantConvert(PSReadLineResources.InvalidSequence, chord);

            return new [] { first, second };
        }

        private static ConsoleKeyInfo GetOneKey(string chord, ref int start)
        {
            ConsoleModifiers mods = 0;
            while (start < chord.Length)
            {
                var thisMod = GetModifier(chord, ref start);
                if (thisMod != 0)
                {
                    // modifier already set?
                    if ((mods & thisMod) != 0)
                    {
                        // Found a duplicate modifier
                        throw CantConvert(PSReadLineResources.InvalidModifier, thisMod, chord);
                    }
                    mods |= thisMod;

                    if (start >= chord.Length)
                    {
                        // Ends with a modifier?
                        throw CantConvert(PSReadLineResources.UnrecognizedKey, chord);
                    }

                    if (chord[start] != '+' && chord[start] != '-')
                    {
                        // No character between mod and key
                        throw CantConvert(PSReadLineResources.InvalidSequence, chord);
                    }

                    start++;
                    continue;
                }

                // Not a modifier
                var comma = chord.IndexOf(",", start, StringComparison.Ordinal);
                string input;
                if (comma == start)
                {
                    input = ",";
                }
                else if (comma == -1)
                {
                    input = chord.Substring(start);
                }
                else
                {
                    input = chord.Substring(start, comma - start);
                }
                start += input.Length;

                if (!MapKeyChar(input, ref mods, out var key, out var keyChar))
                {
                    throw CantConvert(PSReadLineResources.UnrecognizedKey, input);
                }

                return new ConsoleKeyInfo(keyChar, key,
                    (mods & ConsoleModifiers.Shift) != 0,
                    (mods & ConsoleModifiers.Alt) != 0,
                    (mods & ConsoleModifiers.Control) != 0);
            }

            throw CantConvert(PSReadLineResources.InvalidSequence, chord);
        }

        private static ConsoleModifiers GetModifier(string sequence, ref int start)
        {
            switch (sequence[start])
            {
                case 's':
                case 'S':
                    if ((sequence.Length - start >= 5) &&
                        (sequence[start + 1] == 'h' || sequence[start + 1] == 'H') &&
                        (sequence[start + 2] == 'i' || sequence[start + 2] == 'I') &&
                        (sequence[start + 3] == 'f' || sequence[start + 3] == 'F') &&
                        (sequence[start + 4] == 't' || sequence[start + 4] == 'T'))
                    {
                        start += 5;
                        return ConsoleModifiers.Shift;
                    }
                    goto default;

                case 'c':
                case 'C':
                    if (sequence.Length - start >= 7 &&
                        (sequence[start + 1] == 'o' || sequence[start + 1] == 'O') &&
                        (sequence[start + 2] == 'n' || sequence[start + 2] == 'N') &&
                        (sequence[start + 3] == 't' || sequence[start + 3] == 'T') &&
                        (sequence[start + 4] == 'r' || sequence[start + 4] == 'R') &&
                        (sequence[start + 5] == 'o' || sequence[start + 5] == 'O') &&
                        (sequence[start + 6] == 'l' || sequence[start + 6] == 'L'))
                    {
                        start += 7;
                        return ConsoleModifiers.Control;
                    }
                    else if (sequence.Length - start >= 4 &&
                             (sequence[start + 1] == 't' || sequence[start + 1] == 'T') &&
                             (sequence[start + 2] == 'r' || sequence[start + 2] == 'R') &&
                             (sequence[start + 3] == 'l' || sequence[start + 3] == 'L'))
                    {
                        start += 4;
                        return ConsoleModifiers.Control;
                    }
                    goto default;

                case 'a':
                case 'A':
                    if (sequence.Length - start >= 3 &&
                        (sequence[start + 1] == 'l' || sequence[start + 1] == 'L') &&
                        (sequence[start + 2] == 't' || sequence[start + 2] == 'T'))
                    {
                        start += 3;
                        return ConsoleModifiers.Alt;
                    }
                    goto default;

                default:
                    return 0;
            }
        }

        struct KeyPair
        {
            public char KeyChar;
            public ConsoleKey Key;
        }

        private static readonly Dictionary<string, KeyPair> KeyMappings =
            new Dictionary<string, KeyPair>(StringComparer.OrdinalIgnoreCase)
            {
                { "Backspace",  new KeyPair { Key = ConsoleKey.Backspace } }, // Not '\b' - ^H and BS differ on Linux
                { "Delete",     new KeyPair { Key = ConsoleKey.Delete } },
                { "DownArrow",  new KeyPair { Key = ConsoleKey.DownArrow} },
                { "End",        new KeyPair { Key = ConsoleKey.End } },
                { "Enter",      new KeyPair { Key = ConsoleKey.Enter } }, // Platforms differ - '\r' or '\n'
                { "Escape",     new KeyPair { Key = ConsoleKey.Escape, KeyChar = '\x1b' } },
                { "F1",         new KeyPair { Key = ConsoleKey.F1 } },
                { "F2",         new KeyPair { Key = ConsoleKey.F2 } },
                { "F3",         new KeyPair { Key = ConsoleKey.F3 } },
                { "F4",         new KeyPair { Key = ConsoleKey.F4 } },
                { "F5",         new KeyPair { Key = ConsoleKey.F5 } },
                { "F6",         new KeyPair { Key = ConsoleKey.F6 } },
                { "F7",         new KeyPair { Key = ConsoleKey.F7 } },
                { "F8",         new KeyPair { Key = ConsoleKey.F8 } },
                { "F9",         new KeyPair { Key = ConsoleKey.F9 } },
                { "F10",        new KeyPair { Key = ConsoleKey.F10 } },
                { "F11",        new KeyPair { Key = ConsoleKey.F11 } },
                { "F12",        new KeyPair { Key = ConsoleKey.F12 } },
                { "F13",        new KeyPair { Key = ConsoleKey.F13 } },
                { "F14",        new KeyPair { Key = ConsoleKey.F14 } },
                { "F15",        new KeyPair { Key = ConsoleKey.F15 } },
                { "F16",        new KeyPair { Key = ConsoleKey.F16 } },
                { "F17",        new KeyPair { Key = ConsoleKey.F17 } },
                { "F18",        new KeyPair { Key = ConsoleKey.F18 } },
                { "F19",        new KeyPair { Key = ConsoleKey.F19 } },
                { "F20",        new KeyPair { Key = ConsoleKey.F20 } },
                { "F21",        new KeyPair { Key = ConsoleKey.F21 } },
                { "F22",        new KeyPair { Key = ConsoleKey.F22 } },
                { "F23",        new KeyPair { Key = ConsoleKey.F23 } },
                { "F24",        new KeyPair { Key = ConsoleKey.F24 } },
                { "Home",       new KeyPair { Key = ConsoleKey.Home } },
                { "Insert",     new KeyPair { Key = ConsoleKey.Insert } },
                { "LeftArrow",  new KeyPair { Key = ConsoleKey.LeftArrow} },
                { "PageDown",   new KeyPair { Key = ConsoleKey.PageDown} },
                { "PageUp",     new KeyPair { Key = ConsoleKey.PageUp} },
                { "RightArrow", new KeyPair { Key = ConsoleKey.RightArrow} },
                { "Spacebar",   new KeyPair { Key = ConsoleKey.Spacebar, KeyChar = ' ' } },
                { "Tab",        new KeyPair { Key = ConsoleKey.Tab, KeyChar = '\t' }  },
                { "UpArrow",    new KeyPair { Key = ConsoleKey.UpArrow} },
            };

        static bool MapKeyChar(string input, ref ConsoleModifiers mods, out ConsoleKey key, out char keyChar)
        {
            var isCtrl = (mods & ConsoleModifiers.Control) != 0;
            if (input.Length == 1)
            {
                keyChar = input[0];
                if (keyChar >= 'a' && keyChar <= 'z')
                {
                    key = ConsoleKey.A + (keyChar - 'a');
                }
                else if (keyChar >= 'A' && keyChar <= 'Z')
                {
                    key = ConsoleKey.A + (keyChar - 'A');
                    mods |= ConsoleModifiers.Shift;
                }
                else if (keyChar >= '0' && keyChar <= '9')
                {
                    key = ConsoleKey.D0 + (keyChar - '0');
                }
                else
                {
                    key = 0;
                }
                if (isCtrl)
                {
                    switch (keyChar)
                    {
                        case var _ when (keyChar >= 'a' && keyChar <= 'z'):
                            keyChar = (char)(keyChar - 'a' + 1);
                            break;
                        case var _ when (keyChar >= 'A' && keyChar <= ']'):
                            keyChar = (char)(keyChar - 'A' + 1);
                            break;
                        case '_':
                            keyChar = '\x1f';
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                                mods |= ConsoleModifiers.Shift;
                            }
                            break;
                        case '^':
                            keyChar = '\x1e';
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                                mods |= ConsoleModifiers.Shift;
                            }
                            break;
                    }
                }

                return true;
            }

            if (KeyMappings.TryGetValue(input, out var keyPair))
            {
                key = keyPair.Key;

                if (key == ConsoleKey.Backspace)
                {
                    keyChar = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == isCtrl
                        ? '\x7f' : '\x08';
                }
                else
                {
                    keyChar = keyPair.KeyChar;
                }
                return true;
            }

            key = 0;
            keyChar = '\0';
            return Enum.TryParse(input, out key) && !int.TryParse(input, out var asInt);
        }
    }
}
