/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.PowerShell
{
    internal interface ICharMap
    {
        long EscapeTimeout { get; set; }
        bool KeyAvailable { get; }
        /// If this is true, we don't want to block on `Console.ReadKey` or
        /// the escape won't get seen until the next key is pressed.
        bool InEscapeSequence { get; }
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
        public long EscapeTimeout { get; set; }

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

    internal class WindowsAnsiCharMap : ICharMap
    {
        private const int _minSeqLen = 2;
        private const int _maxSeqLen = 5;

        private List<ConsoleKeyInfo> _pendingKeys;
        /// The next index in `_pendingKeys` to write to.
        private int _addKeyIndex;
        /// The next index in `_pendingKeys` to read from. This index becomes
        /// valid when:
        /// - The first character is escape and the escape timeout elapses.
        /// - A sequence is completed.
        /// - A single readable character is inserted.
        /// - `_pendingKeys` is full and not decodable.
        private int _readKeyIndexFrom;
        private int _readKeyIndexTo;

        private Stopwatch _escTimeoutStopwatch = new Stopwatch();

        public WindowsAnsiCharMap(long escapeTimeout = 50)
        {
            this._pendingKeys = new List<ConsoleKeyInfo>(_maxSeqLen);
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

        /// Since '\0' is a valid sequence character, this returns
        /// '\xffff' for an invalid character, which will never appear in
        /// an ANSI (7- or 8-bit per char) escape sequence.
        private char GetSeqChar(int i)
        {
            // Only return valid key indexes for this scan.
            if (i >= _pendingKeys.Count || i >= _addKeyIndex)
            {
                return '\xffff';
            }
            var ch = _pendingKeys[i].KeyChar;
            // Escape is not valid inside a sequence.
            if (ch == '\x1b')
            {
                return '\xffff';
            }
            // '\0' is a valid control key, but not for others.
            var mods = _pendingKeys[i].Modifiers;
            if (ch == '\0' && (mods & ConsoleModifiers.Control) == 0)
            {
                return (char)_pendingKeys[i].Key;
            }
            return ch;
        }

        /// Called when _pendingKeys[0] == ESC
        private bool ProcessAltSequence()
        {
            var ch = GetSeqChar(1);
            // TODO: Check if it's a valid Alt sequence
            if (ch == '\xffff')
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

        /// Returns true if the input is a full or partially complete escape sequence.
        private bool ProcessSequencePart()
        {
            if (GetSeqChar(1) == '[')
            {
                if (_addKeyIndex == 2)
                {
                    return true;
                }

                var ch = GetSeqChar(2);
                ConsoleKey ck = default(ConsoleKey);
                switch (ch)
                {
                case 'A':
                    ck = ConsoleKey.UpArrow;
                    break;
                case 'B':
                    ck = ConsoleKey.DownArrow;
                    break;
                case 'C':
                    ck = ConsoleKey.RightArrow;
                    break;
                case 'D':
                    ck = ConsoleKey.LeftArrow;
                    break;
                default:
                    return false;
                }
                SetKey(0, new ConsoleKeyInfo('\0', ck, shift: false, alt: false, control: false));
                _addKeyIndex = 1;
                _readKeyIndexFrom = 0;
                _readKeyIndexTo = 1;
                return true;
            }
            return false;
        }
    }
}

/* vim: set ts=4 sw=4 sts=4 et: */
