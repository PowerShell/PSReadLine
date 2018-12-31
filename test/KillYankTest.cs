using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void KillWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo  defabc", Keys(
                _.Alt_d, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.Alt_d, // Kill 'abc'
                _.End, _.Ctrl_y)); // Yank 'abc' at end of line
        }

        [SkippableFact]
        public void BackwardKillWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo defabc ", Keys(
                _.Alt_Backspace, // Test on empty line
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 3),
                _.Alt_Backspace,    // Kill 'abc '
                _.End, _.Ctrl_y));  // Yank 'abc ' at the end
        }

        [SkippableFact]
        public void UnixWordRubout()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo abc ", Keys("echo abc a\\b\\c", _.Ctrl_w));
        }

        [SkippableFact]
        public void KillRegion()
        {
            Skip.IfNot(KeyboardHasCtrlAt);

            TestSetup(KeyMode.Emacs, new KeyHandler("Ctrl+z", PSConsoleReadLine.KillRegion));

            Test("echo foobar", Keys("bar", _.Ctrl_At, "echo foo", _.Ctrl_z, _.Home, _.Ctrl_y));
        }

        [SkippableFact]
        public void YankPop()
        {
            TestSetup(KeyMode.Emacs);

            var killedText = new List<string>();

            Test("z", Keys(_.Ctrl_y, _.Alt_y, _.z));

            // Fill the kill ring plus some extra.
            for (int i = 0; i < PSConsoleReadLineOptions.DefaultMaximumKillRingCount + 2; i++)
            {
                var c = (char)('a' + i);
                killedText.Add(c + "zz");
                Test("", Keys(c, "zz", _.Ctrl_u));
            }

            int killRingIndex = killedText.Count - 1;
            Test(killedText[killRingIndex], Keys(_.Ctrl_y));

            Test(killedText[killRingIndex] + killedText[killRingIndex],
                Keys(_.Ctrl_y, _.Ctrl_y));

            killRingIndex -= 1;
            Test(killedText[killRingIndex], Keys(_.Ctrl_y, _.Alt_y));

            // Test wrap around.  We need 1 yank and n-1 yankpop to wrap around once, plus enter.
            Test(killedText[killRingIndex],
                Keys(_.Ctrl_y, Enumerable.Repeat(_.Alt_y, PSConsoleReadLineOptions.DefaultMaximumKillRingCount)));

            // Make sure an empty kill doesn't end up in the kill ring
            Test("a", Keys("a", _.Ctrl_u, _.Ctrl_u, "b", _.Ctrl_u, _.Ctrl_y, _.Alt_y));

            // Make sure an empty kill doesn't end up in the kill ring after movement commands
            Test("abc def", Keys(
                "abc def",
                _.Ctrl_w, // remove 'def'
                _.Ctrl_a, _.Ctrl_u, // empty kill at beginning of line
                _.Ctrl_e, // back to end of line
                "ghi",
                _.Ctrl_w, // remove 'ghi'
                _.Ctrl_y, // yank 'ghi'
                _.Alt_y)); // replace busy with previous text in kill ring
        }

        [SkippableFact]
        public void KillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            Test("", Keys("dir", _.Ctrl_a, _.Ctrl_k));
            Test("dir", Keys(_.Ctrl_y));

            // Kill partial line
            Test("dir ", Keys("dir foo", _.Alt_b, _.Ctrl_k));
            Test("foo", Keys(_.Ctrl_y));
        }

        [SkippableFact]
        public void BackwardKillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            // Check killed text by yanking
            Test("ls", Keys("dir", _.Ctrl_u, "ls"));
            Test("dir", Keys(_.Ctrl_y));

            // Kill whole line with second key binding
            Test("def", Keys("abc", _.Ctrl_x, _.Backspace, "def"));
            Test("abc", Keys(_.Ctrl_y));

            // Kill partial line
            Test("foo", Keys("dir foo", _.Alt_b, _.Ctrl_u));
            Test("dir ", Keys(_.Ctrl_y));
        }

        [SkippableFact]
        public void KillAppend()
        {
            TestSetup(KeyMode.Emacs);

            Test(" abcdir", Keys(
                " abcdir", _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.Ctrl_k, // Kill 'dir'
                _.Ctrl_u, // Kill append ' abc'
                _.Ctrl_y)); // Yank ' abcdir'

            // Test empty kill doesn't affect kill append
            Test("ab", Keys("ab", _.LeftArrow, _.Ctrl_k, _.Ctrl_k, _.Ctrl_u, _.Ctrl_y));

            // test empty kill then good kill
            Test("abc", Keys(
                "abc",
                _.Ctrl_k,
                _.Ctrl_u,
                _.Ctrl_y));

            // test undo/redo history after empty kill
            Test("abc def ghi", Keys(
                "abc def ghi",
                _.Ctrl_w, // remove ghi
                _.Ctrl_k, // empty kill
                _.Ctrl_w, // remove def
                _.Ctrl_Underbar, // bring back def
                _.Ctrl_Underbar)); // bring back ghi

            // startup edge condition
            TestSetup(KeyMode.Emacs);
            Test("abc", Keys(
                "abc",
                _.Ctrl_k,
                _.Ctrl_u,
                _.Ctrl_y));
        }

        [SkippableFact]
        public void ShellKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+d", PSConsoleReadLine.ShellKillWord));

            Test("echo  defabc", Keys(
                _.Alt_d, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.Alt_d, // Kill 'abc'
                _.End, _.Ctrl_y)); // Yank 'abc' at end of line

            Test("echo foo", Keys("'a b c'echo foo", _.Home, _.Alt_d));
        }

        [SkippableFact]
        public void ShellBackwardKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+Backspace", PSConsoleReadLine.ShellBackwardKillWord));

            Test("echo defabc ", Keys(
                _.Alt_Backspace, // Test on empty line
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 3),
                _.Alt_Backspace,    // Kill 'abc '
                _.End, _.Ctrl_y));  // Yank 'abc ' at the end

            Test("echo foo ", Keys("echo foo 'a b c'", _.Alt_Backspace));
        }

        [SkippableFact]
        public void ExchangePointAndMark()
        {
            Skip.IfNot(KeyboardHasCtrlAt);

            TestSetup(KeyMode.Emacs,
                      new KeyHandler("Ctrl+z", PSConsoleReadLine.ExchangePointAndMark));

            var exchangePointAndMark = _.Ctrl_z;
            var setMark = _.Ctrl_At;

            Test("abcde", Keys(
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

            Test("abc", Keys(
                "abc",
                exchangePointAndMark,
                CheckThat(() => AssertCursorLeftIs(0))
                ));
        }

        [SkippableFact]
        public void YankLastNth()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory();
            TestMustDing("", Keys(_.Ctrl_Alt_y));

            SetHistory("echo a b c");
            TestMustDing("", Keys(_.Alt_9, _.Ctrl_Alt_y));

            SetHistory("echo a b c");
            TestMustDing("", Keys(_.Alt_Minus, _.Alt_9, _.Ctrl_Alt_y));

            // Test no argument, gets the first argument (not the command).
            SetHistory("echo aa bb");
            Test("aa", Keys(_.Ctrl_Alt_y));

            // Test various arguments:
            //   * 0 - is the command
            //   * 2 - the second argument
            //   * -1 - the last argument
            //   * -2 - the second to last argument
            SetHistory("echo aa bb cc 'zz zz $(1 2 3)'");
            Test("echo bb 'zz zz $(1 2 3)' cc", Keys(
                _.Alt_0, _.Ctrl_Alt_y, ' ',
                _.Alt_2, _.Ctrl_Alt_y, ' ',
                _.Alt_Minus, _.Escape, _.Ctrl_y, ' ',
                _.Alt_Minus, _.Alt_2, _.Ctrl_Alt_y));
        }

        [SkippableFact]
        public void YankLastArg()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory();
            TestMustDing("", Keys(_.Alt_Period));

            SetHistory("echo def");
            Test("def", Keys(_.Alt_Period));

            SetHistory("echo abc", "echo def");
            Test("abc", Keys(_.Alt_Period, _.Alt_Period));

            SetHistory("echo aa bb cc 'zz zz $(1 2 3)'");
            Test("echo bb 'zz zz $(1 2 3)' cc", Keys(
                _.Alt_0, _.Alt_Period, ' ',
                _.Alt_2, _.Alt_Period, ' ',
                _.Alt_Minus, _.Alt_Period, ' ',
                _.Alt_Minus, _.Alt_2, _.Alt_Period));

            SetHistory("echo a", "echo b");
            TestMustDing("a", Keys(
                _.Alt_Period, _.Alt_Period, _.Alt_Period));

            SetHistory("echo a", "echo b");
            TestMustDing("b", Keys(
                _.Alt_Period, _.Alt_Period, _.Alt_Minus, _.Alt_Period, _.Alt_Period));

            // Somewhat silly test to make sure invalid args are handled reasonably.
            TestSetup(KeyMode.Emacs, new[] {new KeyHandler("Ctrl+z", (key,arg) => PSConsoleReadLine.YankLastArg(null, "zz"))});
            SetHistory("echo a", "echo a");
            TestMustDing("", Keys(_.Ctrl_z));
            TestMustDing("a", Keys(_.Alt_Period, _.Ctrl_z));
        }

        [SkippableFact]
        public void Paste()
        {
            TestSetup(KeyMode.Cmd);

            SetClipboardText("pastetest1");
            Test("pastetest1", Keys(_.Ctrl_v));

            SetClipboardText("pastetest2");
            Test("echo pastetest2", Keys(
                "echo foobar", _.Ctrl_Shift_LeftArrow, _.Ctrl_v));
        }

        [SkippableFact]
        public void Cut()
        {
            TestSetup(KeyMode.Cmd);

            SetClipboardText("");
            Test("", Keys(
                "cuttest1", _.Ctrl_Shift_LeftArrow, _.Ctrl_x));
            AssertClipboardTextIs("cuttest1");
        }

        [SkippableFact]
        public void Copy()
        {
            TestSetup(KeyMode.Cmd);

            SetClipboardText("");
            Test("copytest1", Keys(
                "copytest1", _.Ctrl_C));
            AssertClipboardTextIs("copytest1");

            SetClipboardText("");
            Test("echo copytest2", Keys(
                "echo copytest2", _.Ctrl_Shift_LeftArrow, _.Ctrl_C));
            AssertClipboardTextIs("copytest2");
        }

        [SkippableFact]
        public void CopyOrCancelLine()
        {
            TestSetup(KeyMode.Cmd);

            SetClipboardText("");
            Test("echo copytest2", Keys(
                "echo copytest2", _.Ctrl_Shift_LeftArrow, _.Ctrl_c));
            AssertClipboardTextIs("copytest2");

            Test("", Keys("oops", _.Ctrl_c,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "oops",
                    Tuple.Create(ConsoleColor.Red, _console.BackgroundColor), "^C")),
                InputAcceptedNow));
        }

        [SkippableFact]
        public void SelectBackwardChar()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "eczz", _.Shift_LeftArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ecz", Selected("z"))),
                _.Shift_LeftArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ec", Selected("zz"))),
                _.Delete, "ho"));
        }

        [SkippableFact]
        public void SelectForwardChar()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "zzho", _.Home, _.Shift_RightArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, Selected("z"), "zho")),
                _.Shift_RightArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, Selected("zz"), "ho")),
                "ec"));
        }

        [SkippableFact]
        public void SelectBackwardWord()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo bar", Keys(
                "echo foo bar", _.Ctrl_LeftArrow, _.Ctrl_Shift_LeftArrow,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    Selected("foo "), "bar")),
                _.Delete));
        }

        [SkippableFact]
        public void SelectNextWord()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo bar", Keys(
                "foo echo bar", _.Home, _.Ctrl_Shift_RightArrow,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("foo"),
                    TokenClassification.None, Selected(" "),
                    TokenClassification.None, "echo bar")),
                _.Delete));
        }

        [SkippableFact]
        public void SelectForwardWord()
        {
            TestSetup(KeyMode.Emacs);

            Test(" echo bar", Keys(
                "foo echo bar", _.Home, _.Alt_F,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("foo"),
                    TokenClassification.None, " echo bar")),
                _.Delete));
        }

        [SkippableFact]
        public void SelectShellForwardWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z", PSConsoleReadLine.SelectShellForwardWord));

            Test(" echo bar", Keys(
                "a\\b\\c echo bar", _.Home, _.Ctrl_z,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("a\\b\\c"),
                    TokenClassification.None, " echo bar")),
                _.Delete));
        }

        [SkippableFact]
        public void SelectShellNextWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z", PSConsoleReadLine.SelectShellNextWord));

            Test("echo bar", Keys(
                "a\\b\\c echo bar", _.Home, _.Ctrl_z,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("a\\b\\c "),
                    TokenClassification.None, "echo bar")),
                _.Delete));
        }

        [SkippableFact]
        public void SelectShellBackwardWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z", PSConsoleReadLine.SelectShellBackwardWord));

            Test("echo bar ", Keys(
                "echo bar 'a b c'", _.Ctrl_z,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " bar ",
                    TokenClassification.String, Selected("'a b c'"))),
                _.Delete));
        }

        [SkippableFact]
        public void SelectBackwardsLine()
        {
            TestSetup(KeyMode.Emacs);

            Test("", Keys(
                "echo foo", _.Shift_Home,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                _.Backspace));
        }

        [SkippableFact]
        public void SelectLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "echo foo", _.Home, _.Shift_End,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                _.Delete));
        }

        [SkippableFact]
        public void SelectAll()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "echo foo", _.Ctrl_a,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                _.Delete
                ));

            Test("", Keys(
                "echo foo", _.Ctrl_LeftArrow, _.Ctrl_a,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Delete
                ));
        }
    }
}
