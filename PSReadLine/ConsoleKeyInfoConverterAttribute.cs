using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;

namespace PSConsoleUtilities
{
    // $c = new-object PSConsoleUtilities.ConsoleKeyInfoConverterAttribute
    [AttributeUsage(AttributeTargets.Property)]
    public class ConsoleKeyInfoConverterAttribute : ArgumentTransformationAttribute
    {
        public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
        {
            if (!(inputData is string))
            {
                // pass through
                return inputData;
            }
            
            var sequence = (string)inputData;
            Stack<string> tokens = null;
            ConsoleModifiers modifiers = 0;
            ConsoleKey key = 0;

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
                    // try simple parse for ConsoleKey enum name
                    valid = Enum.TryParse(token, ignoreCase: true, result: out key);

                    // doesn't map to ConsoleKey so convert to virtual key from char
                    if (!valid && token.Length == 1)
                    {
                        string failReason;
                        valid = TryParseCharLiteral(token[0], ref modifiers, ref key, out failReason);

                        if (!valid)
                        {
                            throw new ArgumentException(String.Format("Unable to translate '{0}' to " +
                                "virtual key code: {1}.", token[0], failReason));
                        }
                    }

                    if (!valid)
                    {
                        throw new ArgumentException("Unrecognized key '" + token + "'. Please use a character literal or a " +
                            "well-known key name from the System.ConsoleKey enumeration.");
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
                                String.Format("Duplicate or invalid modifier token '{0}' for key '{1}'.", modifier, key));
                        }
                        modifiers |= modifier;
                    }
                    else
                    {
                        throw new ArgumentException("Invalid modifier token '" + token + "'. The supported modifiers are " +
                            "'alt', 'shift', 'control' or 'ctrl'.");
                    }
                }
            }

            if (!valid)
            {
                throw new ArgumentException("Invalid sequence '" + sequence + "'.");
            }

            char keyChar = GetCharFromConsoleKey(key, modifiers);

            return new ConsoleKeyInfo(keyChar, key,
                shift: ((modifiers & ConsoleModifiers.Shift) != 0),
                alt: ((modifiers & ConsoleModifiers.Alt) != 0),
                control: ((modifiers & ConsoleModifiers.Control) != 0));
        }

        private static bool TryParseCharLiteral(char literal, ref ConsoleModifiers modifiers, ref ConsoleKey key, out string failReason)
        {
            bool valid = false;

            // shift state will be in MSB
            short virtualKey = NativeMethods.VkKeyScan(literal);

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
                    failReason = String.Format("The virtual key code {0} does not map " +
                        "to a known System.ConsoleKey enumerated value.", virtualKey);
                }                
            }
            else
            {
                int hresult = Marshal.GetLastWin32Error();
                Exception e = Marshal.GetExceptionForHR(hresult);
                failReason = e.Message;
            }

            return valid;
        }

        private static char GetCharFromConsoleKey(ConsoleKey key, ConsoleModifiers modifiers)
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
            Debug.Assert(scanCode != 0, "scanCode != 0");

            // get corresponding character  - maybe be 0, 1 or 2 in length (diacriticals)
            var chars = new StringBuilder();
            int charCount = NativeMethods.ToAscii(
                virtualKey, scanCode, state, chars, NativeMethods.MENU_IS_INACTIVE);

            // TODO: support diacriticals (charCount == 2)
            if (charCount == 1)
            {
                keyChar = chars[0];
            }

            return keyChar;
        }
    }
}