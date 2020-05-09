using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PowerShell;
using Microsoft.PowerShell.Internal;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Test
{
    internal class MockedMethods : IPSConsoleReadLineMockableMethods
    {
        internal bool didDing;

        public void Ding()
        {
            didDing = true;
        }

        public CommandCompletion CompleteInput(string input, int cursorIndex, Hashtable options, PowerShell powershell)
        {
            return ReadLine.MockedCompleteInput(input, cursorIndex, options, powershell);
        }

        public bool RunspaceIsRemote(Runspace runspace)
        {
            return false;
        }
    }

    public enum TokenClassification
    {
        None,
        Comment,
        Keyword,
        String,
        Operator,
        Variable,
        Command,
        Parameter,
        Type,
        Number,
        Member,
        Selection,
        Prediction,
    }

    public abstract partial class ReadLine
    {
        protected ReadLine(ConsoleFixture fixture, ITestOutputHelper output, string lang, string os)
        {
            Output = output;
            Fixture = fixture;
            Fixture.Initialize(lang, os);
        }

        internal dynamic _ => Fixture.KbLayout;
        internal ConsoleFixture Fixture { get; }
        internal ITestOutputHelper Output { get; }

        internal virtual bool KeyboardHasLessThan => true;
        internal virtual bool KeyboardHasGreaterThan => true;
        internal virtual bool KeyboardHasCtrlRBracket => true;
        internal virtual bool KeyboardHasCtrlAt => true;

        static ReadLine()
        {
            var iss = InitialSessionState.CreateDefault();
            var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();
            Runspace.DefaultRunspace = rs;
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
        /*Selection*/ ConsoleColor.Black,
        /*Prediction*/ConsoleColor.DarkGreen,
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
        /*Selection*/ ConsoleColor.Gray,
        /*Prediction*/ConsoleColor.Cyan,
        };

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
            Assert.Equal(expected, _console.CursorLeft);
        }

        private void AssertCursorTopIs(int expected)
        {
            Assert.Equal(expected, _console.CursorTop);
        }

        private void AssertLineIs(string expected)
        {
            PSConsoleReadLine.GetBufferState(out var input, out var unused);
            Assert.Equal(expected, input);
        }

        static readonly MethodInfo ClipboardGetTextMethod =
            typeof(PSConsoleReadLine).Assembly.GetType("Microsoft.PowerShell.Internal.Clipboard")
                .GetMethod("GetText", BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo ClipboardSetTextMethod =
            typeof(PSConsoleReadLine).Assembly.GetType("Microsoft.PowerShell.Internal.Clipboard")
                .GetMethod("SetText", BindingFlags.Static | BindingFlags.Public);
        private static string GetClipboardText()
        {
            return (string)ClipboardGetTextMethod.Invoke(null, null);
        }

        private static void SetClipboardText(string text)
        {
            ClipboardSetTextMethod.Invoke(null, new[] { text });
        }

        private void AssertClipboardTextIs(string text)
        {
            var fromClipboard = GetClipboardText();
            Assert.Equal(text, fromClipboard);
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
            Assert.Equal(text, fromClipboard);
        }

        private class NextLineToken { }
        static readonly NextLineToken NextLine = new NextLineToken();

        private class SelectionToken { public string _text; }
        private static SelectionToken Selected(string s) { return new SelectionToken {_text = s}; }

        private CHAR_INFO[] CreateCharInfoBuffer(int lines, params object[] items)
        {
            var result = new List<CHAR_INFO>();
            var fg = _console.ForegroundColor;
            var bg = _console.BackgroundColor;

            foreach (var i in items)
            {
                var item = i;
                if (item is char c1)
                {
                    result.Add(new CHAR_INFO(c1, fg, bg));
                    continue;
                }
                if (item is SelectionToken st)
                {
                    result.AddRange(st._text.Select(c => new CHAR_INFO(c, ConsoleColor.Black, ConsoleColor.Gray)));
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
            // Syntactic sugar - saves a couple parens when calling Keys
            return action;
        }

        private class KeyPlaceholder {}
        private static readonly KeyPlaceholder InputAcceptedNow = new KeyPlaceholder();

        private object[] Keys(params object[] input)
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
                    var consoleKeyInfo = (ConsoleKeyInfo) list[i];
                    if (consoleKeyInfo.Key != ConsoleKey.Enter || consoleKeyInfo.Modifiers != 0) {
                        list.Add(_.Enter);
                    }

                    break;
                }
            }

            return list.ToArray();
        }

        private void NewAddSingleKeyToList(object t, List<object> list)
        {
            switch (t)
            {
                case string s:
                    foreach (var c in s)
                    {
                        list.Add(_[c]);
                    }

                    break;
                case ConsoleKeyInfo _:
                    list.Add(t);
                    break;
                case char _:
                    list.Add(_[(char)t]);
                    break;
                default:
                    Assert.IsAssignableFrom<Action>(t);
                    list.Add(t);
                    break;
            }
        }

        private void AssertScreenIs(int lines, params object[] items)
        {
            var consoleBuffer = _console.ReadBufferLines(0, lines);

            var expectedBuffer = CreateCharInfoBuffer(lines, items);
            Assert.Equal(expectedBuffer.Length, consoleBuffer.Length);
            for (var i = 0; i < expectedBuffer.Length; i++)
            {
                // Comparing CHAR_INFO should work, but randomly some attributes are set
                // that shouldn't be and aren't ever set by any code in PSReadLine, so we'll
                // ignore those bits and just check the stuff we do set.
                Assert.Equal(expectedBuffer[i].UnicodeChar, consoleBuffer[i].UnicodeChar);
                Assert.Equal(expectedBuffer[i].ForegroundColor, consoleBuffer[i].ForegroundColor);
                Assert.Equal(expectedBuffer[i].BackgroundColor, consoleBuffer[i].BackgroundColor);
            }
        }

        private void SetPrompt(string prompt)
        {
            var options = new SetPSReadLineOption {ExtraPromptLineCount = 0};
            if (string.IsNullOrEmpty(prompt))
            {
                options.PromptText = new [] {""};
                PSConsoleReadLine.SetOptions(options);
                return;
            }

            int i;
            for (i = prompt.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(prompt[i])) break;
            }

            options.PromptText = new [] { prompt.Substring(i) };

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

            Assert.Equal(expectedResult, result);

            if (mustDing)
            {
                Assert.True(_mockedMethods.didDing);
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
            Skip.If(WindowsConsoleFixtureHelper.GetKeyboardLayout() != this.Fixture.Lang,
                    $"Keyboard layout must be set to {this.Fixture.Lang}");

            _console = new TestConsole(_);
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

            var options = new SetPSReadLineOption
            {
                AddToHistoryHandler               = PSConsoleReadLineOptions.DefaultAddToHistoryHandler,
                AnsiEscapeTimeout                 = 0,
                BellStyle                         = PSConsoleReadLineOptions.DefaultBellStyle,
                CompletionQueryItems              = PSConsoleReadLineOptions.DefaultCompletionQueryItems,
                ContinuationPrompt                = PSConsoleReadLineOptions.DefaultContinuationPrompt,
                DingDuration                      = 1,  // Make tests virtually silent when they ding
                DingTone                          = 37, // Make tests virtually silent when they ding
                ExtraPromptLineCount              = PSConsoleReadLineOptions.DefaultExtraPromptLineCount,
                HistoryNoDuplicates               = PSConsoleReadLineOptions.DefaultHistoryNoDuplicates,
                HistorySaveStyle                  = HistorySaveStyle.SaveNothing,
                HistorySearchCaseSensitive        = PSConsoleReadLineOptions.DefaultHistorySearchCaseSensitive,
                HistorySearchCursorMovesToEnd     = PSConsoleReadLineOptions.DefaultHistorySearchCursorMovesToEnd,
                MaximumHistoryCount               = PSConsoleReadLineOptions.DefaultMaximumHistoryCount,
                MaximumKillRingCount              = PSConsoleReadLineOptions.DefaultMaximumKillRingCount,
                ShowToolTips                      = PSConsoleReadLineOptions.DefaultShowToolTips,
                WordDelimiters                    = PSConsoleReadLineOptions.DefaultWordDelimiters,
                PromptText                        = new [] {""},
                PredictionSource                  = PredictionSource.History,
                Colors = new Hashtable {
                    { "ContinuationPrompt",       MakeCombinedColor(_console.ForegroundColor, _console.BackgroundColor) },
                    { "Emphasis",                 MakeCombinedColor(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor) },
                    { "Error",                    MakeCombinedColor(ConsoleColor.Red, ConsoleColor.DarkRed) },
                }
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

            var tokenTypes = new[]
            {
                "Default", "Comment", "Keyword", "String", "Operator", "Variable",
                "Command", "Parameter", "Type", "Number", "Member", "Selection", "Prediction"
            };
            var colors = new Hashtable();
            for (var i = 0; i < tokenTypes.Length; i++)
            {
                colors.Add(tokenTypes[i], MakeCombinedColor(Colors[i], BackgroundColors[i]));
            }
            var colorOptions = new SetPSReadLineOption {Colors = colors};
            PSConsoleReadLine.SetOptions(colorOptions);
        }
    }

    public class en_US_Windows : Test.ReadLine, IClassFixture<ConsoleFixture>
    {
        public en_US_Windows(ConsoleFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "en-US", "windows")
        {
        }
    }

    public class fr_FR_Windows : Test.ReadLine, IClassFixture<ConsoleFixture>
    {
        public fr_FR_Windows(ConsoleFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "fr-FR", "windows")
        {
        }

        // I don't think this is actually true for real French keyboard, but on my US keyboard,
        // I have to use Alt 6 0 for `<` and Alt 6 2 for `>` and that means the Alt+< and Alt+>
        // bindings can't work.
        internal override bool KeyboardHasLessThan => false;
        internal override bool KeyboardHasGreaterThan => false;

        // These are most likely an issue with .Net on Windows - AltGr turns into Ctrl+Alt and `]` or `@`
        // requires AltGr, so you can't tell the difference b/w `]` and `Ctrl+]`.
        internal override bool KeyboardHasCtrlRBracket => false;
        internal override bool KeyboardHasCtrlAt => false;
    }
}
