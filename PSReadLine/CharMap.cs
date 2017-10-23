/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.PowerShell
{
    /// Character sequence translator for platforms that behavior is not
    /// natively supported (currently just Windows ANSI input).
    internal interface ICharMap
    {
        /// How long to wait after seeing an escape should we wait before
        /// giving up on looking for a sequence?
        long EscapeTimeout { get; set; }
        /// A key may become available even if nothing else was read because
        /// of the escape sequence timer.
        bool KeyAvailable { get; }
        /// If this is true, we don't want to block on `Console.ReadKey` or
        /// the escape won't get seen until the next key is pressed.
        bool InEscapeSequence { get; }
        /// Read a processed key (not from the console).
        ConsoleKeyInfo ReadKey();
        /// Returns true if no further processing is needed. In this case,
        /// `key` may have changed based on previous input. If false is
        /// returned, the CharMap wants to consume this key for later processing.
        void ProcessKey(ConsoleKeyInfo key);
    }

    /// No-op - relies on whatever processing the .NET Console class does,
    /// which on Unix reads from terminfo, and on Windows is none to very little.
    internal class DotNetCharMap : ICharMap
    {
        private ConsoleKeyInfo _key;

        /// Unused
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

    /// Hard-coded translator for the only VT mode Windows supports.
    internal class WindowsAnsiCharMap : ICharMap
    {
        private List<ConsoleKeyInfo> _pendingKeys;
        /// The next index in `_pendingKeys` to write to.
        private int _addKeyIndex;
        /// The next index in `_pendingKeys` to read from. This index becomes
        /// valid when:
        /// - The first character is escape and the escape timeout elapses.
        /// - A sequence is completed.
        /// - A single readable character is inserted.
        private int _readKeyIndexFrom;
        /// The upper bound of `_pendingKeys` to read from, exclusive.
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
                int index = _readKeyIndexFrom;
                if (++_readKeyIndexFrom == _readKeyIndexTo)
                {
                    for (int i = _readKeyIndexTo; i < _addKeyIndex; i++)
                    {
                        SetKey(i - _readKeyIndexTo, _pendingKeys[i]);
                    }
                    _addKeyIndex -= _readKeyIndexTo;
                    index = _readKeyIndexFrom = _readKeyIndexTo = 0;
                }
                return _pendingKeys[index];
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
                if (i == 0)
                {
                    _escTimeoutStopwatch.Restart();
                }
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
                consoleKey = (ConsoleKey)('B' - ch);
                break;
            }

            SetKey(i, new ConsoleKeyInfo(ch, consoleKey, shift: shift, alt: false, control: control));
            if (i == 0)
            {
                _readKeyIndexTo = 1;
            }
        }

        /// '\0' is used as an invalid character - it is valid, but only
        /// by itself, not as part of a sequence (^[^@ is Esc, Ctrl-@, not Alt-Ctrl-@).
        private char GetSeqChar(int i)
        {
            // Only return valid key indexes for this scan.
            if (i >= _pendingKeys.Count || i >= _addKeyIndex)
            {
                return '\0';
            }
            // None of the valid sequence characters have a KeyChar of '\0'.
            return _pendingKeys[i].KeyChar;
        }

        /// Called when _pendingKeys[0] == ESC but it's not a full sequence.
        /// As far as I can tell, the only keys that can't be combined with
        /// alt are Esc, ^@, and Backspace (which generates ^[^H), but that
        /// gets translated in `ProcessControlKey` so we have to let it go here.
        /// None of the keys this applies to have their KeyChar set to 0 -
        /// the ones that do have a special alt sequence handled later.
        private bool ProcessAltSequence()
        {
            var ch = GetSeqChar(1);
            if (ch == '\0' || ch == '\x1b' || ch >= '\x7f' /*Non-ASCII*/)
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

        /// Scan for input escape sequences.
        /// We're only interested in the range 0 to _addKeyIndex when
        /// _pendingKeys[0] == Escape.
        private void ProcessMultipleKeys()
        {
            if (GetSeqChar(_addKeyIndex - 1) == '\x1b')
            {
                // There's a possible case that it could have been a sequence
                // part, but it's also an alt sequence. Since the second escape
                // causes a reset, we should check if there's an alt sequence
                // that was never seen because we were waiting for a full escape
                // sequence. Either way, we want to read everything up to the
                // escape that was just processed.
                if (_escTimeoutStopwatch.ElapsedMilliseconds <= EscapeTimeout)
                {
                    ProcessAltSequence();
                    _readKeyIndexFrom = 0;
                    _readKeyIndexTo = _addKeyIndex - 1;
                }
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
                // If the timer expired and there are exactly three pending
                // characters, that means the first two which were entered
                // before the timer expiring could be an alt sequence
                // (see above note).
                if (_addKeyIndex == 3)
                {
                    ProcessAltSequence();
                }
                _readKeyIndexFrom = 0;
                _readKeyIndexTo = _addKeyIndex;
            }
        }

        /// Used with ^[Ox and ^[[1;nx sequences.
        private static readonly char[] _escOOrBracket1Chars = new char[]
        {
            'A', 'B', 'C', 'D', 'F', 'H', 'P', 'Q', 'R', 'S'
        };
        /// Used with ^[[x sequences.
        private static readonly char[] _escBracketChars = new char[]
        {
            'A', 'B', 'C', 'D', 'F', 'H'
        };
        /// ConsoleKeys matching ^[Ox, ^[[x, and ^[[1;nx.
        private static readonly ConsoleKey[] _escBracketConsoleKeys = new ConsoleKey[]
        {
            // A                B                     C                      D
            ConsoleKey.UpArrow, ConsoleKey.DownArrow, ConsoleKey.RightArrow, ConsoleKey.LeftArrow,
            // F            H
            ConsoleKey.End, ConsoleKey.Home,
            // P           Q              R              S
            ConsoleKey.F1, ConsoleKey.F2, ConsoleKey.F3, ConsoleKey.F4
        };
        /// Modifiers for ^[[1;nx - look up by n-2.
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

        /// Returns true if the input is a full or partially complete escape sequence.
        /// There are only a few input patterns we have to match here:
        /// - ^[Ox - x in [A, B, C, D, H, F, P, Q, R, S]
        /// - ^[[x - x in [A, B, C, D, H, F]
        /// - ^[[1;nx - x from above lists. N designates the following:
        ///   - 2: Shift
        ///   - 3: Alt
        ///   - 4: Alt+Shift
        ///   - 5: Control
        ///   - 6: Control+Shift
        ///   - 7: Control+Alt
        ///   - 8: Control+Alt+Shift
        /// - ^[[n~ - n is a 1 or 2 digit number. No modifiers are allowed on these.
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
                else
                {
                    // Completed ^[[x sequence (if the lookup succeeds).
                    var index = Array.BinarySearch(_escBracketChars, ch);
                    if (index < 0)
                    {
                        return false;
                    }
                    SetKey(0, new ConsoleKeyInfo('\0', _escBracketConsoleKeys[index], false, false, false));
                    _addKeyIndex = 1;
                    _readKeyIndexFrom = 0;
                    _readKeyIndexTo = 1;
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
                _addKeyIndex = 1;
                _readKeyIndexFrom = 0;
                _readKeyIndexTo = 1;
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
            _addKeyIndex = 1;
            _readKeyIndexFrom = 0;
            _readKeyIndexTo = 1;
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
            var ch = GetSeqChar(3);
            // If it's a 2 digit number, adjust n and ch, then we'll just
            // make sure ch is '~' after.
            if (ch >= '0' && ch <= '9')
            {
                if (_addKeyIndex == 4)
                {
                    // Incomplete, still need a final '~'.
                    return true;
                }
                n = n * 10 + ((int)ch - (int)'0');
                ch = GetSeqChar(4);
            }
            if (ch != '~')
            {
                // Whether we saw a one or two digit number, ch is the character
                // right after the number that needs to be '~'.
                return false;
            }

            // These seem kind of randomly assigned, so just do a switch.
            ConsoleKey key;
            switch (n)
            {
            case 2:
                key = ConsoleKey.Insert;
                break;
            case 3:
                key = ConsoleKey.Delete;
                break;
            case 5:
                key = ConsoleKey.PageUp;
                break;
            case 6:
                key = ConsoleKey.PageDown;
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
            default:
                return false;
            }

            SetKey(0, new ConsoleKeyInfo('\0', key, false, false, false));
            _addKeyIndex = 1;
            _readKeyIndexFrom = 0;
            _readKeyIndexTo = 1;
            return true;
        }
    }
}
