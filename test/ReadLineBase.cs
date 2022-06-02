using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Microsoft.PowerShell;
using Microsoft.PowerShell.PSReadLine;
using Test;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestPSReadLine;

public abstract class ReadLineBase
{
    internal static readonly ConsoleColor[] Colors =
    {
        /*None*/ ConsoleColor.DarkRed,
        /*Comment*/ ConsoleColor.Blue,
        /*Keyword*/ ConsoleColor.Cyan,
        /*String*/ ConsoleColor.Gray,
        /*Operator*/ ConsoleColor.Green,
        /*Variable*/ ConsoleColor.Magenta,
        /*Command*/ ConsoleColor.Red,
        /*Parameter*/ ConsoleColor.White,
        /*Type*/ ConsoleColor.Yellow,
        /*Number*/ ConsoleColor.DarkBlue,
        /*Member*/ ConsoleColor.DarkMagenta,
        /*Selection*/ ConsoleColor.Black,
        /*InlinePrediction*/ ConsoleColor.DarkGreen,
        /*ListPrediction*/ ConsoleColor.Yellow,
        /*ListPredictionSelected*/ ConsoleColor.Gray
    };

    internal static readonly ConsoleColor[] BackgroundColors =
    {
        /*None*/ ConsoleColor.DarkGray,
        /*Comment*/ ConsoleColor.DarkBlue,
        /*Keyword*/ ConsoleColor.DarkCyan,
        /*String*/ ConsoleColor.DarkGray,
        /*Operator*/ ConsoleColor.DarkGreen,
        /*Variable*/ ConsoleColor.DarkMagenta,
        /*Command*/ ConsoleColor.DarkRed,
        /*Parameter*/ ConsoleColor.DarkYellow,
        /*Type*/ ConsoleColor.Black,
        /*Number*/ ConsoleColor.Gray,
        /*Member*/ ConsoleColor.Yellow,
        /*Selection*/ ConsoleColor.Gray,
        /*InlinePrediction*/ ConsoleColor.Cyan,
        /*ListPrediction*/ ConsoleColor.Red,
        /*ListPredictionSelected*/ ConsoleColor.DarkBlue
    };

    private static readonly MethodInfo ClipboardGetTextMethod =
        typeof(PSConsoleReadLine).Assembly.GetType("Microsoft.PowerShell.Internal.Clipboard")
            .GetMethod("GetText", BindingFlags.Static | BindingFlags.Public);

    private static readonly MethodInfo ClipboardSetTextMethod =
        typeof(PSConsoleReadLine).Assembly.GetType("Microsoft.PowerShell.Internal.Clipboard")
            .GetMethod("SetText", BindingFlags.Static | BindingFlags.Public);

    protected static readonly NextLineToken NextLine = new();
    protected static readonly KeyPlaceholder InputAcceptedNow = new();
    protected TestConsole _console;
    protected string _emptyLine;
    protected MockedMethods _mockedMethods;

    static ReadLineBase()
    {
        var iss = InitialSessionState.CreateDefault();
        var rs = RunspaceFactory.CreateRunspace(iss);
        rs.Open();
        Runspace.DefaultRunspace = rs;
    }

    public ReadLineBase(ConsoleFixture fixture, ITestOutputHelper output, string lang, string os)
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

    public void SetHistory(params string[] historyItems)
    {
        History.ClearHistory();
        foreach (var item in historyItems) History.AddToHistory(item);
    }

    protected void AssertCursorLeftTopIs(int left, int top)
    {
        AssertCursorLeftIs(left);
        AssertCursorTopIs(top);
    }

    protected void AssertCursorLeftIs(int expected)
    {
        Assert.Equal(expected, _console.CursorLeft);
    }

    protected void AssertCursorTopIs(int expected)
    {
        Assert.Equal("AssertCursorTopIs " + expected, "AssertCursorTopIs " + _console.CursorTop);
    }

    protected void AssertLineIs(string expected)
    {
        PSConsoleReadLine.GetBufferState(out var input, out var unused);
        Assert.Equal("AssertLineIs " + expected, "AssertLineIs " + input);
    }

    protected static string GetClipboardText()
    {
        return (string) ClipboardGetTextMethod.Invoke(null, null);
    }

    protected static void SetClipboardText(string text)
    {
        ClipboardSetTextMethod.Invoke(null, new[] {text});
    }

    protected void AssertClipboardTextIs(string text)
    {
        var fromClipboard = GetClipboardText();
        Assert.Equal(text, fromClipboard);
    }

    protected void AssertScreenCaptureClipboardIs(params string[] lines)
    {
        var fromClipboard = GetClipboardText();
        var newline = Environment.NewLine;
        var text = string.Join(Environment.NewLine, lines);
        if (!text.EndsWith(newline)) text = text + newline;
        Assert.Equal(text, fromClipboard);
    }

    protected static SelectionToken Selected(string s)
    {
        return new SelectionToken {_text = s};
    }

