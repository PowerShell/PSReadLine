using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestInput()
        {
            TestSetup(KeyMode.Cmd);

            Test("exit", Keys(
                "exit",
                _.Enter,
                CheckThat(() => AssertCursorLeftIs(0))));
        }

        [TestMethod]
        public void TestRevertLine()
        {
            // Add one test for chords
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+X,Escape", PSConsoleReadLine.RevertLine));

            Test("ls", Keys("di", _.Escape, "ls"));
            Test("ls", Keys("di", _.CtrlX, _.Escape, "ls"));

            TestSetup(KeyMode.Emacs);
            Test("ls", Keys("di", _.Escape, _.R, "ls"));
            Test("ls", Keys("di", _.AltR, "ls"));
        }

        [TestMethod]
        public void TestCancelLine()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+C", PSConsoleReadLine.CancelLine));

            Test("", Keys("oops", _.CtrlC,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "oops",
                    Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), "^C")),
                InputAcceptedNow));

            Test("", Keys("exit", _.CtrlC,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Keyword, "exit",
                    Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), "^C")),
                InputAcceptedNow));

            // Test near/at/over buffer width input
            var width = Console.BufferWidth;
            var line = new string('a', width - 2);
            Test("", Keys(line, _.CtrlC,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, line,
                    Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), "^C")),
                InputAcceptedNow));
            line = new string('a', width - 1);
            Test("", Keys(line, _.CtrlC,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, line,
                    Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), "^C")),
                InputAcceptedNow));
            line = new string('a', width);
            Test("", Keys(line, _.CtrlC,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, line,
                    Tuple.Create(ConsoleColor.Red, Console.BackgroundColor), "^C")),
                InputAcceptedNow));
        }

        [TestMethod]
        public void TestForwardDeleteLine()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            Test("", Keys("", _.CtrlEnd));

            // at end of input - doesn't change anything
            Test("abc", Keys("abc", _.CtrlEnd));

            // More normal usage - actually delete stuff
            Test("a", Keys("abc", _.LeftArrow, _.LeftArrow, _.CtrlEnd));
        }

        [TestMethod]
        public void TestBackwardDeleteLine()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            Test("", Keys(_.CtrlHome));

            // at beginning of input - doesn't change anything
            Test("abc", Keys("abc", _.Home, _.CtrlHome));

            // More typical usage
            Test("c", Keys("abc", _.LeftArrow, _.CtrlHome));
            Test("", Keys("abc", _.CtrlHome));
        }

        [TestMethod]
        public void TestBackwardDeleteChar()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            Test("", Keys("", _.Backspace));

            // At end, delete all input
            Test("", Keys("a", _.Backspace));

            // At end, delete all input with extra backspaces
            Test("", Keys("a", _.Backspace, _.Backspace));

            // Delete first character
            Test("b", Keys("ab", _.LeftArrow, _.Backspace));

            // Delete first character with extra backspaces
            Test("b", Keys("ab", _.LeftArrow, _.Backspace, _.Backspace));

            // Delete middle character
            Test("ac", Keys("abc", _.LeftArrow, _.Backspace));
        }

        [TestMethod]
        public void TestDeleteChar()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing, but don't crash)
            Test("", Keys(_.Delete));

            // At end but input not empty (does nothing, but don't crash)
            Test("a", Keys('a', _.Delete));

            // Delete last character
            Test("a", Keys("ab", _.LeftArrow, _.Delete));

            // Delete first character
            Test("b", Keys("ab", _.Home, _.Delete));

            // Delete middle character
            Test("ac", Keys("abc", _.Home, _.RightArrow, _.Delete));
        }

        [TestMethod]
        public void TestDeleteCharOrExit()
        {
            TestSetup(KeyMode.Emacs);

            Test("exit", Keys(_.CtrlD, InputAcceptedNow));

            Test("foo", Keys("foo", _.CtrlD));
            Test("oo", Keys("foo", _.Home, _.CtrlD));
            Test("exit", Keys("foo", _.Home, Enumerable.Repeat(_.CtrlD, 4), InputAcceptedNow));
        }

        [TestMethod]
        public void TestAcceptAndGetNext()
        {
            TestSetup(KeyMode.Emacs);

            // No history
            Test("", Keys(_.CtrlO, InputAcceptedNow));

            // One item in history
            SetHistory("echo 1");
            Test("", Keys(_.CtrlO, InputAcceptedNow));

            // Two items in history, make sure after Ctrl+O, second history item
            // is recalled.
            SetHistory("echo 1", "echo 2");
            Test("echo 1", Keys(_.UpArrow, _.UpArrow, _.CtrlO, InputAcceptedNow));
            Test("echo 2", Keys(_.Enter));

            // Test that the current saved line is saved after AcceptAndGetNext
            SetHistory("echo 1", "echo 2");
            Test("echo 1", Keys("e", _.UpArrow, _.UpArrow, _.CtrlO, InputAcceptedNow));
            Test("e", Keys(_.DownArrow, _.DownArrow, _.Enter));

            // Test that we can edit after recalling the current line
            SetHistory("echo 1", "echo 2");
            Test("echo 1", Keys("e", _.UpArrow, _.UpArrow, _.CtrlO, InputAcceptedNow));
            Test("eee", Keys(_.DownArrow, _.DownArrow, "ee", _.Enter));
        }

        [TestMethod]
        public void TestAcceptAndGetNextWithHistorySearch()
        {
            TestSetup(KeyMode.Emacs,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            // Test that after AcceptAndGetNext, the previous search is not applied
            SetHistory("echo 1", "echo 2", "zzz");
            Test("echo 1", Keys("e", _.UpArrow, _.UpArrow, _.CtrlO, InputAcceptedNow));
            Test("zzz", Keys(_.DownArrow, _.Enter));
        }

        [TestMethod]
        public void TestAddLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("1\n2", Keys('1', _.ShiftEnter, '2'));
        }

        [TestMethod]
        public void TestIgnore()
        {
            TestSetup(KeyMode.Emacs);

            Test("ab", Keys("a", _.VolumeDown, _.VolumeMute, _.VolumeUp, "b"));
        }
    }
}
