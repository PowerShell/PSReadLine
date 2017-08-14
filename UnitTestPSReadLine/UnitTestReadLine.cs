using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;
using Microsoft.PowerShell.Internal;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    internal class MockedMethods : IPSConsoleReadLineMockableMethods
    {
        internal bool didDing;

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

    internal class TestConsole : IConsole
    {
        internal int index;
        internal object[] inputOrValidateItems;
        internal Exception validationFailure;
        private readonly CHAR_INFO[] buffer;
        private readonly int _bufferWidth;
        private readonly int _bufferHeight;
        private readonly int _windowWidth;
        private readonly int _windowHeight;

        internal TestConsole()
        {
            BackgroundColor = UnitTest.BackgroundColors[0];
            ForegroundColor = UnitTest.Colors[0];
            CursorLeft = 0;
            CursorTop = 0;
            _bufferWidth = _windowWidth = 60;
            _bufferHeight = _windowHeight = 1000; // big enough to avoid the need to implement scrolling
            buffer = new CHAR_INFO[BufferWidth * BufferHeight];
            ClearBuffer();
        }

        internal void Init(object[] items)
        {
            this.index = 0;
            this.inputOrValidateItems = items;
            this.validationFailure = null;
        }

        public ConsoleKeyInfo ReadKey()
        {
            while (index < inputOrValidateItems.Length)
            {
                var item = inputOrValidateItems[index++];
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

        public bool KeyAvailable => index < inputOrValidateItems.Length && inputOrValidateItems[index] is ConsoleKeyInfo;

        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }

        public int CursorSize { get; set; }

        public int BufferWidth
        {
            get => _bufferWidth;
            set => throw new NotImplementedException();
        }

        public int BufferHeight
        {
            get => _bufferHeight;
            set => throw new NotImplementedException();
        }

        public int WindowWidth
        {
            get => _windowWidth;
            set => throw new NotImplementedException();
        }

        public int WindowHeight
        {
            get => _windowHeight;
            set => throw new NotImplementedException();
        }

        public int WindowTop { get; set; }

        public ConsoleColor BackgroundColor
        {
            get => _backgroundColor;
            set => _backgroundColor = Negative ? (ConsoleColor)((int)value ^ 7) : value;
        }
        private ConsoleColor _backgroundColor;

        public ConsoleColor ForegroundColor
        {
            get => _foregroundColor;
            set => _foregroundColor = Negative ? (ConsoleColor)((int)value ^ 7) : value;
        }
        private ConsoleColor _foregroundColor;

        private bool Negative;

        public void SetWindowPosition(int left, int top)
        {
        }

        public void SetCursorPosition(int left, int top)
        {
            CursorLeft = left;
            CursorTop = top;
        }

        public void WriteLine(string s)
        {
            // Crappy code here - no checks for a string that's too long, no scrolling.
            Write(s);
            CursorLeft = 0;
            CursorTop += 1;
        }

        public void Write(string s)
        {
            // Crappy code here - no checks for a string that's too long, no scrolling.
            var writePos = CursorTop * BufferWidth + CursorLeft;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == (char) 0x1b)
                {
                    // Escape sequence - limited support here, and assumed to be well formed.
                    var endSequence = s.IndexOf("m", i, StringComparison.Ordinal);
                    var escapeSequence = s.Substring(i, endSequence - i + 1);
                    EscapeSequenceActions[escapeSequence](this);
                    i = endSequence;
                    continue;
                }

                if (s[i] == '\b')
                {
                    CursorLeft -= 1;
                    if (CursorLeft < 0)
                    {
                        CursorTop -= 1;
                        CursorLeft = BufferWidth - 1;
                    }
                }
                else if (s[i] == '\n')
                {
                    CursorTop += 1;
                    CursorLeft = 0;
                    writePos = CursorTop * BufferWidth;
                }
                else
                {
                    CursorLeft += 1;
                    if (CursorLeft == BufferWidth)
                    {
                        CursorLeft = 0;
                        CursorTop += 1;
                    }
                    buffer[writePos].UnicodeChar = s[i];
                    buffer[writePos].BackgroundColor = BackgroundColor;
                    buffer[writePos].ForegroundColor = ForegroundColor;
                    writePos += 1;
                }
            }
        }

        public void WriteBufferLines(CHAR_INFO[] bufferToWrite, ref int top, bool ensureBottomLineVisible)
        {
            var startPos = top * BufferWidth;
            for (int i = 0; i < bufferToWrite.Length; i++)
            {
                buffer[startPos + i] = bufferToWrite[i];
            }
        }

        public void ScrollBuffer(int lines)
        {
        }

        public uint GetConsoleInputMode()
        {
            return 0;
        }

        public void SetConsoleInputMode(uint mode)
        {
        }

        public void Clear()
        {
            SetCursorPosition(0, 0);
            ClearBuffer();
        }

        void ClearBuffer()
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = new CHAR_INFO(' ', ForegroundColor, BackgroundColor);
            }
        }

        public CHAR_INFO[] ReadBufferLines(int top, int count)
        {
            var toCopy = BufferWidth * count;
            var result = new CHAR_INFO[toCopy];
            Array.Copy(buffer, top * BufferWidth, result, 0, toCopy);
            return result;
        }

        public bool IsHandleRedirected(bool stdin)
        {
            return false;
        }

        private int _savedX, _savedY;

        public void SaveCursor()
        {
            _savedX = CursorLeft;
            _savedY = CursorTop;
        }

        public void RestoreCursor()
        {
            SetCursorPosition(_savedX, _savedY);
        }

        private static readonly ConsoleColor DefaultForeground = UnitTest.Colors[0];
        private static readonly ConsoleColor DefaultBackground = UnitTest.BackgroundColors[0];

        private static void ToggleNegative(TestConsole c, bool b)
        {
            c.Negative = false;
            c.ForegroundColor = (ConsoleColor)((int)c.ForegroundColor ^ 7);
            c.BackgroundColor = (ConsoleColor)((int)c.BackgroundColor ^ 7);
            c.Negative = b;
        }
        private static readonly Dictionary<string, Action<TestConsole>> EscapeSequenceActions = new Dictionary<string, Action<TestConsole>> {
            {"\x1b[7m", c => ToggleNegative(c, true) },
            {"\x1b[27m", c => ToggleNegative(c, false) },
            {"\x1b[40m", c => c.BackgroundColor = ConsoleColor.Black},
            {"\x1b[44m", c => c.BackgroundColor = ConsoleColor.DarkBlue },
            {"\x1b[42m", c => c.BackgroundColor = ConsoleColor.DarkGreen},
            {"\x1b[46m", c => c.BackgroundColor = ConsoleColor.DarkCyan},
            {"\x1b[41m", c => c.BackgroundColor = ConsoleColor.DarkRed},
            {"\x1b[45m", c => c.BackgroundColor = ConsoleColor.DarkMagenta},
            {"\x1b[43m", c => c.BackgroundColor = ConsoleColor.DarkYellow},
            {"\x1b[47m", c => c.BackgroundColor = ConsoleColor.Gray},
            {"\x1b[100m", c => c.BackgroundColor = ConsoleColor.DarkGray},
            {"\x1b[104m", c => c.BackgroundColor = ConsoleColor.Blue},
            {"\x1b[102m", c => c.BackgroundColor = ConsoleColor.Green},
            {"\x1b[106m", c => c.BackgroundColor = ConsoleColor.Cyan},
            {"\x1b[101m", c => c.BackgroundColor = ConsoleColor.Red},
            {"\x1b[105m", c => c.BackgroundColor = ConsoleColor.Magenta},
            {"\x1b[103m", c => c.BackgroundColor = ConsoleColor.Yellow},
            {"\x1b[107m", c => c.BackgroundColor = ConsoleColor.White},
            {"\x1b[30m", c => c.ForegroundColor = ConsoleColor.Black},
            {"\x1b[34m", c => c.ForegroundColor = ConsoleColor.DarkBlue},
            {"\x1b[32m", c => c.ForegroundColor = ConsoleColor.DarkGreen},
            {"\x1b[36m", c => c.ForegroundColor = ConsoleColor.DarkCyan},
            {"\x1b[31m", c => c.ForegroundColor = ConsoleColor.DarkRed},
            {"\x1b[35m", c => c.ForegroundColor = ConsoleColor.DarkMagenta},
            {"\x1b[33m", c => c.ForegroundColor = ConsoleColor.DarkYellow},
            {"\x1b[37m", c => c.ForegroundColor = ConsoleColor.Gray},
            {"\x1b[90m", c => c.ForegroundColor = ConsoleColor.DarkGray},
            {"\x1b[94m", c => c.ForegroundColor = ConsoleColor.Blue},
            {"\x1b[92m", c => c.ForegroundColor = ConsoleColor.Green},
            {"\x1b[96m", c => c.ForegroundColor = ConsoleColor.Cyan},
            {"\x1b[91m", c => c.ForegroundColor = ConsoleColor.Red},
            {"\x1b[95m", c => c.ForegroundColor = ConsoleColor.Magenta},
            {"\x1b[93m", c => c.ForegroundColor = ConsoleColor.Yellow},
            {"\x1b[97m", c => c.ForegroundColor = ConsoleColor.White},
            {"\x1b[0m", c => {
                c.ForegroundColor = DefaultForeground;
                c.BackgroundColor = DefaultBackground;
            }}
        };
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
            Vi
        };

        // These colors are random - we just use these colors instead of the defaults
        // so the tests aren't sensitive to tweaks to the default colors.
        internal static readonly ConsoleColor[] Colors = new []
        {
        /*None*/      ConsoleColor.DarkRed,
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

        internal static readonly ConsoleColor[] BackgroundColors = new[]
        {
        /*None*/      ConsoleColor.DarkGray,
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

        static readonly Dictionary<char, ConsoleKeyInfo> CharToKeyInfo = new Dictionary<char, ConsoleKeyInfo>();

        class KeyHandler
        {
            public KeyHandler(string chord, Action<ConsoleKeyInfo?, object> handler)
            {
                this.Chord = chord;
                this.Handler = handler;
            }

            public string Chord { get; }
            public Action<ConsoleKeyInfo?, object> Handler { get; }
        }

        private void AssertCursorLeftTopIs(int left, int top)
        {
            AssertCursorLeftIs(left);
            AssertCursorTopIs(top);
        }

        private void AssertCursorLeftIs(int expected)
        {
            Assert.AreEqual(expected, _console.CursorLeft);
        }

        private void AssertCursorTopIs(int expected)
        {
            Assert.AreEqual(expected, _console.CursorTop);
        }

        private void AssertLineIs(string expected)
        {
            PSConsoleReadLine.GetBufferState(out var input, out var unused);
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
        static readonly NextLineToken NextLine = new NextLineToken();

        private class InvertedToken { }
        static readonly InvertedToken Inverted = new InvertedToken();

        private CHAR_INFO[] CreateCharInfoBuffer(int lines, params object[] items)
        {
            var result = new List<CHAR_INFO>();
            var fg = _console.ForegroundColor;
            var bg = _console.BackgroundColor;

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
                    item = new string(' ', _console.BufferWidth - (result.Count % _console.BufferWidth));
                    fg = _console.ForegroundColor;
                    bg = _console.BackgroundColor;
                    // Fallthrough to string case.
                }
                if (item is string str)
                {
                    result.AddRange(str.Select(c => new CHAR_INFO(c, fg, bg)));
                    continue;
                }
                if (item is TokenClassification)
                {
                    fg = Colors[(int)(TokenClassification)item];
                    bg = BackgroundColors[(int)(TokenClassification)item];
                    continue;
                }
                if (item is Tuple<ConsoleColor, ConsoleColor> tuple)
                {
                    fg = tuple.Item1;
                    bg = tuple.Item2;
                    continue;
                }
                throw new ArgumentException("Unexpected type");
            }

            var extraSpacesNeeded = (lines * _console.BufferWidth) - result.Count;
            if (extraSpacesNeeded > 0)
            {
                var space = new CHAR_INFO(' ', _console.ForegroundColor, _console.BackgroundColor);
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
                if (t is IEnumerable enumerable && !(t is string))
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
            if (t is string s)
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

        private void AssertScreenIs(int lines, params object[] items)
        {
            var consoleBuffer = _console.ReadBufferLines(0, lines);

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

        private void SetPrompt(string prompt)
        {
            var options = new SetPSReadlineOption {ExtraPromptLineCount = 0};
            if (string.IsNullOrEmpty(prompt))
            {
                options.PromptText = "";
                PSConsoleReadLine.SetOptions(options);
                return;
            }

            int i;
            for (i = prompt.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(prompt[i])) break;
            }

            options.PromptText = prompt.Substring(i);

            var lineCount = 1 + prompt.Count(c => c == '\n');
            if (lineCount > 1)
            {
                options.ExtraPromptLineCount = lineCount - 1;
            }
            PSConsoleReadLine.SetOptions(options);
            _console.Write(prompt);
        }

        [ExcludeFromCodeCoverage]
        private void Test(string expectedResult, object[] items)
        {
            Test(expectedResult, items, resetCursor: true, prompt: null, mustDing: false);
        }

        [ExcludeFromCodeCoverage]
        private void Test(string expectedResult, object[] items, string prompt)
        {
            Test(expectedResult, items, resetCursor: true, prompt: prompt, mustDing: false);
        }

        [ExcludeFromCodeCoverage]
        private void Test(string expectedResult, object[] items, bool resetCursor)
        {
            Test(expectedResult, items, resetCursor: resetCursor, prompt: null, mustDing: false);
        }

        [ExcludeFromCodeCoverage]
        private void Test(string expectedResult, object[] items, bool resetCursor, string prompt, bool mustDing)
        {
            if (resetCursor)
            {
                _console.Clear();
            }
            SetPrompt(prompt);

            _console.Init(items);

            var result = PSConsoleReadLine.ReadLine(null, null);

            if (_console.validationFailure != null)
            {
                throw new Exception("", _console.validationFailure);
            }

            while (_console.index < _console.inputOrValidateItems.Length)
            {
                var item = _console.inputOrValidateItems[_console.index++];
                ((Action)item)();
            }

            Assert.AreEqual(expectedResult, result);

            if (mustDing)
            {
                Assert.IsTrue(_mockedMethods.didDing);
            }
        }

        private void TestMustDing(string expectedResult, object[] items)
        {
            Test(expectedResult, items, resetCursor: true, prompt: null, mustDing: true);
        }

        private TestConsole _console;
        private MockedMethods _mockedMethods;

        private static string MakeCombinedColor(ConsoleColor fg, ConsoleColor bg)
            => VTColorUtils.AsEscapeSequence(fg) + VTColorUtils.AsEscapeSequence(bg, isBackground: true);

        private void TestSetup(KeyMode keyMode, params KeyHandler[] keyHandlers)
        {
            _console = new TestConsole();
            _mockedMethods = new MockedMethods();
            var instance = (PSConsoleReadLine)typeof(PSConsoleReadLine)
                .GetField("_singleton", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            typeof(PSConsoleReadLine)
                .GetField("_mockableMethods", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(instance, _mockedMethods);
            typeof(PSConsoleReadLine)
                .GetField("_console", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(instance, _console);

            PSConsoleReadLine.ClearHistory();
            PSConsoleReadLine.ClearKillRing();

            var options = new SetPSReadlineOption
            {
                AddToHistoryHandler               = null,
                BellStyle                         = PSConsoleReadlineOptions.DefaultBellStyle,
                CompletionQueryItems              = PSConsoleReadlineOptions.DefaultCompletionQueryItems,
                ContinuationPrompt                = PSConsoleReadlineOptions.DefaultContinuationPrompt,
                ContinuationPromptColor           = MakeCombinedColor(_console.ForegroundColor, _console.BackgroundColor),
                DingDuration                      = 1,  // Make tests virtually silent when they ding
                DingTone                          = 37, // Make tests virtually silent when they ding
                EmphasisColor                     = MakeCombinedColor(PSConsoleReadlineOptions.DefaultEmphasisColor, _console.BackgroundColor),
                ErrorColor                        = MakeCombinedColor(ConsoleColor.Red, ConsoleColor.DarkRed),
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
                PromptText                        = "",
            };

            switch (keyMode)
            {
            case KeyMode.Cmd:
                options.EditMode = EditMode.Windows;
                break;
            case KeyMode.Emacs:
                options.EditMode = EditMode.Emacs;
                break;
            case KeyMode.Vi:
                options.EditMode = EditMode.Vi;
                break;
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
                colorOptions.Color = MakeCombinedColor(Colors[(int) val], BackgroundColors[(int) val]);

                PSConsoleReadLine.SetOptions(colorOptions);
            }
        }

    }
}
