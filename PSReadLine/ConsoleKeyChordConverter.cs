﻿/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    /// <summary>
    /// A helper class for converting strings to ConsoleKey chords.
    /// </summary>
    public static class ConsoleKeyChordConverter
    {
        /// <summary>
        /// Converts a string to a sequence of ConsoleKeyInfo.
        /// Sequences are separated by ','.  Modifiers
        /// appear before the modified key and are separated by '+'.
        /// Examples:
        ///     Ctrl+X
        ///     Ctrl+D,M
        ///     Escape,X
        /// </summary>
        public static ConsoleKeyInfo[] Convert(string chord)
        {
            if (string.IsNullOrEmpty(chord))
            {
                throw new ArgumentNullException("chord");
            }

            var tokens = chord.Split(new[] {','});

            if (tokens.Length > 2)
            {
                throw new ArgumentException(PSReadLineResources.ChordWithTooManyKeys);
            }

            var result = new ConsoleKeyInfo[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = ConvertOneSequence(tokens[i]);
            }

            return result;
        }

        private static ConsoleKeyInfo ConvertOneSequence(string sequence)
        {
            Stack<string> tokens = null;
            ConsoleModifiers modifiers = 0;
            ConsoleKey key = 0;
            char keyChar = '\u0000';

            bool valid = !String.IsNullOrEmpty(sequence);

            if (valid)
            {
                tokens = new Stack<string>(
                    (sequence.Split(new[] {'+'})
                        .Select(
                            part => part.ToLowerInvariant().Trim())));
            }

            while (valid && tokens.Count > 0)
            {
                string token = tokens.Pop();

                // sequence was something silly like "shift++"
                if (token == String.Empty)
                {
                    valid = false;
                    break;
                }

                // key should be first token to be popped
                if (key == 0)
                {
                    // the keyChar is this token
                    keyChar = token[0];

                    // Enum.TryParse accepts arbitrary integers.  We shouldn't,
                    // but single digits need to map to the correct key, e.g.
                    // ConsoleKey.D1
                    long tokenAsLong;
                    if (long.TryParse(token, out tokenAsLong))
                    {
                        if (tokenAsLong >= 0 && tokenAsLong <= 9)
                        {
                            token = "D" + token;
                        }
                        else
                        {
                            throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, PSReadLineResources.UnrecognizedKey, token));
                        }
                    }
                    // try simple parse for ConsoleKey enum name
                    valid = Enum.TryParse(token, ignoreCase: true, result: out key);

                    // doesn't map to ConsoleKey so convert to virtual key from char
                    if (!valid && token.Length == 1)
                    {
                        string failReason;
                        valid = TryParseCharLiteral(token[0], ref modifiers, ref key, out failReason);

                        if (!valid)
                        {
                            throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, PSReadLineResources.CantTranslateKey, token[0], failReason));
                        }
                    }

                    if (!valid)
                    {
                        throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, PSReadLineResources.UnrecognizedKey, token));
                    }
                }
                else
                {
                    // now, parse modifier(s)
                    ConsoleModifiers modifier;

                    // courtesy translation
                    if (token == "ctrl")
                    {
                        token = "control";
                    }

                    if (Enum.TryParse(token, ignoreCase: true, result: out modifier))
                    {
                        // modifier already set?
                        if ((modifiers & modifier) != 0)
                        {
                            // either found duplicate modifier token or shift state
                            // was already implied from char, e.g. char is "}", which is "shift+]"
                            throw new ArgumentException(
                                String.Format(CultureInfo.CurrentCulture, PSReadLineResources.InvalidModifier, modifier, key));
                        }
                        modifiers |= modifier;
                    }
                    else
                    {
                        throw new ArgumentException(
                            String.Format(CultureInfo.CurrentCulture, PSReadLineResources.InvalidModifier, token, key));
                    }
                }
            }

            if (!valid)
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, PSReadLineResources.InvalidSequence, sequence));
            }

            return new ConsoleKeyInfo(keyChar, key,
                shift: ((modifiers & ConsoleModifiers.Shift) != 0),
                alt: ((modifiers & ConsoleModifiers.Alt) != 0),
                control: ((modifiers & ConsoleModifiers.Control) != 0));
        }

        private static bool TryParseCharLiteral(char literal, ref ConsoleModifiers modifiers, ref ConsoleKey key, out string failReason)
        {
            bool valid = false;

#if UNIX
            bool isShift;
            bool isCtrl;
            key = GetKeyFromCharValue(literal, out isShift, out isCtrl);

            // Failure to get a key for the char just means that the key is not
            // special (in the ConsoleKey enum), so we return a default key.
            // Thus this never fails.
            valid = true;
            failReason = null;

            if (isShift)
            {
                modifiers |= ConsoleModifiers.Shift;
            }
            if (isCtrl)
            {
                modifiers |= ConsoleModifiers.Control;
            }
            // alt is not possible to get
#else
            // shift state will be in MSB
            short virtualKey = NativeMethods.VkKeyScan(literal);
            int hresult = Marshal.GetLastWin32Error();

            if (virtualKey != 0)
            {
                // e.g. "}" = 0x01dd but "]" is 0x00dd, ergo } = shift+].
                // shift = 1, control = 2, alt = 4, hankaku = 8 (ignored)
                int state = virtualKey >> 8;

                if ((state & 1) == 1)
                {
                    modifiers |= ConsoleModifiers.Shift;
                }
                if ((state & 2) == 2)
                {
                    modifiers |= ConsoleModifiers.Control;
                }
                if ((state & 4) == 4)
                {
                    modifiers |= ConsoleModifiers.Alt;
                }

                virtualKey &= 0xff;

                if (Enum.IsDefined(typeof (ConsoleKey), (int) virtualKey))
                {
                    failReason = null;
                    key = (ConsoleKey) virtualKey;
                    valid = true;
                }
                else
                {
                    // haven't seen this happen yet, but possible
                    failReason = String.Format(CultureInfo.CurrentCulture, PSReadLineResources.UnrecognizedKey, virtualKey);
                }                
            }
            else
            {
                Exception e = Marshal.GetExceptionForHR(hresult);
                failReason = e.Message;
            }
#endif

            return valid;
        }

