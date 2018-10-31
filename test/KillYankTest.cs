using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.PowerShell;
using Xunit;
using Microsoft.PowerShell.Internal;

namespace Test
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class ReadLine
    {
        [Fact]
        public void KillWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo  defabc", Keys(
                _.AltD, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.AltD, // Kill 'abc'
                _.End, _.CtrlY)); // Yank 'abc' at end of line
        }

        [Fact]
        public void BackwardKillWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo defabc ", Keys(
                _.AltBackspace, // Test on empty line
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 3),
                _.AltBackspace,    // Kill 'abc '
                _.End, _.CtrlY));  // Yank 'abc ' at the end
        }

        [Fact]
        public void UnixWordRubout()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo abc ", Keys("echo abc a\\b\\c", _.CtrlW));
        }

        [Fact]
        public void KillRegion()
        {
            TestSetup(KeyMode.Emacs, new KeyHandler("Ctrl+z", PSConsoleReadLine.KillRegion));

            Test("echo foobar", Keys("bar", _.CtrlAt, "echo foo", _.CtrlZ, _.Home, _.CtrlY));
        }

        [Fact]
        public void YankPop()
        {
            TestSetup(KeyMode.Emacs);

            var killedText = new List<string>();

            Test("z", Keys(_.CtrlY, _.AltY, _.Z));

            // Fill the kill ring plus some extra.
            for (int i = 0; i < PSConsoleReadLineOptions.DefaultMaximumKillRingCount + 2; i++)
            {
                var c = (char)('a' + i);
                killedText.Add(c + "zz");
                Test("", Keys(c, "zz", _.CtrlU));
            }

            int killRingIndex = killedText.Count - 1;
            Test(killedText[killRingIndex], Keys(_.CtrlY));

            Test(killedText[killRingIndex] + killedText[killRingIndex],
                Keys(_.CtrlY, _.CtrlY));

            killRingIndex -= 1;
            Test(killedText[killRingIndex], Keys(_.CtrlY, _.AltY));

            // Test wrap around.  We need 1 yank and n-1 yankpop to wrap around once, plus enter.
            Test(killedText[killRingIndex],
                Keys(_.CtrlY, Enumerable.Repeat(_.AltY, PSConsoleReadLineOptions.DefaultMaximumKillRingCount)));

            // Make sure an empty kill doesn't end up in the kill ring
            Test("a", Keys("a", _.CtrlU, _.CtrlU, "b", _.CtrlU, _.CtrlY, _.AltY));

            // Make sure an empty kill doesn't end up in the kill ring after movement commands
            Test("abc def", Keys(
                "abc def",
                _.CtrlW, // remove 'def'
                _.CtrlA, _.CtrlU, // empty kill at beginning of line
                _.CtrlE, // back to end of line
                "ghi",
                _.CtrlW, // remove 'ghi'
                _.CtrlY, // yank 'ghi'
                _.AltY)); // replace busy with previous text in kill ring
        }

        [Fact]
        public void KillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            Test("", Keys("dir", _.CtrlA, _.CtrlK));
            Test("dir", Keys(_.CtrlY));

            // Kill partial line
            Test("dir ", Keys("dir foo", _.AltB, _.CtrlK));
            Test("foo", Keys(_.CtrlY));
        }

        [Fact]
        public void BackwardKillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            // Check killed text by yanking
            Test("ls", Keys("dir", _.CtrlU, "ls"));
            Test("dir", Keys(_.CtrlY));

            // Kill whole line with second key binding
            Test("def", Keys("abc", _.CtrlX, _.Backspace, "def"));
            Test("abc", Keys(_.CtrlY));

            // Kill partial line
            Test("foo", Keys("dir foo", _.AltB, _.CtrlU));
            Test("dir ", Keys(_.CtrlY));
        }

        [Fact]
        public void KillAppend()
        {
            TestSetup(KeyMode.Emacs);

            Test(" abcdir", Keys(
                " abcdir", _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.CtrlK, // Kill 'dir'
                _.CtrlU, // Kill append ' abc'
                _.CtrlY)); // Yank ' abcdir'

            // Test empty kill doesn't affect kill append
            Test("ab", Keys("ab", _.LeftArrow, _.CtrlK, _.CtrlK, _.CtrlU, _.CtrlY));

            // test empty kill then good kill
            Test("abc", Keys(
                "abc",
                _.CtrlK,
                _.CtrlU,
                _.CtrlY));

            // test undo/redo history after empty kill
            Test("abc def ghi", Keys(
                "abc def ghi",
                _.CtrlW, // remove ghi
                _.CtrlK, // empty kill
                _.CtrlW, // remove def
                _.CtrlUnderbar, // bring back def
                _.CtrlUnderbar)); // bring back ghi

            // startup edge condition
            TestSetup(KeyMode.Emacs);
            Test("abc", Keys(
                "abc",
                _.CtrlK,
                _.CtrlU,
                _.CtrlY));
        }

        [Fact]
        public void ShellKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+d", PSConsoleReadLine.ShellKillWord));

            Test("echo  defabc", Keys(
                _.AltD, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.AltD, // Kill 'abc'
                _.End, _.CtrlY)); // Yank 'abc' at end of line

            Test("echo foo", Keys("'a b c'echo foo", _.Home, _.AltD));
        }

        [Fact]
        public void ShellBackwardKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+Backspace", PSConsoleReadLine.ShellBackwardKillWord));

            Test("echo defabc ", Keys(
                _.AltBackspace, // Test on empty line
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 3),
                _.AltBackspace,    // Kill 'abc '
                _.End, _.CtrlY));  // Yank 'abc ' at the end

            Test("echo foo ", Keys("echo foo 'a b c'", _.AltBackspace));
        }

        [Fact]
        public void ExchangePointAndMark()
        {
            TestSetup(KeyMode.Emacs,
                      new KeyHandler("Ctrl+z", PSConsoleReadLine.ExchangePointAndMark));

            var exchangePointAndMark = _.CtrlZ;
            var setMark = _.CtrlAt;

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

        [Fact]
        public void YankLastNth()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory();
            TestMustDing("", Keys(_.CtrlAltY));

            SetHistory("echo a b c");
            TestMustDing("", Keys(_.Alt9, _.CtrlAltY));

            SetHistory("echo a b c");
            TestMustDing("", Keys(_.AltMinus, _.Alt9, _.CtrlAltY));

            // Test no argument, gets the first argument (not the command).
            SetHistory("echo aa bb");
            Test("aa", Keys(_.CtrlAltY));

            // Test various arguments:
            //   * 0 - is the command
            //   * 2 - the second argument
            //   * -1 - the last argument
            //   * -2 - the second to last argument
            SetHistory("echo aa bb cc 'zz zz $(1 2 3)'");
            Test("echo bb 'zz zz $(1 2 3)' cc", Keys(
                _.Alt0, _.CtrlAltY, ' ',
                _.Alt2, _.CtrlAltY, ' ',
                _.AltMinus, _.Escape, _.CtrlY, ' ',
                _.AltMinus, _.Alt2, _.CtrlAltY));
        }

        [Fact]
        public void YankLastArg()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory();
            TestMustDing("", Keys(_.AltPeriod));

            SetHistory("echo def");
            Test("def", Keys(_.AltPeriod));

            SetHistory("echo abc", "echo def");
            Test("abc", Keys(_.AltPeriod, _.AltPeriod));

            SetHistory("echo aa bb cc 'zz zz $(1 2 3)'");
            Test("echo bb 'zz zz $(1 2 3)' cc", Keys(
                _.Alt0, _.AltPeriod, ' ',
                _.Alt2, _.AltPeriod, ' ',
                _.AltMinus, _.AltPeriod, ' ',
                _.AltMinus, _.Alt2, _.AltPeriod));

            SetHistory("echo a", "echo b");
            TestMustDing("a", Keys(
                _.AltPeriod, _.AltPeriod, _.AltPeriod));

            SetHistory("echo a", "echo b");
            TestMustDing("b", Keys(
                _.AltPeriod, _.AltPeriod, _.AltMinus, _.AltPeriod, _.AltPeriod));

            // Somewhat silly test to make sure invalid args are handled reasonably.
            TestSetup(KeyMode.Emacs, new[] {new KeyHandler("Ctrl+z", (key,arg) => PSConsoleReadLine.YankLastArg(null, "zz"))});
            SetHistory("echo a", "echo a");
            TestMustDing("", Keys(_.CtrlZ));
            TestMustDing("a", Keys(_.AltPeriod, _.CtrlZ));
        }

        [Fact]
        public void Paste()
        {
            TestSetup(KeyMode.Cmd);

            Clipboard.SetText("pastetest1");
            Test("pastetest1", Keys(_.CtrlV));

            Clipboard.SetText("pastetest2");
            Test("echo pastetest2", Keys(
                "echo foobar", _.CtrlShiftLeftArrow, _.CtrlV));
        }

        [Fact]
        public void PasteLarge()
        {
            TestSetup(KeyMode.Cmd);

            StringBuilder text = new StringBuilder();
            text.Append("@{");
            for (int i = 0; i < _console.BufferHeight + 10; i++)
            {
                text.Append(string.Format("prop{0}={0}", i));
            }
            text.Append("}");

            Clipboard.SetText(text.ToString());
            Test(text.ToString(), Keys(_.CtrlV));
        }

        [Fact]
        public void Cut()
        {
            TestSetup(KeyMode.Cmd);

            Clipboard.SetText("");
            Test("", Keys(
                "cuttest1", _.CtrlShiftLeftArrow, _.CtrlX));
            AssertClipboardTextIs("cuttest1");
        }

        [Fact]
        public void Copy()
        {
            TestSetup(KeyMode.Cmd);

            Clipboard.SetText("");
            Test("copytest1", Keys(
                "copytest1", _.CtrlShiftC));
            AssertClipboardTextIs("copytest1");

            Clipboard.SetText("");
            Test("echo copytest2", Keys(
                "echo copytest2", _.CtrlShiftLeftArrow, _.CtrlShiftC));
            AssertClipboardTextIs("copytest2");
        }

        [Fact]
        public void CopyOrCancelLine()
        {
            TestSetup(KeyMode.Cmd);

            Clipboard.SetText("");
            Test("echo copytest2", Keys(
                "echo copytest2", _.CtrlShiftLeftArrow, _.CtrlC));
            AssertClipboardTextIs("copytest2");

            Test("", Keys("oops", _.CtrlC,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "oops",
                    Tuple.Create(ConsoleColor.Red, _console.BackgroundColor), "^C")),
                InputAcceptedNow));
        }

        [Fact]
        public void SelectBackwardChar()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "eczz", _.ShiftLeftArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ecz", Selected("z"))),
                _.ShiftLeftArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ec", Selected("zz"))),
                _.Delete, "ho"));
        }

        [Fact]
        public void SelectForwardChar()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "zzho", _.Home, _.ShiftRightArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, Selected("z"), "zho")),
                _.ShiftRightArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, Selected("zz"), "ho")),
                "ec"));
        }

        [Fact]
        public void SelectBackwardWord()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo bar", Keys(
                "echo foo bar", _.CtrlLeftArrow, _.CtrlShiftLeftArrow,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    Selected("foo "), "bar")),
                _.Delete));
        }

        [Fact]
        public void SelectNextWord()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo bar", Keys(
                "foo echo bar", _.Home, _.CtrlShiftRightArrow,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("foo"),
                    TokenClassification.None, Selected(" "),
                    TokenClassification.None, "echo bar")),
                _.Delete));
        }

        [Fact]
        public void SelectForwardWord()
        {
            TestSetup(KeyMode.Emacs);

            Test(" echo bar", Keys(
                "foo echo bar", _.Home, _.AltShiftF,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("foo"),
                    TokenClassification.None, " echo bar")),
                _.Delete));
        }

        [Fact]
        public void SelectShellForwardWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z", PSConsoleReadLine.SelectShellForwardWord));

            Test(" echo bar", Keys(
                "a\\b\\c echo bar", _.Home, _.CtrlZ,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("a\\b\\c"),
                    TokenClassification.None, " echo bar")),
                _.Delete));
        }

        [Fact]
        public void SelectShellNextWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z", PSConsoleReadLine.SelectShellNextWord));

            Test("echo bar", Keys(
                "a\\b\\c echo bar", _.Home, _.CtrlZ,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("a\\b\\c "),
                    TokenClassification.None, "echo bar")),
                _.Delete));
        }

        [Fact]
        public void SelectShellBackwardWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z", PSConsoleReadLine.SelectShellBackwardWord));

            Test("echo bar ", Keys(
                "echo bar 'a b c'", _.CtrlZ,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " bar ",
                    TokenClassification.String, Selected("'a b c'"))),
                _.Delete));
        }

        [Fact]
        public void SelectBackwardsLine()
        {
            TestSetup(KeyMode.Emacs);

            Test("", Keys(
                "echo foo", _.ShiftHome,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                _.Backspace));
        }

        [Fact]
        public void SelectLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "echo foo", _.Home, _.ShiftEnd,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                _.Delete));
        }

        [Fact]
        public void SelectAll()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "echo foo", _.CtrlA,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                _.Delete
                ));

            Test("", Keys(
                "echo foo", _.CtrlLeftArrow, _.CtrlA,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Selected("echo foo"))),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Delete
                ));
        }
    }
}
