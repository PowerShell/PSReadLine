/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.PowerShell
{
    // Character sequence translator for platforms that behavior is not
    // natively supported (currently just Windows ANSI input).
    internal interface ICharMap
    {
        // How long to wait after seeing an escape should we wait before
        // giving up on looking for a sequence?
        long EscapeTimeout { get; set; }
        // A key may become available even if nothing else was read because
        // of the escape sequence timer.
        bool KeyAvailable { get; }
        // If this is true, we don't want to block on `Console.ReadKey` or
        // the escape won't get seen until the next key is pressed.
        bool InEscapeSequence { get; }
        // Read a key from the processing buffer. An unspecified value
        // is returned if `KeyAvailable` is false.
        ConsoleKeyInfo ReadKey();
        // Insert a new key into the processing buffer. It is important that
        // immediately after every call to this, you check `KeyAvailable` as
        // the implementations are not designed to hold keys that don't form
        // a recognizable sequence.
        void ProcessKey(ConsoleKeyInfo key);
    }

    // No-op - relies on whatever processing the .NET Console class does,
    // which on Unix reads from terminfo, and on Windows is none to very little.
    internal class DotNetCharMap : ICharMap
    {
        private ConsoleKeyInfo _key;

        // Unused
        public long EscapeTimeout {
            get { return 0; }
            set {}
        }

        public bool KeyAvailable { get; private set; } = false;

        public bool InEscapeSequence { get; } = false;

        public ConsoleKeyInfo ReadKey()
        {
            KeyAvailable = false;
            return _key;
        }

        public void ProcessKey(ConsoleKeyInfo key)
        {
            _key = key;
            KeyAvailable = true;
        }
    }

    // Hard-coded translator for the only VT mode Windows supports.
    internal class WindowsAnsiCharMap : ICharMap
    {
        private List<ConsoleKeyInfo> _pendingKeys;
        // The next index in `_pendingKeys` to write to.
        private int _addKeyIndex;
        // The next index in `_pendingKeys` to read from. This index becomes
        // valid when:
        // - The first character is escape and the escape timeout elapses.
        // - A sequence is completed.
        // - A single readable character is inserted.
        private int _readKeyIndexFrom;
        // The upper bound of `_pendingKeys` to read from, exclusive.
        private int _readKeyIndexTo;

        private Stopwatch _escTimeoutStopwatch = new Stopwatch();

        public WindowsAnsiCharMap(long escapeTimeout = 50)
        {
            // In theory this shouldn't need to be any longer, but one time
            // Windows spewed a whole bunch of stuff to the console and crashed
            // it somehow (Alt+numpad), so just to be safe use a List in case
            // the buffer needs to expand.
            this._pendingKeys = new List<ConsoleKeyInfo>(6);
            // Several places assume that _pendingKeys[0] is valid. Since
            // elements are never removed from the list, only overwritten,
            // doing this will avoid any problems with that assumption.
            this._pendingKeys.Add(default(ConsoleKeyInfo));
            this._addKeyIndex = 0;
            this._readKeyIndexFrom = 0;
            this._readKeyIndexTo = 0;
            this.EscapeTimeout = escapeTimeout;
        }

        public long EscapeTimeout { get; set; }

        public bool KeyAvailable
        {
            get
            {
                if (_readKeyIndexFrom < _readKeyIndexTo)
                {
                    return true;
                }

                if (
                    _addKeyIndex > 0 &&
                    _pendingKeys[0].KeyChar == '\x1b' &&
                    _escTimeoutStopwatch.ElapsedMilliseconds >= EscapeTimeout
                )
                {
                    _readKeyIndexFrom = 0;
                    // If two characters are waiting, it could be an Alt+<ch> sequence.
                    // If there are more than two, we would have processed the sequence
                    // before if it was valid.
                    if (_addKeyIndex == 2)
                    {
                        ProcessAltSequence();
                    }
                    _readKeyIndexTo = _addKeyIndex;
                    return true;
                }
                return false;
            }
        }

        public bool InEscapeSequence
        {
            get
            {
                return _pendingKeys[0].KeyChar == '\x1b' && _escTimeoutStopwatch.ElapsedMilliseconds < EscapeTimeout;
            }
        }

        public ConsoleKeyInfo ReadKey()
        {
            if (_readKeyIndexFrom < _readKeyIndexTo)
            {
                var key = _pendingKeys[_readKeyIndexFrom];
                if (++_readKeyIndexFrom == _readKeyIndexTo)
                {
                    for (int i = _readKeyIndexTo; i < _addKeyIndex; i++)
                    {
                        SetKey(i - _readKeyIndexTo, _pendingKeys[i]);
                    }
                    _addKeyIndex -= _readKeyIndexTo;
                    _readKeyIndexFrom = _readKeyIndexTo = 0;
                }
                return key;
            }
            else
            {
                return default(ConsoleKeyInfo);
            }
        }

        public void ProcessKey(ConsoleKeyInfo key)
        {
            ProcessSingleKey(key);
            if (_addKeyIndex > 1)
            {
                ProcessMultipleKeys();
            }
        }

        // Modify the state when a sequence of raw characters was condensed into
        // a single readable character (an escape sequence was finished).
        private void CondenseState()
        {
            _addKeyIndex = 1;
            _readKeyIndexFrom = 0;
            _readKeyIndexTo = 1;
        }

        public void SetKey(int index, ConsoleKeyInfo key)
        {
            if (index >= _pendingKeys.Count)
            {
                _pendingKeys.Add(key);
            }
            else
            {
                _pendingKeys[index] = key;
            }
        }

        private void ProcessSingleKey(ConsoleKeyInfo key)
        {
            var ch = key.KeyChar;
            if (ch == 0)
            {
                ch = (char)key.Key;
            }

            if (ch < 0x20 || ch == 0x7f)
            {
                ProcessControlKey(_addKeyIndex, ch);
            }
            else
            {
                SetKey(_addKeyIndex, key);
                if (_addKeyIndex == 0)
                {
                    _readKeyIndexTo = 1;
                }
            }

            ++_addKeyIndex;
        }

        private void ProcessControlKey(int i, char ch)
        {
            ConsoleKey consoleKey = default(ConsoleKey);
            bool control = true;
            bool shift = false;
            switch ((int)ch)
            {
            case 0:
                consoleKey = (ConsoleKey)0x40;
                break;
            case 0x8:
            case 0x7F:
                ch = '\x8';
                consoleKey = ConsoleKey.Backspace;
                control = false;
                break;
            case 0x9:
                consoleKey = ConsoleKey.Tab;
                control = false;
                break;
            case 0xA:
                consoleKey = (ConsoleKey)0xA;
                control = false;
                break;
            case 0xD:
                consoleKey = ConsoleKey.Enter;
                control = false;
                break;
            case 0x13:
                consoleKey = ConsoleKey.Pause;
                control = false;
                break;
            case 0x1B:
                SetKey(i, new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false));
                _escTimeoutStopwatch.Restart();
                // Don't let escape set KeyAvailable.
                return;
            case 0x1C:
                consoleKey = (ConsoleKey)'\\';
                break;
            case 0x1D:
                consoleKey = (ConsoleKey)']';
                break;
            case 0x1E:
                consoleKey = (ConsoleKey)'^';
                shift = true;
                break;
            case 0x1F:
                consoleKey = (ConsoleKey)'_';
                shift = true;
                break;
            default:
                consoleKey = (ConsoleKey)((int)ConsoleKey.A + ch - 1);
                break;
            }

            SetKey(i, new ConsoleKeyInfo(ch, consoleKey, shift: shift, alt: false, control: control));
            if (i == 0)
            {
                _readKeyIndexTo = 1;
            }
        }

        // '\0' is used as an invalid character - it is valid, but only
        // by itself, not as part of a sequence (^[^@ is Esc, Ctrl-@, not Alt-Ctrl-@).
        private char GetSeqChar(int i)
        {
            // Only return valid key indexes for this scan.
            // None of the valid sequence characters have a KeyChar of '\0'.
            if (i >= _pendingKeys.Count || i >= _addKeyIndex)
            {
                return '\0';
            }
            var ch = _pendingKeys[i].KeyChar;
            // These characters can't be preceded by Esc (from showkey -a).
            //         Esc            Tab/^I        ^Enter/^J      Non-ASCII
            if (ch == '\x1b' || ch == '\x09' || ch == '\x0a' || ch >= '\x7f')
            {
                return '\0';
            }
            return ch;
        }

        // Called when _pendingKeys[0] == ESC but it's not a full sequence.
        // As far as I can tell, the only keys that can't be combined with
        // alt are Esc, ^@, and Backspace (which generates ^[^H), but that
        // gets translated in `ProcessControlKey` so we have to let it go here.
        // None of the keys this applies to have their KeyChar set to 0 -
        // the ones that do have a special alt sequence handled later.
        private bool ProcessAltSequence()
        {
            var ch = GetSeqChar(1);
            if (ch == '\0')
            {
                return false;
            }

            var key = _pendingKeys[1];
            _pendingKeys[0] = new ConsoleKeyInfo(
                ch,
                key.Key,
                shift: (key.Modifiers & ConsoleModifiers.Shift) != 0,
                alt: true,
                control: (key.Modifiers & ConsoleModifiers.Control) != 0
            );
            --_addKeyIndex;
            _readKeyIndexTo = 1;
            _readKeyIndexFrom = 0;
            for (int i = 1; i < _addKeyIndex; i++)
            {
                SetKey(i, _pendingKeys[i + 1]);
            }
            return true;
        }

        // Scan for input escape sequences.
        // We're only interested in the range 0 to _addKeyIndex when
        // _pendingKeys[0] == Escape.
        private void ProcessMultipleKeys()
        {
            if (_pendingKeys[_addKeyIndex - 1].KeyChar == '\x1b')
            {
                // There's a possible case that it could have been a sequence
                // part, but it's also an alt sequence. Since the second escape
                // causes a reset, we should check if there's an alt sequence
                // that was never seen because we were waiting for a full escape
                // sequence. Either way, we want to read everything up to the
                // escape that was just processed.
                if (_pendingKeys[0].KeyChar == '\x1b')
                {
                    ProcessAltSequence();
                }
                _readKeyIndexFrom = 0;
                _readKeyIndexTo = _addKeyIndex - 1;
                _escTimeoutStopwatch.Restart();
                return;
            }

            if (_escTimeoutStopwatch.ElapsedMilliseconds <= EscapeTimeout)
            {
                // If it's not a valid escape or alt sequence, just return it as input.
                if (!ProcessSequencePart() && !ProcessAltSequence())
                {
                    _readKeyIndexFrom = 0;
                    _readKeyIndexTo = _addKeyIndex;
                }
            }
            else
            {
                // If the timer expired and there are three or more pending
                // characters, that means the first two which were entered
                // before the timer expiring could be an alt sequence.
                if (_addKeyIndex >= 3)
                {
                    ProcessAltSequence();
                }
                _readKeyIndexFrom = 0;
                _readKeyIndexTo = _addKeyIndex;
            }
        }

        // Used with ^[Ox and ^[[1;nx sequences.
        private static readonly char[] _escOOrBracket1Chars = new char[]
        {
            'A', 'B', 'C', 'D', 'F', 'H', 'P', 'Q', 'R', 'S'
        };
        // Used with ^[[x sequences.
        private static readonly char[] _escBracketChars = new char[]
        {
            'A', 'B', 'C', 'D', 'F', 'H'
        };
        // ConsoleKeys matching ^[Ox, ^[[x, and ^[[1;nx.
        private static readonly ConsoleKey[] _escBracketConsoleKeys = new ConsoleKey[]
        {
            // A                B                     C                      D
            ConsoleKey.UpArrow, ConsoleKey.DownArrow, ConsoleKey.RightArrow, ConsoleKey.LeftArrow,
            // F            H
            ConsoleKey.End, ConsoleKey.Home,
            // P           Q              R              S
            ConsoleKey.F1, ConsoleKey.F2, ConsoleKey.F3, ConsoleKey.F4
        };
        // Modifiers for ^[[1;nx - look up by n-2.
        private static readonly ConsoleModifiers[] _escBracketModifiers = new ConsoleModifiers[]
        {
            ConsoleModifiers.Shift,
            ConsoleModifiers.Alt,
            ConsoleModifiers.Alt | ConsoleModifiers.Shift,
            ConsoleModifiers.Control,
            ConsoleModifiers.Control | ConsoleModifiers.Shift,
            ConsoleModifiers.Control | ConsoleModifiers.Alt,
            ConsoleModifiers.Control | ConsoleModifiers.Alt | ConsoleModifiers.Shift
        };

        // The ^[[n~ form is kind of randomly distributed, so just switch on that.

        // Returns true if the input is a full or partially complete escape sequence.
        // There are only a few input patterns we have to match here:
        // - ^[Ox - x in [A, B, C, D, H, F, P, Q, R, S]
        // - ^[[x - x in [A, B, C, D, H, F]
        // - ^[[1;nx - x from above lists. N designates the following:
        //   - 2: Shift
        //   - 3: Alt
        //   - 4: Alt+Shift
        //   - 5: Control
        //   - 6: Control+Shift
        //   - 7: Control+Alt
        //   - 8: Control+Alt+Shift
        // - ^[[n~ - n is a 1 or 2 digit number.
        // - ^[[n;m~ - n same as above, m is from the above modifier list.
        private bool ProcessSequencePart()
        {
            var ch = GetSeqChar(1);
            if (ch == '[')
            {
                if (_addKeyIndex == 2)
                {
                    // Still waiting for the rest.
                    return true;
                }

                ch = GetSeqChar(2);
                if (ch == '1')
                {
                    // ^[[1 - note, it could also be a ^[[1n~ so this function
                    // will forward it on if it doesn't find what it expects.
                    return ProcessBracket1Sequence();
                }
                else if (ch >= '2' && ch <= '9')
                {
                    // ^[[n - expecting possibly 1 more number and a '~'.
                    return ProcessBracketNTildeSequence();
                }
                else if (ch == 'Z')
                {
                    // ^[[Z - Shift-Tab
                    SetKey(0,
                        new ConsoleKeyInfo('\0', ConsoleKey.Tab,
                          shift: true, alt: false, control: false
                        )
                    );
                    CondenseState();
                    return true;
                }
                else
                {
                    // Completed ^[[x sequence (if the lookup succeeds).
                    var index = Array.BinarySearch(_escBracketChars, ch);
                    if (index < 0)
                    {
                        return false;
                    }
                    SetKey(0, new ConsoleKeyInfo('\0', _escBracketConsoleKeys[index], false, false, false));
                    CondenseState();
                    return true;
                }
            }
            else if (ch == 'O')
            {
                if (_addKeyIndex == 2)
                {
                    return true;
                }

                ch = GetSeqChar(2);
                var index = Array.BinarySearch(_escOOrBracket1Chars, ch);
                if (index < 0)
                {
                    return false;
                }
                SetKey(0, new ConsoleKeyInfo('\0', _escBracketConsoleKeys[index], false, false, false));
                CondenseState();
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool ProcessBracket1Sequence()
        {
            // At this point we've already seen ^[[1.
            if (_addKeyIndex == 3)
            {
                // Have ^[[1
                return true;
            }
            if (GetSeqChar(3) != ';')
            {
                // Expected ';', found something else.
                // If it's a number, it may be a sequence of the form ^[[1n~
                return ProcessBracketNTildeSequence();
            }
            if (_addKeyIndex == 4)
            {
                // Have ^[[1;
                return true;
            }
            var ch = GetSeqChar(4);
            int modifierIndex = (int)ch - (int)'2';
            if (ch < '2' || ch > '8')
            {
                // Modifiers only defined for 2-8.
                return false;
            }
            if (_addKeyIndex == 5)
            {
                // Have ^[[1;n - waiting for the last char.
                return true;
            }
            ch = GetSeqChar(5);
            int charIndex = Array.BinarySearch(_escOOrBracket1Chars, ch);
            if (charIndex < 0)
            {
                return false;
            }

            // We did it! A full escape sequence!
            var modifiers = _escBracketModifiers[modifierIndex];
            var key = new ConsoleKeyInfo(
                '\0',
                _escBracketConsoleKeys[charIndex],
                shift: (modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift,
                alt: (modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt,
                control: (modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control
            );
            SetKey(0, key);
            CondenseState();
            return true;
        }

        private bool ProcessBracketNTildeSequence()
        {
            // At this point we've seen ^[[n where n is in [1, 9].
            // We'll accept either a '~' or one more number and then a '~'.
            if (_addKeyIndex == 3)
            {
                // ^[[n - incomplete
                return true;
            }
            int n = (int)GetSeqChar(2) - (int)'0';
            // Some variable length parts are allowed - 1 or 2 digits, possible
            // ";n" modifier sequence, so we need to track the current character index.
            int chIndex = 3;
            var ch = GetSeqChar(chIndex);
            // If it's a 2 digit number, adjust n and ch, then we'll just
            // make sure ch is '~' after.
            if (ch >= '0' && ch <= '9')
            {
                ++chIndex;
                if (_addKeyIndex == chIndex)
                {
                    // Incomplete, still need possible modifiers and a final '~'.
                    return true;
                }
                // Complete the two digit number.
                n = n * 10 + ((int)ch - (int)'0');
                ch = GetSeqChar(chIndex);
            }
            // Some terminals allow modifiers for these characters, so parse them.
            // They are the same as the other sequences, listed in `_escBracketModifiers`.
            ConsoleModifiers modifiers = (ConsoleModifiers)0;
            if (ch == ';')
            {
                ++chIndex;
                if (_addKeyIndex == chIndex)
                {
                    return true;
                }
                ch = GetSeqChar(chIndex);
                if (ch >= '2' && ch <= '8')
                {
                    modifiers = _escBracketModifiers[(int)ch - (int)'2'];
                }
                else
                {
                    // Invalid character
                    return false;
                }

                ++chIndex;
                if (_addKeyIndex == chIndex)
                {
                    return true;
                }
                ch = GetSeqChar(chIndex);
            }

            if (ch != '~')
            {
                // All of these sequences end with '~', whether there were
                // modifiers or not.
                return false;
            }

            // These seem kind of randomly assigned, so just do a switch.
            // Some of these sequences are used by certain terminals (winpty and tmux)
            // in places where other sequences are used by the native Windows console.
            // Since winpty is fairly common and tmux support is personally important,
            // I want to support both of those.
            ConsoleKey key;
            switch (n)
            {
            // This is normally ^[[H, but tmux emits ^[[1~.
            case 1:
                key = ConsoleKey.Home;
                break;
            case 2:
                key = ConsoleKey.Insert;
                break;
            case 3:
                key = ConsoleKey.Delete;
                break;
            // This is normally ^[[F, but tmux emits ^[[4~.
            case 4:
                key = ConsoleKey.End;
                break;
            case 5:
                key = ConsoleKey.PageUp;
                break;
            case 6:
                key = ConsoleKey.PageDown;
                break;
            // 11-14 are emitted by winpty, but Windows uses ^[[OP, etc.
            case 11:
                key = ConsoleKey.F1;
                break;
            case 12:
                key = ConsoleKey.F2;
                break;
            case 13:
                key = ConsoleKey.F3;
                break;
            case 14:
                key = ConsoleKey.F4;
                break;
            case 15:
                key = ConsoleKey.F5;
                break;
            case 17:
                key = ConsoleKey.F6;
                break;
            case 18:
                key = ConsoleKey.F7;
                break;
            case 19:
                key = ConsoleKey.F8;
                break;
            case 20:
                key = ConsoleKey.F9;
                break;
            case 21:
                key = ConsoleKey.F10;
                break;
            case 23:
                key = ConsoleKey.F11;
                break;
            case 24:
                key = ConsoleKey.F12;
                break;
            // tmux emits these for Shift+F1-Shift+F8. I don't have F13 and higher
            // on my keyboard but presumably that's what these codes are for.
            // ConsoleKey defines up to F24, I can't get a code higher than 34
            // and don't want to guess because some codes are randomly skipped.
            case 25:
                key = ConsoleKey.F13;
                break;
            case 26:
                key = ConsoleKey.F14;
                break;
            case 28:
                key = ConsoleKey.F15;
                break;
            case 29:
                key = ConsoleKey.F16;
                break;
            case 31:
                key = ConsoleKey.F17;
                break;
            case 32:
                key = ConsoleKey.F18;
                break;
            case 33:
                key = ConsoleKey.F19;
                break;
            case 34:
                key = ConsoleKey.F20;
                break;
            default:
                return false;
            }

            var keyInfo = new ConsoleKeyInfo(
                '\0',
                key,
                shift: (modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift,
                alt: (modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt,
                control: (modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control
            );
            SetKey(0, keyInfo);
            CondenseState();
            return true;
        }
    }
}