#if UNIX
        // this is borrowed from the CoreFX internal System.IO.StdInReader class
        // https://github.com/dotnet/corefx/blob/5b2ae6aa485773cd5569f56f446698633c9ad945/src/System.Console/src/System/IO/StdInReader.cs#L222
        private static ConsoleKey GetKeyFromCharValue(char x, out bool isShift, out bool isCtrl)
        {
            isShift = false;
            isCtrl = false;

            switch (x)
            {
                case '\b':
                    return ConsoleKey.Backspace;

                case '\t':
                    return ConsoleKey.Tab;

                case '\n':
                    return ConsoleKey.Enter;

                case (char)(0x1B):
                    return ConsoleKey.Escape;

                case '*':
                    return ConsoleKey.Multiply;

                case '+':
                    return ConsoleKey.Add;

                case '-':
                    return ConsoleKey.Subtract;

                case '/':
                    return ConsoleKey.Divide;

                case (char)(0x7F):
                    return ConsoleKey.Delete;

                case ' ':
                    return ConsoleKey.Spacebar;

                default:
                    // 1. Ctrl A to Ctrl Z.
                    if (x >= 1 && x <= 26)
                    {
                        isCtrl = true;
                        return ConsoleKey.A + x - 1;
                    }

                    // 2. Numbers from 0 to 9.
                    if (x >= '0' && x <= '9')
                    {
                        return ConsoleKey.D0 + x - '0';
                    }

                    //3. A to Z
                    if (x >= 'A' && x <= 'Z')
                    {
                        isShift = true;
                        return ConsoleKey.A + (x - 'A');
                    }

                    // 4. a to z.
                    if (x >= 'a' && x <= 'z')
                    {
                        return ConsoleKey.A + (x - 'a');
                    }

                    break;
            }

            return default(ConsoleKey);
        }
#else
        internal static char GetCharFromConsoleKey(ConsoleKey key, ConsoleModifiers modifiers)
        {
            // default for unprintables and unhandled
            char keyChar = '\u0000';

            // emulate GetKeyboardState bitmap - set high order bit for relevant modifier virtual keys
            var state = new byte[256];
            state[NativeMethods.VK_SHIFT] = (byte)(((modifiers & ConsoleModifiers.Shift) != 0) ? 0x80 : 0);
            state[NativeMethods.VK_CONTROL] = (byte)(((modifiers & ConsoleModifiers.Control) != 0) ? 0x80 : 0);
            state[NativeMethods.VK_ALT] = (byte)(((modifiers & ConsoleModifiers.Alt) != 0) ? 0x80 : 0);

            // a ConsoleKey enum's value is a virtual key code
            uint virtualKey = (uint)key;

            // get corresponding scan code
            uint scanCode = NativeMethods.MapVirtualKey(virtualKey, NativeMethods.MAPVK_VK_TO_VSC);

            // get corresponding character  - maybe be 0, 1 or 2 in length (diacritics)
            var chars = new char[2];
            int charCount = NativeMethods.ToUnicode(
                virtualKey, scanCode, state, chars, chars.Length, NativeMethods.MENU_IS_INACTIVE);

            // TODO: support diacritics (charCount == 2)
            if (charCount == 1)
            {
                keyChar = chars[0];
            }
            return keyChar;
        }
#endif
    }
}
