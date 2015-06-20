using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    internal class MockedMethods : Microsoft.PowerShell.Internal.IPSConsoleReadLineMockableMethods
    {
        internal int index;
        internal object[] items;
        internal Exception validationFailure;
        internal bool didDing;

        internal MockedMethods(object[] items)
        {
            this.index = 0;
            this.items = items;
            this.validationFailure = null;
            this.didDing = false;
        }

        public ConsoleKeyInfo ReadKey()
        {
            while (index < items.Length)
            {
                var item = items[index++];
                if (item is ConsoleKeyInfo)
                {
                    return (ConsoleKeyInfo)item;
                }
                try
                {
                    ((Action)item)();
                }
                catch (Exception e)
                {
                    // Just remember the first exception
                    if (validationFailure == null)
                    {
                        validationFailure = e;
                    }
                    // In the hopes of avoiding additional failures, try cancelling via Ctrl+C.
                    return _.CtrlC;
                }
            }

            validationFailure = new Exception("Shouldn't call ReadKey when there are no more keys");
            return _.CtrlC;
        }

        public bool KeyAvailable()
        {
            return index < items.Length && items[index] is ConsoleKeyInfo;
        }

        public void Ding()
        {
            didDing = true;
        }

        public CommandCompletion CompleteInput(string input, int cursorIndex, Hashtable options, PowerShell powershell)
        {
            return UnitTest.MockedCompleteInput(input, cursorIndex, options, powershell);
        }

        public bool RunspaceIsRemote(Runspace runspace)
        {
            return false;
        }
    }

    [TestClass]
    public partial class UnitTest
    {
        static UnitTest()
        {
            var iss = InitialSessionState.CreateDefault2();
            var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();
            Runspace.DefaultRunspace = rs;

            for (var i = 'a'; i <= 'z'; i++)
            {
                CharToKeyInfo[i] = new ConsoleKeyInfo(i, ConsoleKey.A + i - 'a', false, false, false);
            }
            for (var i = 'A'; i <= 'Z'; i++)
            {
                CharToKeyInfo[i] = new ConsoleKeyInfo(i, ConsoleKey.A + i - 'A', true, false, false);
            }
            for (var i = '0'; i <= '9'; i++)
            {
                CharToKeyInfo[i] = new ConsoleKeyInfo(i, ConsoleKey.D0 + i - '0', false, false, false);
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
            CharToKeyInfo[':'] = _.Colon;
            CharToKeyInfo['"'] = _.DQuote;
            CharToKeyInfo['\''] = _.SQuote;
            CharToKeyInfo['\\'] = _.Backslash;
            CharToKeyInfo['/'] = _.Slash;
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

        private static string GetClipboardText()
        {
            string fromClipboard = null;
            ExecuteOnSTAThread(() =>
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Assert.IsTrue(Clipboard.ContainsText());
                        fromClipboard = Clipboard.GetText();
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                    }
                }
            });
            return fromClipboard;
        }

        private void AssertClipboardTextIs(string text)
        {
            var fromClipboard = GetClipboardText();
            Assert.AreEqual(text, fromClipboard);
        }

        private void AssertScreenCaptureClipboardIs(params string[] lines)
        {
            var fromClipboard = GetClipboardText();
            var newline = Environment.NewLine;
            var text = string.Join(Environment.NewLine, lines);
            if (!text.EndsWith(newline))
            {
                text = text + newline;
            }
            Assert.AreEqual(text, fromClipboard);
        }

        private static void ExecuteOnSTAThread(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                throw exception;
            }
        }

        private class NextLineToken { }
        static NextLineToken NextLine = new NextLineToken();

        private class InvertedToken { }
        static InvertedToken Inverted = new InvertedToken();

        private CHAR_INFO[] CreateCharInfoBuffer(int lines, params object[] items)
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
                if (item is InvertedToken)
                {
                    fg = (ConsoleColor)((int)fg ^ 7);
                    bg = (ConsoleColor)((int)bg ^ 7);
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

            var extraSpacesNeeded = (lines * Console.BufferWidth) - result.Count;
            if (extraSpacesNeeded > 0)
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

        private static object[] Keys(params object[] input)
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
            NativeMethods.ScrollConsoleScreenBuffer(handle, ref scrollRectangle, IntPtr.Zero, destinationOrigin, ref fillChar);
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

            var expectedBuffer = CreateCharInfoBuffer(lines, items);
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

        [ExcludeFromCodeCoverage]
        static private void Test(string expectedResult, object[] items, bool resetCursor = true, string prompt = null, bool mustDing = false)
        {
            if (resetCursor)
            {
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
                Console.BufferWidth = Console.WindowWidth = 60;
                Console.BufferHeight = Console.WindowHeight = 40;
            }
            SetPrompt(prompt);

            var mockedMethods = new MockedMethods(items);
            var instance = (PSConsoleReadLine)typeof(PSConsoleReadLine)
                .GetField("_singleton", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            typeof(PSConsoleReadLine)
                .GetField("_mockableMethods", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(instance, mockedMethods);

            var result = PSConsoleReadLine.ReadLine(null, null);

            if (mockedMethods.validationFailure != null)
            {
                throw new Exception("", mockedMethods.validationFailure);
            }

            while (mockedMethods.index < mockedMethods.items.Length)
            {
                var item = mockedMethods.items[mockedMethods.index++];
                ((Action)item)();
            }

            Assert.AreEqual(expectedResult, result);

            if (mustDing)
            {
                Assert.IsTrue(mockedMethods.didDing);
            }
        }

        static private void TestMustDing(string expectedResult, object[] items, bool resetCursor = true, string prompt = null)
        {
            Test(expectedResult, items, resetCursor, prompt, true);
        }

        private void TestSetup(KeyMode keyMode, params KeyHandler[] keyHandlers)
        {
            Console.Clear();
            PSConsoleReadLine.ClearHistory();
            PSConsoleReadLine.ClearKillRing();

            // We don't want stdout redirected, we want it sent to the screen.
            var standardOutput = new StreamWriter(Console.OpenStandardOutput()) {AutoFlush = true};
            Console.SetOut(standardOutput);

            var options = new SetPSReadlineOption
            {
                AddToHistoryHandler               = null,
                BellStyle                         = PSConsoleReadlineOptions.DefaultBellStyle,
                CompletionQueryItems              = PSConsoleReadlineOptions.DefaultCompletionQueryItems,
                ContinuationPrompt                = PSConsoleReadlineOptions.DefaultContinuationPrompt,
                ContinuationPromptBackgroundColor = Console.BackgroundColor,
                ContinuationPromptForegroundColor = Console.ForegroundColor,
                DingDuration                      = 1,  // Make tests virtually silent when they ding
                DingTone                          = 37, // Make tests virtually silent when they ding
                EmphasisBackgroundColor           = Console.BackgroundColor,
                EmphasisForegroundColor           = PSConsoleReadlineOptions.DefaultEmphasisForegroundColor,
                ExtraPromptLineCount              = PSConsoleReadlineOptions.DefaultExtraPromptLineCount,
                HistoryNoDuplicates               = PSConsoleReadlineOptions.DefaultHistoryNoDuplicates,
                HistorySaveStyle                  = HistorySaveStyle.SaveNothing,
                HistorySearchCaseSensitive        = PSConsoleReadlineOptions.DefaultHistorySearchCaseSensitive,
                HistorySearchCursorMovesToEnd     = PSConsoleReadlineOptions.DefaultHistorySearchCursorMovesToEnd,
                MaximumHistoryCount               = PSConsoleReadlineOptions.DefaultMaximumHistoryCount,
                MaximumKillRingCount              = PSConsoleReadlineOptions.DefaultMaximumKillRingCount,
                ResetTokenColors                  = true,
                ShowToolTips                      = PSConsoleReadlineOptions.DefaultShowToolTips,
                WordDelimiters                    = PSConsoleReadlineOptions.DefaultWordDelimiters,
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

    }
}