    protected CHAR_INFO[] CreateCharInfoBuffer(int lines, params object[] items)
    {
        var result = new List<CHAR_INFO>();
        var fg = _console.ForegroundColor;
        var bg = _console.BackgroundColor;

        var isLastItemNextLineToken = false;

        foreach (var i in items)
        {
            var item = i;
            if (item is not NextLineToken) isLastItemNextLineToken = false;

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
                fg = _console.ForegroundColor;
                bg = _console.BackgroundColor;

                var localCopy = isLastItemNextLineToken;
                isLastItemNextLineToken = true;

                if (localCopy)
                {
                    // So this is the 2nd (or 3rd, 4th, and etc.) 'NextLineToken' in a row,
                    // and that means an empty line is requested.
                    item = _emptyLine;
                }
                else
                {
                    var lineLen = result.Count % _console.BufferWidth;
                    if (lineLen == 0 && result.Count > 0)
                        // The existing content is right at the end of a physical line,
                        // so there is no need to pad.
                        continue;

                    // Padding is needed. Fall through to the string case.
                    item = new string(' ', _console.BufferWidth - lineLen);
                }
            }

            if (item is string str)
            {
                result.AddRange(str.Select(c => new CHAR_INFO(c, fg, bg)));
                continue;
            }

            if (item is TokenClassification)
            {
                fg = Colors[(int) (TokenClassification) item];
                bg = BackgroundColors[(int) (TokenClassification) item];
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

        var extraSpacesNeeded = lines * _console.BufferWidth - result.Count;
        if (extraSpacesNeeded > 0)
        {
            var space = new CHAR_INFO(' ', _console.ForegroundColor, _console.BackgroundColor);
            result.AddRange(Enumerable.Repeat(space, extraSpacesNeeded));
        }

        return result.ToArray();
    }

    public static Action CheckThat(Action action)
    {
        // Syntactic sugar - saves a couple parens when calling Keys
        return action;
    }

    protected object[] Keys(params object[] input)
    {
        var autoAddEnter = true;
        var list = new List<object>();
        foreach (var t in input)
        {
            if (t is IEnumerable enumerable && !(t is string))
            {
                foreach (var i in enumerable) NewAddSingleKeyToList(i, list);
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
            // Make sure we have Enter as the last key.
            for (var i = list.Count - 1; i >= 0; i--)
            {
                // Actions after the last key are fine, they'll
                // get called.
                if (list[i] is Action) continue;

                // We've skipped any actions at the end, this is
                // the last key.  If it's not Enter, add Enter at the
                // end for convenience.
                var consoleKeyInfo = (ConsoleKeyInfo) list[i];
                if (consoleKeyInfo.Key != ConsoleKey.Enter || consoleKeyInfo.Modifiers != 0) list.Add(_.Enter);

                break;
            }

        return list.ToArray();
    }

    protected void NewAddSingleKeyToList(object t, List<object> list)
    {
        switch (t)
        {
            case string s:
                foreach (var c in s) list.Add(_[c]);

                break;
            case ConsoleKeyInfo _:
                list.Add(t);
                break;
            case char _:
                list.Add(_[(char) t]);
                break;
            default:
                Assert.IsAssignableFrom<Action>(t);
                list.Add(t);
                break;
        }
    }

    protected void AssertScreenIs(int lines, params object[] items)
    {
        AssertScreenIs(0, lines, items);
    }

    protected void AssertScreenIs(int top, int lines, params object[] items)
    {
        var consoleBuffer = _console.ReadBufferLines(top, lines);

        var expectedBuffer = CreateCharInfoBuffer(lines, items);
        Assert.Equal(expectedBuffer.Length, consoleBuffer.Length);
        Assert.Equal(expectedBuffer.ShowContext(), consoleBuffer.ShowContext());
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

    protected void SetPrompt(string prompt)
    {
        var options = new SetPSReadLineOption {ExtraPromptLineCount = 0};
        if (string.IsNullOrEmpty(prompt))
        {
            options.PromptText = new[] {""};
            PSConsoleReadLine.SetOptions(options);
            return;
        }

        int i;
        for (i = prompt.Length - 1; i >= 0; i--)
            if (!char.IsWhiteSpace(prompt[i]))
                break;

        options.PromptText = new[] {prompt.Substring(i)};

        var lineCount = 1 + prompt.Count(c => c == '\n');
        if (lineCount > 1) options.ExtraPromptLineCount = lineCount - 1;
        PSConsoleReadLine.SetOptions(options);
        _console.Write(prompt);
    }

    [ExcludeFromCodeCoverage]
    protected void Test(string expectedResult, object[] items)
    {
        Test(expectedResult, items, true, null, false);
    }

    [ExcludeFromCodeCoverage]
    protected void Test(string expectedResult, object[] items, string prompt)
    {
        Test(expectedResult, items, true, prompt, false);
    }

    [ExcludeFromCodeCoverage]
    protected void Test(string expectedResult, object[] items, bool resetCursor)
    {
        Test(expectedResult, items, resetCursor, null, false);
    }

    [ExcludeFromCodeCoverage]
    protected void Test(string expectedResult, object[] items, bool resetCursor, string prompt, bool mustDing)
    {
        if (resetCursor) _console.Clear();
        SetPrompt(prompt);

        _console.Init(items);

        var result = PSConsoleReadLine.ReadLine(
            null,
            null,
            true);

        if (_console.validationFailure != null) throw new Exception("", _console.validationFailure);

        while (_console.index < _console.inputOrValidateItems.Length)
        {
            var item = _console.inputOrValidateItems[_console.index++];
            ((Action) item)();
        }

        Assert.Equal(expectedResult, result);

        if (mustDing) Assert.True(_mockedMethods.didDing);
    }

    protected void TestMustDing(string expectedResult, object[] items)
    {
        Test(expectedResult, items, true, null, true);
    }

    protected static string MakeCombinedColor(ConsoleColor fg, ConsoleColor bg)
    {
        return VTColorUtils.AsEscapeSequence(fg) + VTColorUtils.AsEscapeSequence(bg, true);
    }

    protected void TestSetup(KeyMode keyMode, params KeyHandler[] keyHandlers)
    {
        TestSetup(null, keyMode, keyHandlers);
    }

    protected void TestSetup(TestConsole console, KeyMode keyMode, params KeyHandler[] keyHandlers)
    {
        Skip.If(WindowsConsoleFixtureHelper.GetKeyboardLayout() != Fixture.Lang,
            $"Keyboard layout must be set to {Fixture.Lang}");

        _console = console ?? new TestConsole(_);
        _mockedMethods = new MockedMethods();
        var aRL = PSConsoleReadLine.Singleton;

        typeof(PSConsoleReadLine)
            .GetField("_mockableMethods", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(aRL, _mockedMethods);

        typeof(Renderer)
            .GetField("Console", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, _console);

        _emptyLine ??= new string(' ', _console.BufferWidth);

        History.ClearHistory();
        PSConsoleReadLine.ClearKillRing();

        var options = new SetPSReadLineOption
        {
            AddToHistoryHandler = PSConsoleReadLineOptions.DefaultAddToHistoryHandler,
            AnsiEscapeTimeout = 0,
            BellStyle = PSConsoleReadLineOptions.DefaultBellStyle,
            CompletionQueryItems = PSConsoleReadLineOptions.DefaultCompletionQueryItems,
            ContinuationPrompt = PSConsoleReadLineOptions.DefaultContinuationPrompt,
            DingDuration = 1, // Make tests virtually silent when they ding
            DingTone = 37, // Make tests virtually silent when they ding
            ExtraPromptLineCount = PSConsoleReadLineOptions.DefaultExtraPromptLineCount,
            HistoryNoDuplicates = PSConsoleReadLineOptions.DefaultHistoryNoDuplicates,
            HistorySaveStyle = HistorySaveStyle.SaveNothing,
            HistorySearchCaseSensitive = PSConsoleReadLineOptions.DefaultHistorySearchCaseSensitive,
            HistorySearchCursorMovesToEnd = PSConsoleReadLineOptions.DefaultHistorySearchCursorMovesToEnd,
            MaximumHistoryCount = PSConsoleReadLineOptions.DefaultMaximumHistoryCount,
            MaximumKillRingCount = PSConsoleReadLineOptions.DefaultMaximumKillRingCount,
            ShowToolTips = PSConsoleReadLineOptions.DefaultShowToolTips,
            WordDelimiters = PSConsoleReadLineOptions.DefaultWordDelimiters,
            PromptText = new[] {""},
            Colors = new Hashtable
            {
                {"ContinuationPrompt", MakeCombinedColor(_console.ForegroundColor, _console.BackgroundColor)},
                {
                    "Emphasis",
                    MakeCombinedColor(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor)
                },
                {"Error", MakeCombinedColor(ConsoleColor.Red, ConsoleColor.DarkRed)}
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
            PSConsoleReadLine.SetKeyHandler(new[] {keyHandler.Chord}, keyHandler.Handler, "", "");

        var tokenTypes = new[]
        {
            "Default", "Comment", "Keyword", "String", "Operator", "Variable",
            "Command", "Parameter", "Type", "Number", "Member", "Selection",
            "InlinePrediction", "ListPrediction", "ListPredictionSelected"
        };
        var colors = new Hashtable();
        for (var i = 0; i < tokenTypes.Length; i++)
            colors.Add(tokenTypes[i], MakeCombinedColor(Colors[i], BackgroundColors[i]));
        var colorOptions = new SetPSReadLineOption {Colors = colors};
        PSConsoleReadLine.SetOptions(colorOptions);
    }

    public class KeyHandler
    {
        public KeyHandler(string chord, Action<ConsoleKeyInfo?, object> handler)
        {
            Chord = chord;
            Handler = handler;
        }

        public string Chord { get; }
        public Action<ConsoleKeyInfo?, object> Handler { get; }
    }

    protected class NextLineToken
    {
    }

    protected class SelectionToken
    {
        public string _text;
    }

    protected class KeyPlaceholder
    {
    }
}