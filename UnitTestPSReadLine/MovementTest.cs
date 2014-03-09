using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestEndOfLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys( _.End, CheckThat(() => AssertCursorLeftIs(0)) ));

            var buffer = new string(' ', Console.BufferWidth);
            Test(buffer, Keys(
                buffer,
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.End,
                CheckThat(() => AssertCursorLeftTopIs(0, 1))
                ));

            buffer = new string(' ', Console.BufferWidth + 5);
            Test(buffer, Keys(
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

            Test("abcde", Keys(
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
        public void TestGotoBrace()
        {
            TestSetup(KeyMode.Cmd);

            // Test empty input
            Test("", Keys(_.CtrlRBracket));

            Test("(11)", Keys("(11)", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("$a[11]", Keys("$a[11]", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(2))));
            Test("{11}", Keys("{11}", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("(11)", Keys("(11)", _.Home, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(3))));
            Test("$a[11]", Keys("$a[11]", _.Home, _.RightArrow, _.RightArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(5))));
            Test("{11}", Keys("{11}", _.Home, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(3))));

            // Test multiples, make sure we go to the right one.
            Test("((11))", Keys("((11))", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("((11))", Keys("((11))", _.LeftArrow, _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(1))));

            // Make sure we don't match inside a string
            TestMustDing("", Keys(
                "'()'", _.LeftArrow, _.LeftArrow,
                _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(2)),
                _.CtrlC, InputAcceptedNow));

            foreach (var c in new[] {'(', ')', '{', '}', '[', ']'})
            {
                TestMustDing("", Keys(
                    'a', c, _.LeftArrow,
                    _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(1)),
                    _.CtrlC, InputAcceptedNow));
            }
        }

        [TestMethod]
        public void TestCharacterSearch()
        {
            TestSetup(KeyMode.Cmd);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3", _.Home,
                _.F3, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.F3, '|', CheckThat(() => AssertCursorLeftIs(12))));

            TestSetup(KeyMode.Emacs);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3", _.Home,
                _.CtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.CtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.End,
                _.AltMinus, _.Alt2, _.CtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.Home,
                _.Alt2, _.CtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(12))));

            TestMustDing("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.CtrlRBracket, 'z'));

            int i = 0;
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z",
                (key, count) => PSConsoleReadLine.CharacterSearch(null, i++ == 0 ? (object)'|' : "|")));

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.Home,
                _.CtrlZ, CheckThat(() => AssertCursorLeftIs(5)),
                _.CtrlZ, CheckThat(() => AssertCursorLeftIs(12))));
        }

        [TestMethod]
        public void TestCharacterSearchBackward()
        {
            TestSetup(KeyMode.Cmd);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.ShiftF3, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.ShiftF3, '|', CheckThat(() => AssertCursorLeftIs(5))));

            TestSetup(KeyMode.Emacs);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.AltCtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.AltCtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.Home,
                _.AltMinus, _.Alt2, _.AltCtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.End,
                _.Alt2, _.AltCtrlRBracket, '|', CheckThat(() => AssertCursorLeftIs(5))));

            TestMustDing("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.AltCtrlRBracket, 'z'));

            int i = 0;
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z",
                (key, count) => PSConsoleReadLine.CharacterSearchBackward(null, i++ == 0 ? (object)'|' : "|")));

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.CtrlZ, CheckThat(() => AssertCursorLeftIs(12)),
                _.CtrlZ, CheckThat(() => AssertCursorLeftIs(5))));
        }
    }
}
