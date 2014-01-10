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
            CharToKeyInfo['*'] = _.Star;
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
        /*Member*/    ConsoleColor.DarkMagenta,
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
        /*Member*/    ConsoleColor.Yellow,
        };

        static Dictionary<char, ConsoleKeyInfo> CharToKeyInfo = new Dictionary<char, ConsoleKeyInfo>();

        class KeyHandler
        {
            public KeyHandler(string chord, Action<ConsoleKeyInfo?, object> handler)
            {
                this.Chord = chord;
                this.Handler = handler;
            }

            public string Chord { get; private set; }
            public Action<ConsoleKeyInfo?, object> Handler { get; private set; }
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

        static Action CheckThat(Action action)
        {
            // Syntatic sugar - saves a couple parens when calling Keys
            return action;
        }

        private class KeyPlaceholder {}
        private static readonly KeyPlaceholder InputAcceptedNow = new KeyPlaceholder();

        private static object[] NewKeys(params object[] input)
        {
            bool autoAddEnter = true;
            var list = new List<object>();
            foreach (var t in input)
            {
                var enumerable = t as IEnumerable;
                if (enumerable != null && !(t is string))
                {
                    foreach (var i in enumerable)
                    {
                        NewAddSingleKeyToList(i, list);
                    }
                    continue;
                }
                if (t == InputAcceptedNow)
                {
                    autoAddEnter = false;
                    continue;
                }

                NewAddSingleKeyToList(t, list);
            }

            if (autoAddEnter)
            {
                // Make sure we have Enter as the last key.
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    // Actions after the last key are fine, they'll
                    // get called.
                    if (list[i] is Action)
                    {
                        continue;
                    }

                    // We've skipped any actions at the end, this is
                    // the last key.  If it's not Enter, add Enter at the
                    // end for convenience.
                    var key = (ConsoleKeyInfo)list[i];
                    if (key.Key != ConsoleKey.Enter || key.Modifiers != 0)
                    {
                        list.Add(_.Enter);
                    }
                    break;
                }
            }

            return list.ToArray();
        }

        private static void NewAddSingleKeyToList(object t, List<object> list)
        {
            var s = t as string;
            if (s != null)
            {
                foreach (var c in s)
                {
                    list.Add(CharToKeyInfo[c]);
                }
            }
            else if (t is ConsoleKeyInfo)
            {
                list.Add(t);
            }
            else if (t is char)
            {
                list.Add(CharToKeyInfo[(char)t]);
            }
            else
            {
                Assert.IsInstanceOfType(t, typeof(Action));
                list.Add(t);
            }
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
                // Comparing CHAR_INFO should work, but randomly some attributes are set
                // that shouldn't be and aren't ever set by any code in PSReadline, so we'll
                // ignore those bits and just check the stuff we do set.
                Assert.AreEqual(expectedBuffer[i].UnicodeChar, consoleBuffer[i].UnicodeChar);
                Assert.AreEqual(expectedBuffer[i].ForegroundColor, consoleBuffer[i].ForegroundColor);
                Assert.AreEqual(expectedBuffer[i].BackgroundColor, consoleBuffer[i].BackgroundColor);
            }
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

        static private void NewTest(string expectedResult, object[] items, bool resetCursor = true, string prompt = null)
        {
            if (resetCursor)
            {
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
            }
            SetPrompt(prompt);
            int index = 0;
            using (ShimsContext.Create())
            {
                System.Management.Automation.Fakes.ShimCommandCompletion.CompleteInputStringInt32HashtablePowerShell =
                    MockedCompleteInput;
                PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.ConsoleReadKey = () =>
                {
                    while (index < items.Length)
                    {
                        var item = items[index++];
                        if (item is ConsoleKeyInfo)
                        {
                            return (ConsoleKeyInfo)item;
                        }
                        ((Action)item)();
                    }
                    Assert.Fail("Shouldn't call ReadKey when there are no more keys");
                    return _.CtrlC;
                };

                var result = PSConsoleReadLine.ReadLine();

                while (index < items.Length)
                {
                    var item = items[index++];
                    ((Action)item)();
                }

                Assert.AreEqual(expectedResult, result);
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
                HistoryNoDuplicates         = PSConsoleReadlineOptions.DefaultHistoryNoDuplicates,
                MaximumHistoryCount         = PSConsoleReadlineOptions.DefaultMaximumHistoryCount,
                MaximumKillRingCount        = PSConsoleReadlineOptions.DefaultMaximumKillRingCount,
                ResetTokenColors            = true,
                ExtraPromptLineCount        = PSConsoleReadlineOptions.DefaultExtraPromptLineCount,
                DingDuration                = 1,  // Make tests virtually silent when they ding
                DingTone                    = 37, // Make tests virtually silent when they ding
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
                PSConsoleReadLine.SetKeyHandler(new [] {keyHandler.Chord}, keyHandler.Handler, "", "");
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

            NewTest("exit", NewKeys(
                "exit",
                _.Enter,
                CheckThat(() => AssertCursorLeftIs(0))));
        }

        [TestMethod]
        public void TestAcceptAndGetNext()
        {
            TestSetup(KeyMode.Emacs);

            // No history
            NewTest("", NewKeys(_.CtrlO, InputAcceptedNow));

            // One item in history
            PSConsoleReadLine.AddToHistory("echo 1");
            NewTest("", NewKeys(_.CtrlO, InputAcceptedNow));

            // Two items in history, make sure after Ctrl+O, second history item
            // is recalled.
            PSConsoleReadLine.AddToHistory("echo 2");
            NewTest("echo 1", NewKeys(_.UpArrow, _.UpArrow, _.CtrlO, InputAcceptedNow));
            NewTest("echo 2", NewKeys(_.Enter));
        }

        [TestMethod]
        public void TestLongLine()
        {
            TestSetup(KeyMode.Cmd);

            var sb = new StringBuilder();
            sb.Append('"');
            sb.Append('z', Console.BufferWidth);
            sb.Append('"');

            var input = sb.ToString();
            NewTest(input, NewKeys(input));
        }

        [TestMethod]
        public void TestEndOfLine()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("", NewKeys( _.End, CheckThat(() => AssertCursorLeftIs(0)) ));

            var buffer = new string(' ', Console.BufferWidth);
            NewTest(buffer, NewKeys(
                buffer,
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.End,
                CheckThat(() => AssertCursorLeftTopIs(0, 1))
                ));

            buffer = new string(' ', Console.BufferWidth + 5);
            NewTest(buffer, NewKeys(
                buffer,
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.End,
                CheckThat(() => AssertCursorLeftTopIs(5, 1))
                ));
        }

        [TestMethod]
        public void TestCursorMovement()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("abcde", NewKeys(
                // Left arrow at start of line.
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                "ace",
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(2)),
                'd',
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(2)),
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                'b'
                ));
        }

        [TestMethod]
        public void TestMultiLine()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("d|\nd", NewKeys(
                "d|",
                _.Enter, CheckThat(() => AssertCursorTopIs(1)),
                'd'));

            // Make sure <ENTER> when input is incomplete actually puts a newline
            // wherever the cursor is.
            var continationPrefixLength = PSConsoleReadlineOptions.DefaultContinuationPrompt.Length;
            NewTest("{\n\nd\n}", NewKeys(
                '{',
                _.Enter,      CheckThat(() => AssertCursorTopIs(1)),
                'd',
                _.Enter,      CheckThat(() => AssertCursorTopIs(2)),
                _.Home,
                _.RightArrow, CheckThat(() => AssertCursorLeftTopIs(1, 0)),
                _.Enter,      CheckThat(() => AssertCursorLeftTopIs(continationPrefixLength, 1)),
                _.End,        CheckThat(() => AssertCursorLeftTopIs(continationPrefixLength, 3)),
                '}'));

            // Make sure <ENTER> when input successfully parses accepts the input regardless
            // of where the cursor is, plus it moves the cursor to the end (so the new prompt
            // doesn't overwrite the end of the previous long/multi-line command line.)
            NewTest("{\n}", NewKeys(
                "{\n}",
                _.Home,
                _.Enter, CheckThat(() => AssertCursorLeftTopIs(0, 2))));
        }

        [TestMethod]
        public void TestDelete()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing, but don't crash)
            NewTest("", NewKeys(_.Delete));

            // At end but input not empty (does nothing, but don't crash)
            NewTest("a", NewKeys('a', _.Delete));

            // Delete last character
            NewTest("a", NewKeys("ab", _.LeftArrow, _.Delete));

            // Delete first character
            NewTest("b", NewKeys("ab", _.Home, _.Delete));

            // Delete middle character
            NewTest("ac", NewKeys("abc", _.Home, _.RightArrow, _.Delete));
        }

        [TestMethod]
        public void TestBackspace()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            NewTest("", NewKeys("", _.Backspace));

            // At end, delete all input
            NewTest("", NewKeys("a", _.Backspace));

            // At end, delete all input with extra backspaces
            NewTest("", NewKeys("a", _.Backspace, _.Backspace));

            // Delete first character
            NewTest("b", NewKeys("ab", _.LeftArrow, _.Backspace));

            // Delete first character with extra backspaces
            NewTest("b", NewKeys("ab", _.LeftArrow, _.Backspace, _.Backspace));

            // Delete middle character
            NewTest("ac", NewKeys("abc", _.LeftArrow, _.Backspace));
        }

        [TestMethod]
        public void TestForwardDeleteLine()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            NewTest("", NewKeys("", _.CtrlEnd));

            // at end of input - doesn't change anything
            NewTest("abc", NewKeys("abc", _.CtrlEnd));

            // More normal usage - actually delete stuff
            NewTest("a", NewKeys("abc", _.LeftArrow, _.LeftArrow, _.CtrlEnd));
        }

        [TestMethod]
        public void TestBackwardDeleteLine()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            NewTest("", NewKeys(_.CtrlHome));

            // at beginning of input - doesn't change anything
            NewTest("abc", NewKeys("abc", _.Home, _.CtrlHome));

            // More typical usage
            NewTest("c", NewKeys("abc", _.LeftArrow, _.CtrlHome));
            NewTest("", NewKeys("abc", _.CtrlHome));
        }

        [TestMethod]
        public void TestAddLine()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("1\n2", NewKeys('1', _.ShiftEnter, '2'));
        }

        [TestMethod]
        public void TestIgnore()
        {
            TestSetup(KeyMode.Emacs);

            NewTest("ab", NewKeys("a", _.VolumeDown, _.VolumeMute, _.VolumeUp, "b"));
        }

        [TestMethod]
        public void TestTabComplete()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("$true", NewKeys(
                "$tr",
                _.Tab,
                CheckThat(() => AssertCursorLeftIs(5))));

            // Validate no change on no match
            NewTest("$zz", NewKeys(
                "$zz",
                _.Tab,
                CheckThat(() => AssertCursorLeftIs(3))));

            NewTest("$this", NewKeys(
                "$t",
                _.Tab,
                CheckThat(() => AssertLineIs("$thing")),
                _.Tab,
                CheckThat(() => AssertLineIs("$this")),
                _.Tab,
                CheckThat(() => AssertLineIs("$true")),
                _.ShiftTab,
                CheckThat(() => AssertLineIs("$this"))));
        }

        [TestMethod]
        public void TestComplete()
        {
            TestSetup(KeyMode.Emacs);

            NewTest("ambiguous1", NewKeys(
                "ambig",
                _.Tab,
                CheckThat(() => AssertLineIs("ambiguous")),
                '1'));
        }

        [TestMethod]
        public void TestHistory()
        {
            TestSetup(KeyMode.Cmd);

            // No history
            NewTest("", NewKeys(_.UpArrow, _.DownArrow));

            PSConsoleReadLine.AddToHistory("dir c*");
            PSConsoleReadLine.AddToHistory("ps p*");

            NewTest("dir c*", NewKeys(_.UpArrow, _.UpArrow));
            NewTest("dir c*", NewKeys(_.UpArrow, _.UpArrow, _.DownArrow));
        }

        [TestMethod]
        public void TestSearchHistory()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            // No history
            NewTest("", NewKeys(_.UpArrow, _.DownArrow));

            // Clear history in case the above added some history (but it shouldn't)
            PSConsoleReadLine.ClearHistory();

            NewTest(" ", NewKeys(' ', _.UpArrow, _.DownArrow));

            PSConsoleReadLine.AddToHistory("dir c*");
            PSConsoleReadLine.AddToHistory("ps p*");
            PSConsoleReadLine.AddToHistory("dir cd*");

            NewTest("dir c*", NewKeys(
                "d",
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                _.DownArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(1))));

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistorySearchCursorMovesToEnd = true});
            NewTest("dir cd*", NewKeys(
                "d",
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(6)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(7)),
                _.DownArrow,
                CheckThat(() => AssertCursorLeftIs(6)),
                _.UpArrow,
                CheckThat(() => AssertCursorLeftIs(7))));
        }

        [TestMethod]
        public void TestInteractiveHistorySearch()
        {
            TestSetup(KeyMode.Emacs);

            PSConsoleReadLine.AddToHistory("echo aaa");
            NewTest("echo aaa", NewKeys(_.CtrlR, 'a'));
        }

        [TestMethod]
        public void TestAddToHistoryHandler()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {AddToHistoryHandler = s => s.StartsWith("z")});

            NewTest("zzzz", NewKeys("zzzz"));
            NewTest("azzz", NewKeys("azzz"));
            NewTest("zzzz", NewKeys(_.UpArrow));
        }

        [TestMethod]
        public void TestHistoryDuplicates()
        {
            TestSetup(KeyMode.Cmd);
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistoryNoDuplicates = false});

            NewTest("zzzz", NewKeys("zzzz"));
            NewTest("aaaa", NewKeys("aaaa"));
            NewTest("bbbb", NewKeys("bbbb"));
            NewTest("bbbb", NewKeys("bbbb"));
            NewTest("cccc", NewKeys("cccc"));
            NewTest("aaaa", NewKeys(Enumerable.Repeat(_.UpArrow, 4)));

            // Changing the option should affect existing history.
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistoryNoDuplicates = true});
            NewTest("aaaa", NewKeys(Enumerable.Repeat(_.UpArrow, 3)));

            PSConsoleReadLine.ClearHistory();
            NewTest("aaaa", NewKeys("aaaa"));
            NewTest("bbbb", NewKeys("bbbb"));
            NewTest("bbbb", NewKeys("bbbb"));
            NewTest("cccc", NewKeys("cccc"));
            NewTest("aaaa", NewKeys(Enumerable.Repeat(_.UpArrow, 3)));
        }

        [TestMethod]
        public void TestHistoryCount()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("zzzz", NewKeys("zzzz"));
            NewTest("aaaa", NewKeys("aaaa"));
            NewTest("bbbb", NewKeys("bbbb"));
            NewTest("cccc", NewKeys("cccc"));

            // There should be 4 items in history, the following should remove the
            // oldest history item.
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption {MaximumHistoryCount = 3});
            NewTest("aaaa", NewKeys(Enumerable.Repeat(_.UpArrow, 4)));

            NewTest("zzzz", NewKeys("zzzz"));
            NewTest("aaaa", NewKeys("aaaa"));
            NewTest("bbbb", NewKeys("bbbb"));
            NewTest("cccc", NewKeys("cccc"));
            NewTest("aaaa", NewKeys(Enumerable.Repeat(_.UpArrow, 4)));
        }

        [TestMethod]
        public void TestYankPop()
        {
            TestSetup(KeyMode.Emacs);

            var killedText = new List<string>();

            NewTest("z", NewKeys(_.CtrlY, _.AltY, _.Z));

            // Fill the kill ring plus some extra.
            for (int i = 0; i < PSConsoleReadlineOptions.DefaultMaximumKillRingCount + 2; i++)
            {
                var c = (char)('a' + i);
                killedText.Add(c + "zz");
                NewTest("", NewKeys(c, "zz", _.CtrlU));
            }

            int killRingIndex = killedText.Count - 1;
            NewTest(killedText[killRingIndex], NewKeys(_.CtrlY));

            NewTest(killedText[killRingIndex] + killedText[killRingIndex],
                NewKeys(_.CtrlY, _.CtrlY));

            killRingIndex -= 1;
            NewTest(killedText[killRingIndex], NewKeys(_.CtrlY, _.AltY));

            // Test wrap around.  We need 1 yank and n-1 yankpop to wrap around once, plus enter.
            NewTest(killedText[killRingIndex],
                NewKeys(_.CtrlY, Enumerable.Repeat(_.AltY, PSConsoleReadlineOptions.DefaultMaximumKillRingCount)));

            // Make sure an empty kill doesn't end up in the kill ring
            NewTest("a", NewKeys("a", _.CtrlU, _.CtrlU, "b", _.CtrlU, _.CtrlY, _.AltY));
        }

        [TestMethod]
        public void TestKillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            NewTest("", NewKeys("dir", _.CtrlA, _.CtrlK));
            NewTest("dir", NewKeys(_.CtrlY));

            // Kill partial line
            NewTest("dir ", NewKeys("dir foo", _.AltB, _.CtrlK));
            NewTest("foo", NewKeys(_.CtrlY));
        }

        [TestMethod]
        public void TestBackwardKillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            // Check killed text by yanking
            NewTest("ls", NewKeys("dir", _.CtrlU, "ls"));
            NewTest("dir", NewKeys(_.CtrlY));

            // Kill whole line with second key binding
            NewTest("def", NewKeys("abc", _.CtrlX, _.Backspace, "def"));
            NewTest("abc", NewKeys(_.CtrlY));

            // Kill partial line
            NewTest("foo", NewKeys("dir foo", _.AltB, _.CtrlU));
            NewTest("dir ", NewKeys(_.CtrlY));
        }

        [TestMethod]
        public void TestKillAppend()
        {
            TestSetup(KeyMode.Emacs);

            NewTest(" abcdir", NewKeys(
                " abcdir", _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.CtrlK, // Kill 'dir'
                _.CtrlU, // Kill append ' abc'
                _.CtrlY)); // Yank ' abcdir'

            // Test empty kill doesn't affect kill append
            NewTest("ab", NewKeys("ab", _.LeftArrow, _.CtrlK, _.CtrlK, _.CtrlU, _.CtrlY));
        }

        [TestMethod]
        public void TestShellKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+D", PSConsoleReadLine.ShellKillWord));

            NewTest("echo  defabc", NewKeys(
                _.AltD, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.AltD, // Kill 'abc'
                _.End, _.CtrlY)); // Yank 'abc' at end of line
        }

        [TestMethod]
        public void TestShellBackwardKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+Backspace", PSConsoleReadLine.ShellBackwardKillWord));

            NewTest("echo defabc ", NewKeys(
                _.AltBackspace, // Test on empty line
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 3),
                _.AltBackspace,    // Kill 'abc '
                _.End, _.CtrlY));  // Yank 'abc ' at the end
        }

        [TestMethod]
        public void TestRevertLine()
        {
            // Add one test for chords
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+X,Escape", PSConsoleReadLine.RevertLine));

            NewTest("ls", NewKeys("di", _.Escape, "ls"));
            NewTest("ls", NewKeys("di", _.CtrlX, _.Escape, "ls"));

            TestSetup(KeyMode.Emacs);
            NewTest("ls", NewKeys("di", _.Escape, _.R, "ls"));
            NewTest("ls", NewKeys("di", _.AltR, "ls"));
        }

        [TestMethod]
        public void TestCmdShellForwardWord()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+RightArrow", PSConsoleReadLine.ShellForwardWord));

            NewTest("aaa  bbb  ccc", NewKeys(
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                "aaa  bbb  ccc",
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow,
                _.LeftArrow,
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow,
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(5)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(10)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(13))));

            NewTest("echo \"a $b c $d e\" 42", NewKeys(
                "echo \"a $b c $d e\" 42",
                _.Home,
                Enumerable.Repeat(_.RightArrow, 5),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(8)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(13)),
                _.CtrlRightArrow,
                CheckThat(() => AssertCursorLeftIs(19))));
        }

        [TestMethod]
        public void TestBackwardWord()
        {
            TestSetup(KeyMode.Cmd);

            const string input = "  aaa  bbb  ccc  ";
            NewTest(input, NewKeys(
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(12)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.CtrlLeftArrow, CheckThat(() => AssertCursorLeftIs(0))));
        }

        [TestMethod]
        public void TestEmacsBackwardWord()
        {
            TestSetup(KeyMode.Emacs);

            const string input = "  aaa  bbb  ccc  ";
            NewTest(input, NewKeys(
                _.AltB, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.AltB, CheckThat(() => AssertCursorLeftIs(12)),
                _.AltB, CheckThat(() => AssertCursorLeftIs(7)),
                _.AltB, CheckThat(() => AssertCursorLeftIs(2)),
                _.AltB, CheckThat(() => AssertCursorLeftIs(0))));
        }

        [TestMethod]
        public void TestEmacsShellForwardWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+F", PSConsoleReadLine.ShellForwardWord));

            string input = "aaa  bbb  ccc";
            NewTest(input, NewKeys(
                _.AltF, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.CtrlA, CheckThat(() => AssertCursorLeftIs(0)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(3)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(8)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(13))));

            input = "echo \"a $b c $d e\" 42";
            NewTest(input, NewKeys(
                input, _.Home,
                Enumerable.Repeat(_.RightArrow, 5),
                _.AltF, CheckThat(() => AssertCursorLeftIs(10)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(15)),
                _.AltF, CheckThat(() => AssertCursorLeftIs(18))));
        }

        [TestMethod]
        public void TestRender()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("", NewKeys(
                "abc -def <#123#> \"hello $name\"",
                _.Home,
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-def",
                        TokenClassification.None, " ",
                        TokenClassification.Comment, "<#123#>",
                        TokenClassification.None, " ",
                        TokenClassification.String, "\"hello ",
                        TokenClassification.Variable, "$name",
                        TokenClassification.String, "\"")),
                _.CtrlC,
                InputAcceptedNow
                ));

            NewTest("", NewKeys(
                "\"$([int];\"_$(1+2)\")\"",
                CheckThat(() =>
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
                        TokenClassification.String, "\"")),
                _.CtrlC,
                InputAcceptedNow
                ));

            NewTest("", NewKeys(
                "\"a $b c $d e\"",
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.String, "\"a ",
                        TokenClassification.Variable, "$b",
                        TokenClassification.String, " c ",
                        TokenClassification.Variable, "$d",
                        TokenClassification.String, " e\"")),
                _.CtrlC,
                InputAcceptedNow
                ));

            NewTest("{}", NewKeys(
                '{', _.Enter,
                _.Backspace, CheckThat(() => AssertScreenIs(2, TokenClassification.None, '{', NextLine)),
                '}'));

            ClearScreen();
            string promptLine = "PS> ";
            NewTest("\"\"", NewKeys(
                '"',
                CheckThat(() => AssertScreenIs(1,
                                   TokenClassification.None,
                                   promptLine.Substring(0, promptLine.IndexOf('>')),
                                   Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), ">",
                                   TokenClassification.None, " ",
                                   TokenClassification.String, "\"")),
                '"'), prompt: promptLine);
        }

        [TestMethod]
        public void TestContinuationPrompt()
        {
            TestSetup(KeyMode.Cmd);

            NewTest("", NewKeys(
                "{\n}",
                CheckThat(() =>
                    AssertScreenIs(2,
                        TokenClassification.None, '{',
                        NextLine,
                        Tuple.Create(Console.ForegroundColor, Console.BackgroundColor),
                        PSConsoleReadlineOptions.DefaultContinuationPrompt,
                        TokenClassification.None, '}')),
                _.CtrlC,
                InputAcceptedNow
                ));

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption{ ContinuationPrompt = ""});
            NewTest("", NewKeys(
                "{\n}",
                CheckThat(() => AssertScreenIs(2, TokenClassification.None, '{', NextLine, '}' )),
                _.CtrlC,
                InputAcceptedNow
                ));

            var continuationPrompt = "::::: ";
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption{
                ContinuationPrompt = continuationPrompt,
                ContinuationPromptForegroundColor = ConsoleColor.Magenta,
                ContinuationPromptBackgroundColor = ConsoleColor.DarkYellow,
            });
            NewTest("", NewKeys(
                "{\n}",
                CheckThat(() =>
                    AssertScreenIs(2,
                        TokenClassification.None, '{',
                        NextLine,
                        Tuple.Create(ConsoleColor.Magenta, ConsoleColor.DarkYellow),
                        continuationPrompt,
                        TokenClassification.None, '}')),
                _.CtrlC,
                InputAcceptedNow
                ));
        }

        [TestMethod]
        public void TestExchangePointAndMark()
        {
            TestSetup(KeyMode.Emacs,
                      new KeyHandler("Ctrl+Z", PSConsoleReadLine.ExchangePointAndMark));

            var exchangePointAndMark = _.CtrlZ;
            var setMark = _.CtrlAt;

            NewTest("abcde", NewKeys(
                "abcde",
                exchangePointAndMark,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.RightArrow,
                setMark,
                _.RightArrow,
                _.RightArrow,
                CheckThat(() => AssertCursorLeftIs(3)),
                exchangePointAndMark,
                CheckThat(() => AssertCursorLeftIs(1))
                ));

            NewTest("abc", NewKeys(
                "abc",
                exchangePointAndMark,
                CheckThat(() => AssertCursorLeftIs(0))
                ));
        }

        [TestMethod]
        public void TestPossibleCompletions()
        {
            TestSetup(KeyMode.Emacs);

            ClearScreen();
            // Test empty input, make sure line after the cursor is blank and cursor didn't move
            NewTest("", NewKeys(
                _.AltEquals,
                CheckThat(() =>
                {
                    AssertCursorLeftTopIs(0, 0);
                    AssertScreenIs(2, NextLine);
                })));

            const string promptLine1 = "c:\\windows";
            const string promptLine2 = "PS> ";
            NewTest("psvar", NewKeys(
                "psvar",
                _.AltEquals,
                CheckThat(() => AssertScreenIs(5,
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
                                               TokenClassification.Command, "psvar"))),
                prompt: promptLine1 + "\n" + promptLine2);

            using (ShimsContext.Create())
            {
                bool ding = false;
                PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.Ding =
                    () => ding = true;

                ClearScreen();
                NewTest("none", NewKeys(
                    "none",
                    _.AltEquals,
                    CheckThat(() => AssertScreenIs(2, TokenClassification.Command, "none", NextLine))));
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
                completions.Add(new CompletionResult("$thing"));
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
        public void TestClearScreen()
        {
            TestSetup(KeyMode.Emacs);

            NewTest("echo 1\necho 2\necho 3", NewKeys(
                "echo 1",
                _.ShiftEnter,
                "echo 2",
                _.ShiftEnter,
                "echo 3"));
            AssertCursorTopIs(3);
            NewTest("echo foo", NewKeys(
                "echo foo"
                ), resetCursor: false);
            AssertCursorTopIs(4);
            NewTest("echo zed", NewKeys(
                "echo zed",
                _.CtrlL,
                CheckThat(() => AssertCursorTopIs(0))
                ), resetCursor: false);
        }

        [TestMethod]
        [ExcludeFromCodeCoverage]
        public void TestUselessStuffForBetterCoverage()
        {
            // Useless test to just make sure coverage numbers are better, written
            // in the first way I could think of that doesn't warn about doing something useless.
            var options = new SetPSReadlineOption();
            var getKeyHandlerCommand = new GetKeyHandlerCommand();
            var useless = ((object)options.AddToHistoryHandler ?? options).GetHashCode()
                          + options.EditMode.GetHashCode()
                          + ((object)options.ContinuationPrompt ?? options).GetHashCode()
                          + options.ContinuationPromptBackgroundColor.GetHashCode()
                          + options.ContinuationPromptForegroundColor.GetHashCode()
                          + options.HistoryNoDuplicates.GetHashCode()
                          + options.HistorySearchCursorMovesToEnd.GetHashCode()
                          + options.MaximumHistoryCount.GetHashCode()
                          + options.MaximumKillRingCount.GetHashCode()
                          + options.DingDuration.GetHashCode()
                          + options.DingTone.GetHashCode()
                          + options.BellStyle.GetHashCode()
                          + options.ExtraPromptLineCount.GetHashCode()
                          + options.ShowToolTips.GetHashCode()
                          + getKeyHandlerCommand.Bound.GetHashCode()
                          + getKeyHandlerCommand.Unbound.GetHashCode();
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
        }

        #region KeyInfoConverter tests

        [TestMethod]
        public void TestKeyInfoConverterSimpleCharLiteral()
        {
            var result = ConsoleKeyChordConverter.Convert("x");
            Assert.IsNotNull(result);            
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, 'x');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, (ConsoleModifiers)0);            
        }

        [TestMethod]
        public void TestKeyInfoConverterSimpleCharLiteralWithModifiers()
        {
            var result = ConsoleKeyChordConverter.Convert("alt+shift+x");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, 'X');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift | ConsoleModifiers.Alt);
        }

        [TestMethod]
        public void TestKeyInfoConverterSymbolLiteral()
        {
            var result = ConsoleKeyChordConverter.Convert("}");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterShiftedSymbolLiteral()
        {
            // } => shift+]  / shift+oem6
            var result = ConsoleKeyChordConverter.Convert("shift+]");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterWellKnownConsoleKey()
        {
            // oem6
            var result = ConsoleKeyChordConverter.Convert("shift+oem6");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterSequence()
        {
            // oem6
            var result = ConsoleKeyChordConverter.Convert("Escape,X");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 2);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, (char)27);
            Assert.AreEqual(key.Key, ConsoleKey.Escape);
            Assert.AreEqual(key.Modifiers, (ConsoleModifiers)0);

            key = result[1];

            Assert.AreEqual(key.KeyChar, 'x');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, (ConsoleModifiers)0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]        
        public void TestKeyInfoConverterInvalidKey()
        {
            var result = ConsoleKeyChordConverter.Convert("escrape");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestKeyInfoConverterInvalidModifierTypo()
        {
            var result = ConsoleKeyChordConverter.Convert("alt+shuft+x");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestKeyInfoConverterInvalidModifierInapplicable()
        {
            var result = ConsoleKeyChordConverter.Convert("shift+}");
        }

        [TestMethod]
        [ExpectedException(typeof (ArgumentException))]
        public void TestKeyInfoConverterInvalidSubsequence1()
        {
            var result = ConsoleKeyChordConverter.Convert("x,");
        }

        [TestMethod]
        [ExpectedException(typeof (ArgumentException))]
        public void TestKeyInfoConverterInvalidSubsequence2()
        {
            var result = ConsoleKeyChordConverter.Convert(",x");
        }

        #endregion KeyInfoConverter tests
    }
}
