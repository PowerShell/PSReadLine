/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Move the cursor forward to the start of the next word.
        /// Word boundaries are defined by a configurable set of characters.
        /// </summary>
        public static void ViNextWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            if (numericArg < 0)
            {
                ViBackwardWord(key, -numericArg);
                return;
            }

            while (numericArg-- > 0)
            {
                int i = _singleton.ViFindNextWordPoint(_singleton.Options.WordDelimiters);
                if (i >= _singleton._buffer.Length)
                {
                    i += ViEndOfLineFactor;
                }
                _singleton.MoveCursor(Math.Max(i, 0));
            }
        }

        /// <summary>
        /// Move the cursor back to the start of the current word, or if between words,
        /// the start of the previous word.  Word boundaries are defined by a configurable
        /// set of characters.
        /// </summary>
        public static void ViBackwardWord(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            if (numericArg < 0)
            {
                ViNextWord(key, -numericArg);
                return;
            }

            while (numericArg-- > 0)
            {
                _singleton.MoveCursor(_singleton.ViFindPreviousWordPoint(_singleton.Options.WordDelimiters));
            }
        }

        /// <summary>
        /// Moves the cursor back to the beginning of the previous word, using only white space as delimiters.
        /// </summary>
        public static void ViBackwardGlob(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            int i = _singleton._current;
            while (numericArg-- > 0)
            {
                i = _singleton.ViFindPreviousGlob(i - 1);
            }
            _singleton.MoveCursor(i);
        }

        /// <summary>
        /// Moves to the next word, using only white space as a word delimiter.
        /// </summary>
        public static void ViNextGlob(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            int i = _singleton._current;
            while (numericArg-- > 0)
            {
                i = _singleton.ViFindNextGlob(i);
            }

            int newPosition = Math.Min(i, Math.Max(0, _singleton._buffer.Length - 1));
            if (newPosition != _singleton._current)
            {
                _singleton.MoveCursor(newPosition);
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Moves the cursor to the end of the word, using only white space as delimiters.
        /// </summary>
        public static void ViEndOfGlob(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            if (numericArg < 0)
            {
                ViEndOfPreviousGlob(key, -numericArg);
                return;
            }

            while (numericArg-- > 0)
            {
                _singleton.MoveCursor(_singleton.ViFindEndOfGlob());
            }
        }

        /// <summary>
        /// Moves to the end of the previous word, using only white space as a word delimiter.
        /// </summary>
        public static void ViEndOfPreviousGlob(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (!TryGetArgAsInt(arg, out var numericArg, 1))
            {
                return;
            }

            if (numericArg < 0)
            {
                ViEndOfGlob(key, -numericArg);
                return;
            }

            while (numericArg-- > 0)
            {
                _singleton.MoveCursor(_singleton.ViFindEndOfPreviousGlob());
            }
        }

        /// <summary>
        /// Returns 0 if the cursor is allowed to go past the last character in the line, -1 otherwise.
        /// </summary>
        /// <seealso cref="ForwardChar"/>
        private static int ViEndOfLineFactor => InViCommandMode() ? -1 : 0;

        /// <summary>
        /// Move the cursor to the end of the current logical line.
        /// </summary>
        public static void MoveToEndOfLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            var eol = GetEndOfLogicalLinePos(_singleton._current);
            if (eol != _singleton._current)
            {
                _singleton.MoveCursor(eol);
            }
            _singleton._moveToEndOfLineCommandCount++;
            _singleton._moveToLineDesiredColumn = int.MaxValue;
        }

        /// <summary>
        /// Move the cursor forward to the end of the current word, or if between words,
        /// to the end of the next word.  Word boundaries are defined by a configurable
        /// set of characters.
        /// </summary>
        public static void NextWordEnd(ConsoleKeyInfo? key = null, object arg = null)
        {
            int qty = arg as int? ?? 1;
            for (; qty > 0 && _singleton._current < _singleton._buffer.Length - 1; qty--)
            {
                _singleton.MoveCursor(_singleton.ViFindNextWordEnd(_singleton.Options.WordDelimiters));
            }
        }

        /// <summary>
        /// Move to the column indicated by arg.
        /// </summary>
        public static void GotoColumn(ConsoleKeyInfo? key = null, object arg = null)
        {
            int col = arg as int? ?? -1;
            if (col < 0)
            {
                Ding();
                return;
            }

            if (col < _singleton._buffer.Length + ViEndOfLineFactor)
            {
                _singleton.MoveCursor(Math.Min(col, _singleton._buffer.Length) - 1);
            }
            else
            {
                _singleton.MoveCursor(_singleton._buffer.Length + ViEndOfLineFactor);
                Ding();
            }
        }

        /// <summary>
        /// Move the cursor to the first non-blank character in the line.
        /// </summary>
        public static void GotoFirstNonBlankOfLine(ConsoleKeyInfo? key = null, object arg = null)
        {
            var newCurrent = GetFirstNonBlankOfLogicalLinePos(_singleton._current);
            if (newCurrent != _singleton._current)
            {
                _singleton.MoveCursor(newCurrent);
            }
        }

        /// <summary>
        /// Similar to <see cref="GotoBrace"/>, but is character based instead of token based.
        /// </summary>
        public static void ViGotoBrace(ConsoleKeyInfo? key = null, object arg = null)
        {
            int i = _singleton.ViFindBrace(_singleton._current);
            if (i == _singleton._current)
            {
                Ding();
                return;
            }
            _singleton.MoveCursor(i);
        }

        private int ViFindBrace(int i)
        {
            if (_buffer.Length == 0)
            {
                return i;
            }

            switch (_buffer[i])
            {
                case '{':
                    return ViFindForward(i, '}', withoutPassing: '{');
                case '[':
                    return ViFindForward(i, ']', withoutPassing: '[');
                case '(':
                    return ViFindForward(i, ')', withoutPassing: '(');
                case '}':
                    return ViFindBackward(i, '{', withoutPassing: '}');
                case ']':
                    return ViFindBackward(i, '[', withoutPassing: ']');
                case ')':
                    return ViFindBackward(i, '(', withoutPassing: ')');
                default:
                    int l1 = ViFindForward(i, '{', withoutPassing: '}') is var x && x > i ? x : int.MaxValue;
                    int l2 = ViFindForward(i, '[', withoutPassing: ']') is var y && y > i ? y : int.MaxValue;
                    int l3 = ViFindForward(i, '(', withoutPassing: ')') is var z && z > i ? z : int.MaxValue;
                    int r1 = ViFindForward(i, '}', withoutPassing: '{') is var a && a > i ? a : int.MaxValue;
                    int r2 = ViFindForward(i, ']', withoutPassing: '[') is var b && b > i ? b : int.MaxValue;
                    int r3 = ViFindForward(i, ')', withoutPassing: '(') is var c && c > i ? c : int.MaxValue;
                    int closestLeft = Math.Min(Math.Min(l1, l2), l3);
                    int closestRight = Math.Min(Math.Min(r1, r2), r3);

                    if (closestRight <= closestLeft && closestRight != int.MaxValue)
                    {
                        return closestRight;
                    }

                    closestLeft = closestLeft == int.MaxValue ? i : closestLeft;
                    switch (_buffer[closestLeft])
                    {
                        case '{':
                            return ViFindForward(closestLeft, '}', withoutPassing: '{');
                        case '[':
                            return ViFindForward(closestLeft, ']', withoutPassing: '[');
                        case '(':
                            return ViFindForward(closestLeft, ')', withoutPassing: '(');
                        default:
                            return i;
                    }
            }
        }

        private int ViFindBackward(int start, char target, char withoutPassing)
        {
            if (start == 0)
            {
                return start;
            }
            int i = start - 1;
            int withoutPassingCount = 0;
            while (i != 0 && !(_buffer[i] == target && withoutPassingCount == 0))
            {
                if (_buffer[i] == withoutPassing)
                {
                    withoutPassingCount++;
                }
                if (_buffer[i] == target)
                {
                    withoutPassingCount--;
                }
                i--;
            }
            if (_buffer[i] == target && withoutPassingCount == 0)
            {
                return i;
            }
            return start;
        }

        private int ViFindForward(int start, char target, char withoutPassing)
        {
            if (IsAtEndOfLine(start))
            {
                return start;
            }
            int i = start + 1;
            int withoutPassingCount = 0;
            while (!IsAtEndOfLine(i) && !(_buffer[i] == target && withoutPassingCount == 0))
            {
                if (_buffer[i] == withoutPassing)
                {
                    withoutPassingCount++;
                }
                if (_buffer[i] == target)
                {
                    withoutPassingCount--;
                }
                i++;
            }
            if (_buffer[i] == target && withoutPassingCount == 0)
            {
                return i;
            }
            return start;
        }
    }
}
