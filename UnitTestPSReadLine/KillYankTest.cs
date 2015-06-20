using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestKillWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo  defabc", Keys(
                _.AltD, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.AltD, // Kill 'abc'
                _.End, _.CtrlY)); // Yank 'abc' at end of line
        }

        [TestMethod]
        public void TestBackwardKillWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo defabc ", Keys(
                _.AltBackspace, // Test on empty line
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 3),
                _.AltBackspace,    // Kill 'abc '
                _.End, _.CtrlY));  // Yank 'abc ' at the end
        }

        [TestMethod]
        public void TestUnixWordRubout()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo abc ", Keys("echo abc a\\b\\c", _.CtrlW));
        }

        [TestMethod]
        public void TestKillRegion()
        {
            TestSetup(KeyMode.Emacs, new KeyHandler("Ctrl+Z", PSConsoleReadLine.KillRegion));

            Test("echo foobar", Keys("bar", _.CtrlAt, "echo foo", _.CtrlZ, _.Home, _.CtrlY));
        }

        [TestMethod]
        public void TestYankPop()
        {
            TestSetup(KeyMode.Emacs);

            var killedText = new List<string>();

            Test("z", Keys(_.CtrlY, _.AltY, _.Z));

            // Fill the kill ring plus some extra.
            for (int i = 0; i < PSConsoleReadlineOptions.DefaultMaximumKillRingCount + 2; i++)
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
                Keys(_.CtrlY, Enumerable.Repeat(_.AltY, PSConsoleReadlineOptions.DefaultMaximumKillRingCount)));

            // Make sure an empty kill doesn't end up in the kill ring
            Test("a", Keys("a", _.CtrlU, _.CtrlU, "b", _.CtrlU, _.CtrlY, _.AltY));
        }

        [TestMethod]
        public void TestKillLine()
        {
            TestSetup(KeyMode.Emacs);

            // Kill whole line
            Test("", Keys("dir", _.CtrlA, _.CtrlK));
            Test("dir", Keys(_.CtrlY));

            // Kill partial line
            Test("dir ", Keys("dir foo", _.AltB, _.CtrlK));
            Test("foo", Keys(_.CtrlY));
        }

        [TestMethod]
        public void TestBackwardKillLine()
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

        [TestMethod]
        public void TestKillAppend()
        {
            TestSetup(KeyMode.Emacs);

            Test(" abcdir", Keys(
                " abcdir", _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.CtrlK, // Kill 'dir'
                _.CtrlU, // Kill append ' abc'
                _.CtrlY)); // Yank ' abcdir'

            // Test empty kill doesn't affect kill append
            Test("ab", Keys("ab", _.LeftArrow, _.CtrlK, _.CtrlK, _.CtrlU, _.CtrlY));
        }

        [TestMethod]
        public void TestShellKillWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+D", PSConsoleReadLine.ShellKillWord));

            Test("echo  defabc", Keys(
                _.AltD, // Test on empty input
                "echo abc def",
                Enumerable.Repeat(_.LeftArrow, 7),
                _.AltD, // Kill 'abc'
                _.End, _.CtrlY)); // Yank 'abc' at end of line

            Test("echo foo", Keys("'a b c'echo foo", _.Home, _.AltD));
        }

        [TestMethod]
        public void TestShellBackwardKillWord()
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

        [TestMethod]
        public void TestExchangePointAndMark()
        {
            TestSetup(KeyMode.Emacs,
                      new KeyHandler("Ctrl+Z", PSConsoleReadLine.ExchangePointAndMark));

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

        [TestMethod]
        public void TestYankLastNth()
        {
            TestSetup(KeyMode.Emacs);

            SetHistory();
            TestMustDing("", Keys(_.AltCtrlY));

            SetHistory("echo a b c");
            TestMustDing("", Keys(_.Alt9, _.AltCtrlY));

            SetHistory("echo a b c");
            TestMustDing("", Keys(_.AltMinus, _.Alt9, _.AltCtrlY));

            // Test no argument, gets the first argument (not the command).
            SetHistory("echo aa bb");
            Test("aa", Keys(_.AltCtrlY));

            // Test various arguments:
            //   * 0 - is the command
            //   * 2 - the second argument
            //   * -1 - the last argument
            //   * -2 - the second to last argument
            SetHistory("echo aa bb cc 'zz zz $(1 2 3)'");
            Test("echo bb 'zz zz $(1 2 3)' cc", Keys(
                _.Alt0, _.AltCtrlY, ' ',
                _.Alt2, _.AltCtrlY, ' ',
                _.AltMinus, _.Escape, _.CtrlY, ' ',
                _.AltMinus, _.Alt2, _.AltCtrlY));
        }

        [TestMethod]
        public void TestYankLastArg()
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
            TestSetup(KeyMode.Emacs, new[] {new KeyHandler("Ctrl+Z", (key,arg) => PSConsoleReadLine.YankLastArg(null, "zz"))});
            SetHistory("echo a", "echo a");
            TestMustDing("", Keys(_.CtrlZ)); 
            TestMustDing("a", Keys(_.AltPeriod, _.CtrlZ)); 
        }

        [TestMethod]
        public void TestPaste()
        {
            TestSetup(KeyMode.Cmd);

            ExecuteOnSTAThread(() => Clipboard.SetText("pastetest1"));
            Test("pastetest1", Keys(_.CtrlV));

            ExecuteOnSTAThread(() => Clipboard.SetText("pastetest2"));
            Test("echo pastetest2", Keys(
                "echo foobar", _.ShiftCtrlLeftArrow, _.CtrlV));
        }

        [TestMethod]
        public void TestCut()
        {
            TestSetup(KeyMode.Cmd);

            ExecuteOnSTAThread(() => Clipboard.SetText(""));
            Test("", Keys(
                "cuttest1", _.ShiftCtrlLeftArrow, _.CtrlX));
            AssertClipboardTextIs("cuttest1");
        }

        [TestMethod]
        public void TestCopy()
        {
            TestSetup(KeyMode.Cmd);

            ExecuteOnSTAThread(() => Clipboard.SetText(""));
            Test("copytest1", Keys(
                "copytest1", _.CtrlShiftC));
            AssertClipboardTextIs("copytest1");

            ExecuteOnSTAThread(() => Clipboard.SetText(""));
            Test("echo copytest2", Keys(
                "echo copytest2", _.ShiftCtrlLeftArrow, _.CtrlShiftC));
            AssertClipboardTextIs("copytest2");
        }

        [TestMethod]
        public void TestCopyOrCancelLine()
        {
            TestSetup(KeyMode.Cmd);

            ExecuteOnSTAThread(() => Clipboard.SetText(""));
            Test("echo copytest2", Keys(
                "echo copytest2", _.ShiftCtrlLeftArrow, _.CtrlC));
            AssertClipboardTextIs("copytest2");

            Test("", Keys("oops", _.CtrlC,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "oops",
                    Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), "^C")),
                InputAcceptedNow));
        }

        [TestMethod]
        public void TestSelectBackwardChar()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "eczz", _.ShiftLeftArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ecz", Inverted, "z", Inverted)),
                _.ShiftLeftArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ec", Inverted, "zz", Inverted)),
                _.Delete, "ho"));
        }

        [TestMethod]
        public void TestSelectForwardChar()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "zzho", _.Home, _.ShiftRightArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, Inverted, "z", Inverted, "zho")),
                _.ShiftRightArrow,
                CheckThat(() => AssertScreenIs(1, TokenClassification.Command, Inverted, "zz", Inverted, "ho")),
                "ec"));
        }

        [TestMethod]
        public void TestSelectBackwardWord()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo bar", Keys(
                "echo foo bar", _.CtrlLeftArrow, _.ShiftCtrlLeftArrow,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " ",
                    Inverted, "foo ", Inverted, "bar")),
                _.Delete));
        }

        [TestMethod]
        public void TestSelectNextWord()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo bar", Keys(
                "foo echo bar", _.Home, _.ShiftCtrlRightArrow,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "foo",
                    TokenClassification.None, Inverted, " ", Inverted,
                    TokenClassification.None, "echo bar")),
                _.Delete));
        }

        [TestMethod]
        public void TestSelectForwardWord()
        {
            TestSetup(KeyMode.Emacs);

            Test(" echo bar", Keys(
                "foo echo bar", _.Home, _.AltShiftF,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "foo", Inverted,
                    TokenClassification.None, " echo bar")),
                _.Delete));
        }

        [TestMethod]
        public void TestSelectShellForwardWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Z", PSConsoleReadLine.SelectShellForwardWord));

            Test(" echo bar", Keys(
                "a\\b\\c echo bar", _.Home, _.CtrlZ,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "a\\b\\c", Inverted,
                    TokenClassification.None, " echo bar")),
                _.Delete));
        }

        [TestMethod]
        public void TestSelectShellNextWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Z", PSConsoleReadLine.SelectShellNextWord));

            Test("echo bar", Keys(
                "a\\b\\c echo bar", _.Home, _.CtrlZ,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "a\\b\\c",
                    TokenClassification.None, Inverted, " ",
                    TokenClassification.None, "echo bar")),
                _.Delete));
        }

        [TestMethod]
        public void TestSelectShellBackwardWord()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Z", PSConsoleReadLine.SelectShellBackwardWord));

            Test("echo bar ", Keys(
                "echo bar 'a b c'", _.CtrlZ,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, " bar ",
                    TokenClassification.String, Inverted, "'a b c'")),
                _.Delete));
        }

        [TestMethod]
        public void TestSelectBackwardsLine()
        {
            TestSetup(KeyMode.Emacs);

            Test("", Keys(
                "echo foo", _.ShiftHome,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "echo",
                    TokenClassification.None, Inverted, " foo")),
                _.Backspace));
        }

        [TestMethod]
        public void TestSelectLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "echo foo", _.Home, _.ShiftEnd,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "echo",
                    TokenClassification.None, Inverted, " foo")),
                _.Delete));
        }

        [TestMethod]
        public void TestSelectAll()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "echo foo", _.CtrlA,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "echo",
                    TokenClassification.None, Inverted, " foo")),
                _.Delete
                ));

            Test("", Keys(
                "echo foo", _.CtrlLeftArrow, _.CtrlA,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, Inverted, "echo",
                    TokenClassification.None, Inverted, " foo")),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Delete
                ));
        }
    }
}
