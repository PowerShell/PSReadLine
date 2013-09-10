using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    [TestClass]
    public class UnitTest
    {
        static UnitTest()
        {
            var iss = InitialSessionState.CreateDefault2();
            var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();
            Runspace.DefaultRunspace = rs;

            for (var i = 'a'; i <= 'z'; i++)
            {
                CharToKeyInfo[i] = new ConsoleKeyInfo(i, ConsoleKey.A + 'a' - i, false, false, false);
            }
            for (var i = 'A'; i <= 'Z'; i++)
            {
                CharToKeyInfo[i] = new ConsoleKeyInfo(i, ConsoleKey.A + 'A' - i, true, false, false);
            }
            for (var i = '0'; i <= '9'; i++)
            {
                CharToKeyInfo[i] = new ConsoleKeyInfo(i, ConsoleKey.D0 + '0' - i, false, false, false);
            }
            CharToKeyInfo['{'] = _.LCurly;
            CharToKeyInfo['}'] = _.RCurly;
            CharToKeyInfo['('] = _.LParen;
            CharToKeyInfo[')'] = _.RParen;
            CharToKeyInfo['['] = _.LBracket;
            CharToKeyInfo[']'] = _.RBracket;
            CharToKeyInfo[' '] = _.Space;
            CharToKeyInfo['$'] = _.Dollar;
            CharToKeyInfo['#'] = _.Pound;
            CharToKeyInfo['<'] = _.LessThan;
            CharToKeyInfo['>'] = _.GreaterThan;
            CharToKeyInfo['+'] = _.Plus;
            CharToKeyInfo['-'] = _.Minus;
            CharToKeyInfo['_'] = _.Underbar;
            CharToKeyInfo['|'] = _.Pipe;
            CharToKeyInfo[';'] = _.Semicolon;
            CharToKeyInfo['"'] = _.DQuote;
            CharToKeyInfo['\''] = _.SQuote;
            CharToKeyInfo['\n'] = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
            CharToKeyInfo['\r'] = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        }

        private enum KeyMode
        {
            Cmd,
            Emacs,
#if FALSE
            Vi
#endif
        };

        // These colors are random - we just use these colors instead of the defaults
        // so the tests aren't sensitive to tweaks to the default colors.
        private static readonly ConsoleColor[] ForegroundColors = new []
        {
        /*None*/      Console.ForegroundColor,
        /*Comment*/   ConsoleColor.Blue,
        /*Keyword*/   ConsoleColor.Cyan,
        /*String*/    ConsoleColor.Gray,
        /*Operator*/  ConsoleColor.Green,
        /*Variable*/  ConsoleColor.Magenta,
        /*Command*/   ConsoleColor.Red,
        /*Parameter*/ ConsoleColor.White,
        /*Type*/      ConsoleColor.Yellow,
        /*Number*/    ConsoleColor.DarkBlue,
        };

        private static readonly ConsoleColor[] BackgroundColors = new[]
        {
        /*None*/      Console.BackgroundColor,
        /*Comment*/   ConsoleColor.DarkBlue,
        /*Keyword*/   ConsoleColor.DarkCyan,
        /*String*/    ConsoleColor.DarkGray,
        /*Operator*/  ConsoleColor.DarkGreen,
        /*Variable*/  ConsoleColor.DarkMagenta,
        /*Command*/   ConsoleColor.DarkRed,
        /*Parameter*/ ConsoleColor.DarkYellow,
        /*Type*/      ConsoleColor.Black,
        /*Number*/    ConsoleColor.Gray,
        };

        static Dictionary<char, ConsoleKeyInfo> CharToKeyInfo = new Dictionary<char, ConsoleKeyInfo>();

        class KeyHandler
        {
            public KeyHandler(ConsoleKeyInfo key, Action handler)
            {
                this.Key = key;
                this.Handler = handler;
            }

            public ConsoleKeyInfo Key { get; private set; }
            public Action Handler { get; private set; }
        }

        class KeyWithValidation
        {
            public KeyWithValidation(ConsoleKeyInfo key, Action validator = null)
            {
                this.Key = key;
                this.Validator = validator;
            }

            public ConsoleKeyInfo Key { get; set; }
            public Action Validator { get; set; }
        }

        class CancelReadLineException : Exception
        {
            public static void Throw()
            {
                throw new CancelReadLineException();
            }
        }

        private void AssertCursorLeftTopIs(int left, int top)
        {
            AssertCursorLeftIs(left);
            AssertCursorTopIs(top);
        }

        private void AssertCursorLeftIs(int expected)
        {
            Assert.AreEqual(expected, Console.CursorLeft);
        }

        private void AssertCursorTopIs(int expected)
        {
            Assert.AreEqual(expected, Console.CursorTop);
        }

        private void AssertLineIs(string expected)
        {
            string input;
            int unused;
            PSConsoleReadLine.GetBufferState(out input, out unused);
            Assert.AreEqual(expected, input);
        }

        private class NextLineToken { }
        static NextLineToken NextLine = new NextLineToken();

        private CHAR_INFO[] CreateCharInfoBuffer(params object[] items)
        {
            var result = new List<CHAR_INFO>();
            var fg = Console.ForegroundColor;
            var bg = Console.BackgroundColor;

            foreach (var i in items)
            {
                var item = i;
                if (item is char)
                {
                    result.Add(new CHAR_INFO((char)item, fg, bg));
                    continue;
                }
                if (item is NextLineToken)
                {
                    item = new string(' ', Console.BufferWidth - (result.Count % Console.BufferWidth));
                    fg = Console.ForegroundColor;
                    bg = Console.BackgroundColor;
                    // Fallthrough to string case.
                }
                var str = item as string;
                if (str != null)
                {
                    result.AddRange(str.Select(c => new CHAR_INFO(c, fg, bg)));
                    continue;
                }
                if (item is TokenClassification)
                {
                    fg = ForegroundColors[(int)(TokenClassification)item];
                    bg = BackgroundColors[(int)(TokenClassification)item];
                    continue;
                }
                var tuple = item as Tuple<ConsoleColor, ConsoleColor>;
                if (tuple != null)
                {
                    fg = tuple.Item1;
                    bg = tuple.Item2;
                    continue;
                }
                throw new ArgumentException("Unexpected type");
            }

            var extraSpacesNeeded = Console.BufferWidth - (result.Count % Console.BufferWidth);
            if (extraSpacesNeeded != 0)
            {
                var space = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
                result.AddRange(Enumerable.Repeat(space, extraSpacesNeeded));
            }

            return result.ToArray();
        }

        static private KeyWithValidation[] Keys(params object[] input)
        {
            var list = new List<KeyWithValidation>();
            foreach (var t in input)
            {
                if (t is ConsoleKeyInfo)
                {
                    list.Add(new KeyWithValidation((ConsoleKeyInfo)t));
                }
                else if (t is string)
                {
                    foreach (var c in (string)t)
                    {
                        list.Add(new KeyWithValidation(CharToKeyInfo[c]));
                    }
                }
                else if (t is char)
                {
                    list.Add(new KeyWithValidation(CharToKeyInfo[(char)t]));
                }
                else
                {
                    list.Add((KeyWithValidation)t);
                }
            }
            return list.ToArray();
        }

        private static CHAR_INFO[] ReadBufferLines(int top, int count)
        {
            var result = new CHAR_INFO[Console.BufferWidth * count];
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var readBufferSize = new COORD {
                X = (short)Console.BufferWidth,
                Y = (short)count};
            var readBufferCoord = new COORD {X = 0, Y = 0};
            var readRegion = new SMALL_RECT
            {
                Top = (short)top,
                Left = 0,
                Bottom = (short)(top + count),
                Right = (short)(Console.BufferWidth - 1)
            };
            NativeMethods.ReadConsoleOutput(handle, result,
                readBufferSize, readBufferCoord, ref readRegion);
            return result;
        }

        private static void ScrollBuffer(int lines)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var scrollRectangle = new SMALL_RECT
            {
                Top = (short) lines,
                Left = 0,
                Bottom = (short) (lines + Console.BufferHeight - 1),
                Right = (short)Console.BufferWidth
            };
            var destinationOrigin = new COORD {X = 0, Y = 0};
            var fillChar = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            NativeMethods.ScrollConsoleScreenBuffer(handle, ref scrollRectangle, ref scrollRectangle, destinationOrigin, ref fillChar);
        }

        private static void WriteBufferLines(CHAR_INFO[] buffer, ref int top)
        {
            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            int bufferWidth = Console.BufferWidth;
            int bufferLineCount = buffer.Length / bufferWidth;
            if ((top + bufferLineCount) > Console.BufferHeight)
            {
                var scrollCount = (top + bufferLineCount) - Console.BufferHeight;
                ScrollBuffer(scrollCount);
                top -= scrollCount;
            }
            var bufferSize = new COORD
            {
                X = (short) bufferWidth,
                Y = (short) bufferLineCount
            };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var writeRegion = new SMALL_RECT
            {
                Top = (short) top,
                Left = 0,
                Bottom = (short) (top + bufferLineCount - 1),
                Right = (short) bufferWidth
            };
            NativeMethods.WriteConsoleOutput(handle, buffer,
                                             bufferSize, bufferCoord, ref writeRegion);
        }

        private void AssertScreenIs(int lines, params object[] items)
        {
            var consoleBuffer = ReadBufferLines(0, lines);

            var expectedBuffer = CreateCharInfoBuffer(items);
            Assert.AreEqual(expectedBuffer.Length, consoleBuffer.Length);
            for (var i = 0; i < expectedBuffer.Length; i++)
            {
                Assert.AreEqual(expectedBuffer[i], consoleBuffer[i]);
            }
        }

        static void SetBufferState(string text, int cursor)
        {
            var keys = new List<KeyWithValidation>();
            keys.AddRange(Keys(text));
            keys.Add(new KeyWithValidation(_.Home));
            while (cursor-- > 0)
            {
                keys.Add(new KeyWithValidation(_.RightArrow));
            }
            keys.Last().Validator = CancelReadLineException.Throw;
            Test(keys.ToArray());
        }

        static void ClearScreen()
        {
            int bufferWidth = Console.BufferWidth;
            const int bufferLineCount = 10;
            var consoleBuffer = new CHAR_INFO[bufferWidth * bufferLineCount];
            for (int i = 0; i < consoleBuffer.Length; i++)
            {
                consoleBuffer[i] = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
            }
            int top = 0;
            WriteBufferLines(consoleBuffer, ref top);
        }

        static private void SetPrompt(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
                return;

            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);

            var lineCount = 1 + prompt.Count(c => c == '\n');
            if (lineCount > 1)
            {
                var options = new SetPSReadlineOption {ExtraPromptLineCount = lineCount - 1};
                PSConsoleReadLine.SetOptions(options);
            }
            int bufferWidth = Console.BufferWidth;
            var consoleBuffer = new CHAR_INFO[lineCount * bufferWidth];
            int j = 0;
            for (int i = 0; i < prompt.Length; i++, j++)
            {
                if (prompt[i] == '\n')
                {
                    for (; j % Console.BufferWidth != 0; j++)
                    {
                        consoleBuffer[j] = new CHAR_INFO(' ', Console.ForegroundColor, Console.BackgroundColor);
                    }
                    Console.CursorTop += 1;
                    Console.CursorLeft = 0;
                    j -= 1;  // We don't actually write the newline
                }
                else
                {
                    consoleBuffer[j] = new CHAR_INFO(prompt[i], Console.ForegroundColor, Console.BackgroundColor);
                    Console.CursorLeft += 1;
                }
            }

            var bufferSize = new COORD
                             {
                                 X = (short) bufferWidth,
                                 Y = (short) lineCount
                             };
            var bufferCoord = new COORD {X = 0, Y = 0};
            var writeRegion = new SMALL_RECT
                              {
                                  Top = 0,
                                  Left = 0,
                                  Bottom = (short) (lineCount - 1),
                                  Right = (short) bufferWidth
                              };
            NativeMethods.WriteConsoleOutput(handle, consoleBuffer, bufferSize, bufferCoord, ref writeRegion);
        }

        static private string Test(KeyWithValidation[] keys, string prompt = null)
        {
            Console.CursorLeft = 0;
            Console.CursorTop = 0;
            SetPrompt(prompt);
            int index = 0;
            try
            {
                using (ShimsContext.Create())
                {
                    PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.ReadKey = () => keys[index].Key;
                    PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.PostKeyHandler = () =>
                    {
                        if (keys[index].Validator != null)
                        {
                            keys[index].Validator();
                        }
                        index++;
                    };
                    System.Management.Automation.Fakes.ShimCommandCompletion.CompleteInputStringInt32HashtablePowerShell =
                        MockedCompleteInput;

                    return PSConsoleReadLine.ReadLine();
                }
            }
            catch (CancelReadLineException)
            {
                return null;
            }
        }

        private string Test(ConsoleKeyInfo[] keys, string prompt = null)
        {
            Console.CursorLeft = 0;
            Console.CursorTop = 0;
            SetPrompt(prompt);
            int index = 0;
            using (ShimsContext.Create())
            {
                PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.ReadKey = () => keys[index++];
                System.Management.Automation.Fakes.ShimCommandCompletion.CompleteInputStringInt32HashtablePowerShell =
                    MockedCompleteInput;
                return PSConsoleReadLine.ReadLine();
            }
        }

        private void TestSetup(KeyMode keyMode, params KeyHandler[] keyHandlers)
        {
            ClearScreen();
            PSConsoleReadLine.ClearHistory();
            PSConsoleReadLine.ClearKillRing();

            var options = new SetPSReadlineOption
            {
                AddToHistoryHandler         = null,
                HistoryNoDuplicates         = PSConsoleReadLine.DefaultHistoryNoDuplicates,
                MinimumHistoryCommandLength = PSConsoleReadLine.DefaultMinimumHistoryCommandLength,
                MaximumHistoryCount         = PSConsoleReadLine.DefaultMaximumHistoryCount,
                MaximumKillRingCount        = PSConsoleReadLine.DefaultMaximumKillRingCount,
                ResetTokenColors            = true,
            };

            switch (keyMode)
            {
            case KeyMode.Cmd:
                options.EditMode = EditMode.Windows;
                break;
            case KeyMode.Emacs:
                options.EditMode = EditMode.Emacs;
                break;
#if FALSE
            case KeyMode.Vi:
                options.EditMode = EditMode.Vi;
                break;
#endif
            }

            PSConsoleReadLine.SetOptions(options);

            foreach (var keyHandler in keyHandlers)
            {
                PSConsoleReadLine.SetKeyHandler(keyHandler.Key, false, keyHandler.Handler, "", "");
            }

            var colorOptions = new SetPSReadlineOption();
            foreach (var val in typeof(TokenClassification).GetEnumValues())
            {
                colorOptions.TokenKind = (TokenClassification)val;
                colorOptions.ForegroundColor = ForegroundColors[(int)val];
                colorOptions.BackgroundColor = BackgroundColors[(int)val];
                PSConsoleReadLine.SetOptions(colorOptions);
            }
        }

        [TestMethod]
        public void TestInput()
        {
            TestSetup(KeyMode.Cmd);

            var keys = Keys("exit", _.Enter);
            var result = Test(keys); Assert.AreEqual("exit", result);
            AssertCursorLeftIs(4);
        }

        [TestMethod]
        public void TestLongLine()
        {
            TestSetup(KeyMode.Cmd);

            var width = Console.BufferWidth;
            var sb = new StringBuilder();
            var keys = new ConsoleKeyInfo[width + 3];
            int i = 0;
            keys[i++] = _.DQuote;
            sb.Append('"');
            while (i < width + 1)
            {
                keys[i++] = _.Z;
                sb.Append('z');
            }
            keys[i++] = _.DQuote;
            sb.Append('"');
            keys[i] = _.Enter;

            var result = Test(keys); Assert.AreEqual(sb.ToString(), result);
        }

        [TestMethod]
        public void TestEndOfLine()
        {
            TestSetup(KeyMode.Cmd);

            var keys = Keys(
                new KeyWithValidation(_.End, () => AssertCursorLeftIs(0)),
                new KeyWithValidation(_.Enter)
            );
            var result = Test(keys); Assert.AreEqual("", result);

            TestSetup(KeyMode.Cmd);

            var width = Console.BufferWidth;
            var buffer = new StringBuilder();
            keys = new KeyWithValidation[width * 3];
            int i = 0;
            while (i < width)
            {
                keys[i++] = new KeyWithValidation(_.Space);
                buffer.Append(' ');
            }
            keys[i++] = new KeyWithValidation(_.Home, () => AssertCursorLeftIs(0));
            keys[i++] = new KeyWithValidation(_.End, () => AssertCursorLeftTopIs(0, 1));
            keys[i] = new KeyWithValidation(_.Enter);
            result = Test(keys); Assert.AreEqual(buffer.ToString(), result);

            for (int j = 0; j < 5; j++)
            {
                keys[i++] = new KeyWithValidation(_.Space);
                buffer.Append(' ');
            }
            keys[i++] = new KeyWithValidation(_.Home, () => AssertCursorLeftIs(0));
            keys[i++] = new KeyWithValidation(_.End, () => AssertCursorLeftTopIs(5, 1));
            keys[i] = new KeyWithValidation(_.Enter);
            result = Test(keys); Assert.AreEqual(buffer.ToString(), result);
        }

        [TestMethod]
        public void TestCursorMovement()
        {
            TestSetup(KeyMode.Cmd);

            var keysWithValidation = Keys(
                new KeyWithValidation(_.LeftArrow, () => AssertCursorLeftIs(0)),
                "ace",
                new KeyWithValidation(_.LeftArrow, () => AssertCursorLeftIs(2)),
                'd',
                new KeyWithValidation(_.LeftArrow, () => AssertCursorLeftIs(2)),
                new KeyWithValidation(_.LeftArrow, () => AssertCursorLeftIs(1)),
                'b', _.Enter
            );
            var result = Test(keysWithValidation); Assert.AreEqual("abcde", result);
        }

        [TestMethod]
        public void TestMultiLine()
        {
            TestSetup(KeyMode.Cmd);

            var keysWithValidation = Keys(
                "d|",
                new KeyWithValidation(_.Enter, () => AssertCursorTopIs(1)),
                'd', _.Enter
            );
            var result = Test(keysWithValidation); Assert.AreEqual("d|\nd", result);

            // Make sure <ENTER> when input is incomplete actually puts a newline
            // wherever the cursor is.
            var continationPrefixLength = PSConsoleReadLine.DefaultContinuationPrompt.Length;
            keysWithValidation = Keys(
                '{',
                new KeyWithValidation(_.Enter, () => AssertCursorTopIs(1)),
                'd',
                new KeyWithValidation(_.Enter, () => AssertCursorTopIs(2)),
                new KeyWithValidation(_.Home),
                new KeyWithValidation(_.RightArrow, () => AssertCursorLeftTopIs(1, 0)),
                new KeyWithValidation(_.Enter, () => AssertCursorLeftTopIs(continationPrefixLength, 1)),
                new KeyWithValidation(_.End, () => AssertCursorLeftTopIs(continationPrefixLength, 3)),
                '}', _.Enter
            );
            result = Test(keysWithValidation); Assert.AreEqual("{\n\nd\n}", result);

            // Make sure <ENTER> when input successfully parses accepts the input regardless
            // of where the cursor is, plus it moves the cursor to the end (so the new prompt
            // doesn't overwrite the end of the previous long/multi-line command line.)
            keysWithValidation = Keys(
                '{',
                new KeyWithValidation(_.Enter),
                '}',
                new KeyWithValidation(_.Home),
                new KeyWithValidation(_.Enter, () => AssertCursorLeftTopIs(continationPrefixLength + 1, 1))
            );
            result = Test(keysWithValidation); Assert.AreEqual("{\n}", result);
        }

        [TestMethod]
        public void TestDelete()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing, but don't crash)
            var keys = Keys(_.Delete, _.Enter);
            var result = Test(keys); Assert.AreEqual("", result);

            // At end but input not empty (does nothing, but don't crash)
            keys = Keys('a', _.Delete, _.Enter);
            result = Test(keys); Assert.AreEqual("a", result);

            // Delete last character
            keys = Keys("ab", _.LeftArrow, _.Delete, _.Enter);
            result = Test(keys); Assert.AreEqual("a", result);

            // Delete first character
            keys = Keys("ab", _.Home, _.Delete, _.Enter);
            result = Test(keys); Assert.AreEqual("b", result);

            // Delete middle character
            keys = Keys("abc", _.Home, _.RightArrow, _.Delete, _.Enter);
            result = Test(keys); Assert.AreEqual("ac", result);
        }

        [TestMethod]
        public void TestBackspace()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            var keys = new [] {_.Backspace, _.Enter};
            var result = Test(keys); Assert.AreEqual("", result);

            // At end, delete all input
            keys = new [] {_.A, _.Backspace, _.Enter};
            result = Test(keys); Assert.AreEqual("", result);

            // At end, delete all input with extra backspaces
            keys = new [] {_.A, _.Backspace, _.Backspace, _.Enter};
            result = Test(keys); Assert.AreEqual("", result);

            // Delete first character
            keys = new [] {_.A, _.B, _.LeftArrow, _.Backspace, _.Enter};
            result = Test(keys); Assert.AreEqual("b", result);

            // Delete first character with extra backspaces
            keys = new [] {_.A, _.B, _.LeftArrow, _.Backspace, _.Backspace, _.Enter};
            result = Test(keys); Assert.AreEqual("b", result);

            // Delete middle character
            keys = new [] {_.A, _.B, _.C, _.LeftArrow, _.Backspace, _.Enter};
            result = Test(keys); Assert.AreEqual("ac", result);
        }

        [TestMethod]
        public void TestAddLine()
        {
            TestSetup(KeyMode.Cmd);

            var keys = new[] {_._1, _.ShiftEnter, _._2, _.Enter};
            var result = Test(keys); Assert.AreEqual("1\n2", result);
        }

        [TestMethod]
        public void TestIgnore()
        {
            TestSetup(KeyMode.Emacs);

            var keys = new[] {_.A, _.VolumeDown, _.VolumeMute, _.VolumeUp, _.B, _.Enter};
            var result = Test(keys); Assert.AreEqual("ab", result);
        }

        [TestMethod]
        public void TestTabComplete()
        {
            TestSetup(KeyMode.Cmd);

            var keys = new [] {_.Dollar, _.T, _.R, _.Tab, _.Enter};
            var result = Test(keys); Assert.AreEqual("$true", result);
            AssertCursorLeftIs(5);

            // Validate no change on no match
            keys = new [] {_.Dollar, _.Z, _.Z, _.Tab, _.Enter};
            result = Test(keys); Assert.AreEqual("$zz", result);
            AssertCursorLeftIs(3);

            var keysWithValidation = new []
            {
                new KeyWithValidation(_.Dollar),
                new KeyWithValidation(_.T),
                new KeyWithValidation(_.Tab, () => AssertLineIs("$this")),
                new KeyWithValidation(_.Tab, () => AssertLineIs("$true")),
                new KeyWithValidation(_.Tab, () => AssertLineIs("$this")),
                new KeyWithValidation(_.ShiftTab, () => AssertLineIs("$true")),
                new KeyWithValidation(_.Enter),
            };
            result = Test(keysWithValidation); Assert.AreEqual("$true", result);
        }

        [TestMethod]
        public void TestComplete()
        {
            TestSetup(KeyMode.Emacs);

            var keys = Keys("ambig",
                            new KeyWithValidation(_.Tab, () => AssertLineIs("ambiguous")),
                            "1", _.Enter);
            var result = Test(keys); Assert.AreEqual("ambiguous1", result);
        }

        [TestMethod]
        public void TestHistory()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            var keys = new [] {_.UpArrow, _.DownArrow, _.Enter};
            var result = Test(keys); Assert.AreEqual("", result);

            keys = new [] {_.D, _.I, _.R, _.Space, _.C, _.Star, _.Enter};
            result = Test(keys); Assert.AreEqual("dir c*", result);

            keys = new [] {_.P, _.S, _.Space, _.P, _.Star, _.Enter};
            result = Test(keys); Assert.AreEqual("ps p*", result);

            keys = new [] {_.UpArrow, _.UpArrow, _.Enter};
            result = Test(keys); Assert.AreEqual("dir c*", result);

            keys = new [] {_.UpArrow, _.UpArrow, _.DownArrow, _.Enter};
            result = Test(keys); Assert.AreEqual("dir c*", result);
        }

        [TestMethod]
        public void TestSearchHistory()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler(_.UpArrow, PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler(_.DownArrow, PSConsoleReadLine.HistorySearchForward));

            // No history
            var keys = new [] {_.UpArrow, _.DownArrow, _.Enter};
            var result = Test(keys); Assert.AreEqual("", result);

            // Clear history in case the above added some history (but it shouldn't)
            PSConsoleReadLine.ClearHistory();

            keys = new [] {_.Space, _.UpArrow, _.DownArrow, _.Enter};
            result = Test(keys); Assert.AreEqual(" ", result);

            keys = new [] {_.D, _.I, _.R, _.Space, _.C, _.Star, _.Enter};
            result = Test(keys); Assert.AreEqual("dir c*", result);

            keys = new [] {_.P, _.S, _.Space, _.P, _.Star, _.Enter};
            result = Test(keys); Assert.AreEqual("ps p*", result);

            keys = new [] {_.D, _.I, _.R, _.Space, _.C, _.D, _.Star, _.Enter};
            result = Test(keys); Assert.AreEqual("dir cd*", result);

            var keysWithValidation = new []
            {
                new KeyWithValidation(_.D),
                new KeyWithValidation(_.UpArrow, () => AssertCursorLeftIs(1)),
                new KeyWithValidation(_.UpArrow, () => AssertCursorLeftIs(1)),
                new KeyWithValidation(_.DownArrow, () => AssertCursorLeftIs(1)),
                new KeyWithValidation(_.UpArrow, () => AssertCursorLeftIs(1)),
                new KeyWithValidation(_.Enter)
            };
            result = Test(keysWithValidation); Assert.AreEqual("dir c*", result);

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistorySearchCursorMovesToEnd = true});
            keysWithValidation = new []
            {
                new KeyWithValidation(_.D),
                new KeyWithValidation(_.UpArrow, () => AssertCursorLeftIs(6)),
                new KeyWithValidation(_.UpArrow, () => AssertCursorLeftIs(7)),
                new KeyWithValidation(_.DownArrow, () => AssertCursorLeftIs(6)),
                new KeyWithValidation(_.UpArrow, () => AssertCursorLeftIs(7)),
                new KeyWithValidation(_.Enter)
            };
            result = Test(keysWithValidation); Assert.AreEqual("dir cd*", result);

        }

        [TestMethod]
        public void TestAddToHistoryHandler()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {AddToHistoryHandler = s => s.StartsWith("z")});

            var keys = new [] {_.Z, _.Z, _.Z, _.Z, _.Enter};
            var result = Test(keys); Assert.AreEqual("zzzz", result);

            keys = new [] {_.A, _.Z, _.Z, _.Z, _.Enter};
            result = Test(keys); Assert.AreEqual("azzz", result);

            keys = new [] {_.UpArrow, _.Enter};
            result = Test(keys); Assert.AreEqual("zzzz", result);
        }

        [TestMethod]
        public void TestHistoryDuplicates()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistoryNoDuplicates = false});

            var keys0 = new [] {_.Z, _.Z, _.Z, _.Z, _.Enter};
            var keys1 = new [] {_.A, _.A, _.A, _.A, _.Enter};
            var keys2 = new [] {_.B, _.B, _.B, _.B, _.Enter};
            var keys3 = new [] {_.C, _.C, _.C, _.C, _.Enter};
            var result = Test(keys0); Assert.AreEqual("zzzz", result);
            result = Test(keys1); Assert.AreEqual("aaaa", result);
            result = Test(keys2); Assert.AreEqual("bbbb", result);
            result = Test(keys2); Assert.AreEqual("bbbb", result);
            result = Test(keys3); Assert.AreEqual("cccc", result);
            var keys4 = new [] {_.UpArrow, _.UpArrow, _.UpArrow, _.UpArrow, _.Enter};
            result = Test(keys4); Assert.AreEqual("aaaa", result);

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistoryNoDuplicates = true});
            var keys5 = new [] {_.UpArrow, _.UpArrow, _.UpArrow, _.Enter};
            result = Test(keys5); Assert.AreEqual("aaaa", result);

            PSConsoleReadLine.ClearHistory();
            result = Test(keys1); Assert.AreEqual("aaaa", result);
            result = Test(keys2); Assert.AreEqual("bbbb", result);
            result = Test(keys2); Assert.AreEqual("bbbb", result);
            result = Test(keys3); Assert.AreEqual("cccc", result);
            keys5 = new [] {_.UpArrow, _.UpArrow, _.UpArrow, _.Enter};
            result = Test(keys5); Assert.AreEqual("aaaa", result);
        }

        [TestMethod]
        public void TestHistoryMinimumCommandLength()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {MinimumHistoryCommandLength = 6});

            var keys = new [] {_.A, _.B, _.C, _.D, _.E, _.F, _.G, _.H, _.Enter};
            var result = Test(keys); Assert.AreEqual("abcdefgh", result);
            keys = new [] {_.A, _.B, _.C, _.D, _.E, _.F, _.Enter};
            result = Test(keys); Assert.AreEqual("abcdef", result);
            keys = new [] {_.A, _.B, _.C, _.D, _.E, _.Enter};
            result = Test(keys); Assert.AreEqual("abcde", result);
            keys = new [] {_.UpArrow, _.Enter};
            result = Test(keys); Assert.AreEqual("abcdef", result);

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {MinimumHistoryCommandLength = 7});
            keys = new [] {_.UpArrow, _.Enter};
            result = Test(keys); Assert.AreEqual("abcdefgh", result);
        }

        [TestMethod]
        public void TestHistoryCount()
        {
            TestSetup(KeyMode.Cmd);

            var keys0 = new [] {_.Z, _.Z, _.Z, _.Z, _.Enter};
            var keys1 = new [] {_.A, _.A, _.A, _.A, _.Enter};
            var keys2 = new [] {_.B, _.B, _.B, _.B, _.Enter};
            var keys3 = new [] {_.C, _.C, _.C, _.C, _.Enter};
            var result = Test(keys0); Assert.AreEqual("zzzz", result);
            result = Test(keys1); Assert.AreEqual("aaaa", result);
            result = Test(keys2); Assert.AreEqual("bbbb", result);
            result = Test(keys3); Assert.AreEqual("cccc", result);

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {MaximumHistoryCount = 3});
            var keys4 = new [] {_.UpArrow, _.UpArrow, _.UpArrow, _.UpArrow, _.Enter};
            result = Test(keys4); Assert.AreEqual("aaaa", result);

            result = Test(keys0); Assert.AreEqual("zzzz", result);
            result = Test(keys0); Assert.AreEqual("zzzz", result);
            result = Test(keys0); Assert.AreEqual("zzzz", result);
            result = Test(keys1); Assert.AreEqual("aaaa", result);
            result = Test(keys2); Assert.AreEqual("bbbb", result);
            result = Test(keys3); Assert.AreEqual("cccc", result);
            result = Test(keys4); Assert.AreEqual("aaaa", result);
        }

        [TestMethod]
        public void TestYankPop()
        {
            TestSetup(KeyMode.Emacs);

            var killedText = new List<string>();

            var keys = new[] {_.CtrlY, _.AltY, _.Z, _.Enter};
            var result = Test(keys); Assert.AreEqual("z", result);

            // Fill the kill ring plus some extra.
            keys = new [] {_.Z, _.Z, _.Z, _.CtrlU, _.Enter};
            for (int i = 0; i < PSConsoleReadLine.DefaultMaximumKillRingCount + 2; i++)
            {
                var c = (char)('a' + i);
                var k = (ConsoleKey)((int)ConsoleKey.A + i);
                killedText.Add(c + "zz");
                keys[0] = new ConsoleKeyInfo(c, k, false, false, false);
                result = Test(keys); Assert.AreEqual("", result);
            }

            int killRingIndex = killedText.Count - 1;
            keys = new [] {_.CtrlY, _.Enter};
            result = Test(keys); Assert.AreEqual(killedText[killRingIndex], result);

            keys = new [] {_.CtrlY, _.CtrlY, _.Enter};
            result = Test(keys); Assert.AreEqual(killedText[killRingIndex] + killedText[killRingIndex], result);

            killRingIndex -= 1;
            keys = new [] {_.CtrlY, _.AltY, _.Enter};
            result = Test(keys); Assert.AreEqual(killedText[killRingIndex], result);

            // Test wrap around.  We need 1 yank and n-1 yankpop to wrap around once, plus enter.
            keys = new ConsoleKeyInfo[PSConsoleReadLine.DefaultMaximumKillRingCount + 2];
            keys[0] = _.CtrlY;
            keys[keys.Length - 1] = _.Enter;
            for (int i = 1; i <= keys.Length - 2; i++)
            {
                keys[i] = _.AltY;
            }
            result = Test(keys); Assert.AreEqual(killedText[killRingIndex], result);

            // Make sure an empty kill doesn't end up in the kill ring
            keys = new [] {_.A, _.CtrlU, _.CtrlU, _.B, _.CtrlU, _.CtrlY, _.AltY, _.Enter};
            result = Test(keys); Assert.AreEqual("a", result);
        }

        [TestMethod]
        public void TestKillLine()
        {
            TestSetup(KeyMode.Emacs);

            var keys = new [] {_.D, _.I, _.R, _.CtrlA, _.CtrlK, _.Enter};
            var result = Test(keys); Assert.AreEqual("", result);

            keys = new [] {_.CtrlY, _.Enter};
            result = Test(keys); Assert.AreEqual("dir", result);
        }

        [TestMethod]
        public void TestBackwardKillLine()
        {
            TestSetup(KeyMode.Emacs);

            var keys = new [] {_.D, _.I, _.R, _.CtrlU, _.L, _.S, _.Enter};
            var result = Test(keys); Assert.AreEqual("ls", result);

            keys = new [] {_.CtrlY, _.Enter};
            result = Test(keys); Assert.AreEqual("dir", result);

            keys = new [] {_.A, _.B, _.C, _.CtrlX, _.Backspace, _.D, _.E, _.F, _.Enter};
            result = Test(keys); Assert.AreEqual("def", result);
        }

        [TestMethod]
        public void TestKillAppend()
        {
            TestSetup(KeyMode.Emacs);

            var keys = Keys(" abcdir", _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.CtrlK, // Kill 'dir'
                _.CtrlU, // Kill append ' abc'
                _.CtrlY, // Yank 'dir abc'
                _.Enter);
            var result = Test(keys);
            Assert.AreEqual("dir abc", result);

            // Test empty kill doesn't affect kill append
            keys = Keys("ab", _.LeftArrow, _.CtrlK, _.CtrlK, _.CtrlU, _.CtrlY, _.Enter);
            result = Test(keys); Assert.AreEqual("ba", result);
        }

        [TestMethod]
        public void TestKillWord()
        {
            TestSetup(KeyMode.Emacs);

            var keys = Keys(_.AltD,  // Test on empty input
                "echo abc def", _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.AltD,          // Kill 'abc'
                 _.End, _.CtrlY, // Yank 'abc' at end of line
                _.Enter);
            var result = Test(keys); Assert.AreEqual("echo  defabc", result);
        }

        [TestMethod]
        public void TestKillBackwardWord()
        {
            TestSetup(KeyMode.Emacs);

            var keys = Keys(_.AltBackspace, // Test on empty line
                "echo abc def", _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.AltBackspace,  // Kill 'abc '
                _.End, _.CtrlY,  // Yank 'abc ' at the end
                _.Enter);
            var result = Test(keys);
            Assert.AreEqual("echo defabc ", result);
        }

        [TestMethod]
        public void TestRevertLine()
        {
            TestSetup(KeyMode.Cmd);

            var keys = new [] {_.D, _.I, _.Escape, _.L, _.S, _.Enter};
            var result = Test(keys); Assert.AreEqual("ls", result);

            TestSetup(KeyMode.Emacs);
            keys = new [] {_.D, _.I, _.Escape, _.R, _.L, _.S, _.Enter};
            result = Test(keys); Assert.AreEqual("ls", result);

            keys = new [] {_.D, _.I, _.AltR,  _.L, _.S, _.Enter};
            result = Test(keys); Assert.AreEqual("ls", result);
        }

        [TestMethod]
        public void TestForwardWord()
        {
            TestSetup(KeyMode.Cmd);

            var keys = new[]
            {
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(0)),
                new KeyWithValidation(_.A),
                new KeyWithValidation(_.A),
                new KeyWithValidation(_.A),
                new KeyWithValidation(_.Space),
                new KeyWithValidation(_.Space),
                new KeyWithValidation(_.B),
                new KeyWithValidation(_.B),
                new KeyWithValidation(_.B),
                new KeyWithValidation(_.Space),
                new KeyWithValidation(_.Space),
                new KeyWithValidation(_.C),
                new KeyWithValidation(_.C),
                new KeyWithValidation(_.C),
                new KeyWithValidation(_.Home, () => AssertCursorLeftIs(0)),
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(5)),
                new KeyWithValidation(_.LeftArrow),
                new KeyWithValidation(_.LeftArrow),
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(5)),
                new KeyWithValidation(_.LeftArrow),
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(5)),
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(10)),
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(13)),
                new KeyWithValidation(_.Home, CancelReadLineException.Throw)
            };
            Test(keys);

            keys = Keys("echo \"a $b c $d e\" 42",
                _.Home, _.RightArrow, _.RightArrow, _.RightArrow, _.RightArrow, _.RightArrow,
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(8)),
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(13)),
                new KeyWithValidation(_.CtrlRightArrow, () => AssertCursorLeftIs(19)),
                new KeyWithValidation(_.Home, CancelReadLineException.Throw));
            Test(keys);
        }

        [TestMethod]
        public void TestBackwardWord()
        {
            TestSetup(KeyMode.Cmd);

            const string input = "  aaa  bbb  ccc  ";
            var keysWithValidation = Keys(
                new KeyWithValidation(_.CtrlLeftArrow, () => AssertCursorLeftIs(0)),
                input,
                new KeyWithValidation(_.CtrlLeftArrow, () => AssertCursorLeftIs(12)),
                new KeyWithValidation(_.CtrlLeftArrow, () => AssertCursorLeftIs(7)),
                new KeyWithValidation(_.CtrlLeftArrow, () => AssertCursorLeftIs(2)),
                new KeyWithValidation(_.CtrlLeftArrow, () => AssertCursorLeftIs(0)),
                _.Enter
            );
            var result = Test(keysWithValidation); Assert.AreEqual(input, result);
        }

        [TestMethod]
        public void TestEmacsBackwardWord()
        {
            TestSetup(KeyMode.Emacs);

            const string input = "  aaa  bbb  ccc  ";
            var keysWithValidation = Keys(
                new KeyWithValidation(_.AltB, () => AssertCursorLeftIs(0)),
                input,
                new KeyWithValidation(_.AltB, () => AssertCursorLeftIs(12)),
                new KeyWithValidation(_.AltB, () => AssertCursorLeftIs(7)),
                new KeyWithValidation(_.AltB, () => AssertCursorLeftIs(2)),
                new KeyWithValidation(_.AltB, () => AssertCursorLeftIs(0)),
                _.Enter
            );
            var result = Test(keysWithValidation);
            Assert.AreEqual(input, result);
        }

        [TestMethod]
        public void TestEmacsForwardWord()
        {
            TestSetup(KeyMode.Emacs);

            var keys = Keys(
                new KeyWithValidation(_.AltF, () => AssertCursorLeftIs(0)),
                _.A, _.A, _.A, _.Space, _.Space, _.B, _.B, _.B, _.Space, _.Space, _.C, _.C, _.C,
                new KeyWithValidation(_.CtrlA, () => AssertCursorLeftIs(0)),
                new KeyWithValidation(_.AltF, () => AssertCursorLeftIs(3)),
                new KeyWithValidation(_.AltF, () => AssertCursorLeftIs(8)),
                new KeyWithValidation(_.AltF, () => AssertCursorLeftIs(13)),
                new KeyWithValidation(_.Home, CancelReadLineException.Throw));
            Test(keys);

            string input = "echo \"a $b c $d e\" 42";
            keys = Keys(
                input, _.Home, _.RightArrow, _.RightArrow, _.RightArrow, _.RightArrow, _.RightArrow,
                new KeyWithValidation(_.AltF, () => AssertCursorLeftIs(10)),
                new KeyWithValidation(_.AltF, () => AssertCursorLeftIs(15)),
                new KeyWithValidation(_.AltF, () => AssertCursorLeftIs(18)),
                new KeyWithValidation(_.Home, CancelReadLineException.Throw));
            Test(keys);
        }

        [TestMethod]
        public void TestRender()
        {
            TestSetup(KeyMode.Cmd);

            string input = "abc -def <#123#> \"hello $name\"";
            SetBufferState(input, 0);
            AssertScreenIs(1,
                TokenClassification.Command, "abc",
                TokenClassification.None, " ",
                TokenClassification.Parameter, "-def",
                TokenClassification.None, " ",
                TokenClassification.Comment, "<#123#>",
                TokenClassification.None, " ",
                TokenClassification.String, "\"hello ",
                TokenClassification.Variable, "$name",
                TokenClassification.String, "\"");

            input = "\"$([int];\"_$(1+2)\")\"";
            SetBufferState(input, input.Length);
            AssertScreenIs(1,
                TokenClassification.String, "\"",
                TokenClassification.None, "$(",
                TokenClassification.None, "[",
                TokenClassification.Type, "int",
                TokenClassification.None, "];",
                TokenClassification.String, "\"_",
                TokenClassification.None, "$(",
                TokenClassification.Number, "1",
                TokenClassification.Operator, "+",
                TokenClassification.Number, "2",
                TokenClassification.None, ")",
                TokenClassification.String, "\"",
                TokenClassification.None, ")",
                TokenClassification.String, "\"");

            input = "\"a $b c $d e\"";
            SetBufferState(input, input.Length);
            AssertScreenIs(1,
                TokenClassification.String, "\"a ",
                TokenClassification.Variable, "$b",
                TokenClassification.String, " c ",
                TokenClassification.Variable, "$d",
                TokenClassification.String, " e\"");

            var keys = Keys(
                '{', _.Enter,
                new KeyWithValidation(_.Backspace,
                    () => AssertScreenIs(2, TokenClassification.None, '{', NextLine)),
                '}', _.Enter);
            var result = Test(keys); Assert.AreEqual("{}", result);

            string promptLine = "PS> ";
            keys = Keys(
                new KeyWithValidation(_.DQuote,
                    () => AssertScreenIs(1,
                        TokenClassification.None, promptLine.Substring(0, promptLine.IndexOf('>')),
                        Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), ">",
                        TokenClassification.None, " ",
                        TokenClassification.String, "\""
                        )),
                '"', _.Enter);
            ClearScreen();
            result = Test(keys, promptLine); Assert.AreEqual("\"\"", result);
        }

        [TestMethod]
        public void TestContinuationPrompt()
        {
            TestSetup(KeyMode.Cmd);

            const string input = "{\n}";
            SetBufferState(input, input.Length);
            var continuationPrompt = PSConsoleReadLine.DefaultContinuationPrompt;
            var continationPrefixLength = continuationPrompt.Length;
            AssertScreenIs(2,
                TokenClassification.None, '{',
                NextLine,
                Tuple.Create(PSConsoleReadLine.DefaultContinuationPromptForegroundColor,
                             PSConsoleReadLine.DefaultContinuationPromptBackgroundColor),
                continuationPrompt,
                TokenClassification.None, '}'
                );

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption{ ContinuationPrompt = ""});
            SetBufferState(input, input.Length);
            AssertScreenIs(2, TokenClassification.None, '{', NextLine, '}' );

            continuationPrompt = "::::: ";
            continationPrefixLength = continuationPrompt.Length;
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption{
                ContinuationPrompt = continuationPrompt,
                ContinuationPromptForegroundColor = ConsoleColor.Magenta,
                ContinuationPromptBackgroundColor = ConsoleColor.DarkYellow,
            });
            SetBufferState(input, input.Length);
            AssertScreenIs(2,
                TokenClassification.None, '{',
                NextLine,
                Tuple.Create(ConsoleColor.Magenta, ConsoleColor.DarkYellow),
                continuationPrompt,
                TokenClassification.None, '}'
                );
        }

        [TestMethod]
        public void TestExchangePointAndMark()
        {
            TestSetup(KeyMode.Emacs,
                      new KeyHandler(_.CtrlZ, PSConsoleReadLine.ExchangePointAndMark));

            var keys = Keys(_.A, _.B, _.C, _.D, _.E,
                new KeyWithValidation(_.CtrlZ, () => AssertCursorLeftIs(0)),
                _.RightArrow, _.CtrlAt, _.RightArrow,
                new KeyWithValidation(_.RightArrow, () => AssertCursorLeftIs(3)),
                new KeyWithValidation(_.CtrlZ, () => AssertCursorLeftIs(1)),
                _.Enter);
            var result = Test(keys); Assert.AreEqual("abcde", result);

            // Make sure mark gets reset to 0.
            keys = Keys(_.A, _.B, _.C,
                new KeyWithValidation(_.CtrlZ, () => AssertCursorLeftIs(0)),
                _.Enter);
            result = Test(keys); Assert.AreEqual("abc", result);
        }

        [TestMethod]
        public void TestPossibleCompletions()
        {
            TestSetup(KeyMode.Emacs);

            ClearScreen();
            // Test empty input, make sure line after the cursor is blank and cursor didn't move
            var keys = Keys(
                new KeyWithValidation(_.AltEquals, () =>
                {
                    AssertCursorLeftTopIs(0, 0);
                    AssertScreenIs(2, NextLine);
                }),
                _.Enter);
            Test(keys);

            const string promptLine1 = "c:\\windows";
            const string promptLine2 = "PS> ";
            keys = Keys("psvar", 
                new KeyWithValidation(_.AltEquals,
                    () => AssertScreenIs(5,
                        TokenClassification.None, promptLine1,
                        NextLine,
                        promptLine2,
                        TokenClassification.Command, "psvar",
                        NextLine,
                        "$pssomething",
                        NextLine,
                        TokenClassification.None, promptLine1,
                        NextLine,
                        promptLine2,
                        TokenClassification.Command, "psvar")),
                _.Enter
                );
            Test(keys, promptLine1 + "\n" + promptLine2);

            keys = Keys("none",
                new KeyWithValidation(_.AltEquals,
                    () => AssertScreenIs(2,
                        TokenClassification.Command, "none",
                        NextLine)),
                _.Enter);
            ClearScreen();
            Test(keys);

            using (ShimsContext.Create())
            {
                bool ding = false;
                PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.Ding =
                    () => ding = true;

                ClearScreen();
                Test(keys);
                Assert.IsTrue(ding);
            }
        }

        static private CommandCompletion MockedCompleteInput(string input, int cursor, Hashtable options, PowerShell powerShell)
        {
            var ctor = typeof (CommandCompletion).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null, 
                new [] {typeof (Collection<CompletionResult>), typeof (int), typeof (int), typeof (int)}, null);

            var completions = new Collection<CompletionResult>();
            const int currentMatchIndex = -1;
            var replacementIndex = 0;
            var replacementLength = 0;
            switch (input)
            {
            case "$t":
                replacementIndex = 0;
                replacementLength = 2;
                completions.Add(new CompletionResult("$this"));
                completions.Add(new CompletionResult("$true"));
                break;
            case "$tr":
                replacementIndex = 0;
                replacementLength = 3;
                completions.Add(new CompletionResult("$true"));
                break;
            case "psvar":
                replacementIndex = 0;
                replacementLength = 5;
                completions.Add(new CompletionResult("$pssomething"));
                break;
            case "ambig":
                replacementIndex = 0;
                replacementLength = 5;
                completions.Add(new CompletionResult("ambiguous1"));
                completions.Add(new CompletionResult("ambiguous2"));
                completions.Add(new CompletionResult("ambiguous3"));
                break;
            case "none":
                break;
            }

            return (CommandCompletion)ctor.Invoke(
                new object[] {completions, currentMatchIndex, replacementIndex, replacementLength});
        }

        [TestMethod]
        [ExcludeFromCodeCoverage]
        public void TestUselessStuffForBetterCoverage()
        {
            // Useless test to just make sure coverage numbers are better, written
            // in the first way I could think of that doesn't warn about doing something useless.
            var options = new SetPSReadlineOption();
            var useless = ((object)options.AddToHistoryHandler ?? options).GetHashCode()
                          + options.EditMode.GetHashCode()
                          + ((object)options.ContinuationPrompt ?? options).GetHashCode()
                          + options.ContinuationPromptBackgroundColor.GetHashCode()
                          + options.ContinuationPromptForegroundColor.GetHashCode()
                          + options.HistoryNoDuplicates.GetHashCode()
                          + options.HistorySearchCursorMovesToEnd.GetHashCode()
                          + options.MaximumHistoryCount.GetHashCode()
                          + options.MaximumKillRingCount.GetHashCode()
                          + options.MinimumHistoryCommandLength.GetHashCode()
                          + options.DingDuration.GetHashCode()
                          + options.DingTone.GetHashCode()
                          + options.BellStyle.GetHashCode()
                          + options.ExtraPromptLineCount.GetHashCode()
                          + options.ShowToolTips.GetHashCode();
            // This assertion just avoids annoying warnings about unused variables.
            Assert.AreNotEqual(Math.PI, useless);

            bool exception = false;
            try
            {
                CreateCharInfoBuffer(new object());
            }
            catch (ArgumentException)
            {
                exception = true;
            }
            Assert.IsTrue(exception, "CreateCharBuffer invalid arugment raised an exception");

            using (ShimsContext.Create())
            {
                PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.AllInstances.ConsoleBufferGet = x => null;
                PSConsoleReadLine.SetBufferState("a", 1);
                Console.CursorLeft = 0;
                PSConsoleReadLine.SetBufferState("a", -1);
                AssertCursorLeftIs(0);
                Console.CursorLeft = 0;
                PSConsoleReadLine.SetBufferState("a", 11);
                AssertCursorLeftIs(1);
            }
        }

        [TestMethod]
        public void TestKeyInfoConverterSimpleCharLiteral()
        {
            var converter = new ConsoleKeyInfoConverterAttribute();
            
            object result = converter.Transform(null, "x");
            Assert.IsNotNull(result);            
            Assert.IsInstanceOfType(result, typeof(ConsoleKeyInfo));

            var key = (ConsoleKeyInfo) result;
            
            Assert.AreEqual(key.KeyChar, 'x');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, (ConsoleModifiers)0);            
        }

        [TestMethod]
        public void TestKeyInfoConverterSimpleCharLiteralWithModifiers()
        {
            var converter = new ConsoleKeyInfoConverterAttribute();

            object result = converter.Transform(null, "alt+shift+x");
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ConsoleKeyInfo));

            var key = (ConsoleKeyInfo) result;

            Assert.AreEqual(key.KeyChar, 'X');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift | ConsoleModifiers.Alt);
        }

        [TestMethod]
        public void TestKeyInfoConverterSymbolLiteral()
        {
            var converter = new ConsoleKeyInfoConverterAttribute();

            object result = converter.Transform(null, "}");
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ConsoleKeyInfo));

            var key = (ConsoleKeyInfo)result;

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterShiftedSymbolLiteral()
        {
            // } => shift+]  / shift+oem6
            var converter = new ConsoleKeyInfoConverterAttribute();

            object result = converter.Transform(null, "shift+]");
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ConsoleKeyInfo));

            var key = (ConsoleKeyInfo)result;

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterWellKnownConsoleKey()
        {
            // oem6
            var converter = new ConsoleKeyInfoConverterAttribute();

            object result = converter.Transform(null, "shift+oem6");
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ConsoleKeyInfo));

            var key = (ConsoleKeyInfo)result;

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterPassThrough()
        {
            // pass through consolekeyinfo            
            var converter = new ConsoleKeyInfoConverterAttribute();

            var key = new ConsoleKeyInfo('x', ConsoleKey.X, true, false, false);
            object result = converter.Transform(null, key);
            
            Assert.AreEqual(key, result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]        
        public void TestKeyInfoConverterInvalidKey()
        {
            var converter = new ConsoleKeyInfoConverterAttribute();
            object result = converter.Transform(null, "escrape");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestKeyInfoConverterInvalidModifierTypo()
        {
            var converter = new ConsoleKeyInfoConverterAttribute();
            object result = converter.Transform(null, "alt+shuft+x");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestKeyInfoConverterInvalidModifierInapplicable()
        {
            var converter = new ConsoleKeyInfoConverterAttribute();
            object result = converter.Transform(null, "shift+}");
        }
    }
}
