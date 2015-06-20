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
        public void TestMultilineCursorMovement()
        {
            TestSetup(KeyMode.Cmd);

            var continutationPromptLength = PSConsoleReadlineOptions.DefaultContinuationPrompt.Length;
            Test("", Keys(
                "4444", _.ShiftEnter,
                "666666", _.ShiftEnter,
                "88888888", _.ShiftEnter,
                "666666", _.ShiftEnter,
                "4444", _.ShiftEnter,

                // Starting at the end of the next to last line (because it's not blank)
                // Verify that Home first goes to the start of the line, then the start of the input.
                _.LeftArrow,
                _.Home, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 4)),
                _.Home, CheckThat(() => AssertCursorLeftTopIs(0, 0)),

                // Now (because we're at the start), verify first End goes to end of the line
                // and the second End goes to the end of the input.
                _.End, CheckThat(() => AssertCursorLeftTopIs(4, 0)),
                _.End, CheckThat(() => AssertCursorLeftTopIs(0 + continutationPromptLength, 5)),

                _.Home, _.Home,
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 2)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 3)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 4)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 5)),
                _.LeftArrow, _.Home,
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 3)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 2)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(4, 0)),

                // Make sure that movement between lines stays at the end of a line if it starts
                // at the end of a line
                _.End, _.End,
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(4 + continutationPromptLength, 4)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(6 + continutationPromptLength, 3)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(8 + continutationPromptLength, 2)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(6 + continutationPromptLength, 1)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(4, 0)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(6 + continutationPromptLength, 1)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(8 + continutationPromptLength, 2)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(6 + continutationPromptLength, 3)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(4 + continutationPromptLength, 4)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(0 + continutationPromptLength, 5)),

                _.Escape,
                _.ShiftEnter,
                "88888888", _.ShiftEnter,
                "55555", _.ShiftEnter,
                "22", _.ShiftEnter,
                "55555", _.ShiftEnter,
                "88888888",
                _.LeftArrow, _.LeftArrow,
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(5 + continutationPromptLength, 4)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(2 + continutationPromptLength, 3)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(5 + continutationPromptLength, 2)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(6 + continutationPromptLength, 1)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(6 + continutationPromptLength, 1)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(5 + continutationPromptLength, 2)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(2 + continutationPromptLength, 3)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(5 + continutationPromptLength, 4)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(6 + continutationPromptLength, 5)),

                // Clear the input, we were just testing movement
                _.Escape
                ));
        }

        [TestMethod]
        public void TestCursorMovement()
        {
            TestSetup(KeyMode.Cmd);

            Test("abcde", Keys(
                // Left arrow at start of line.
                _.LeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                "ace",
                _.LeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                'd',
                _.LeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.LeftArrow, CheckThat(() => AssertCursorLeftIs(1)),
                'b'
                ));

            // Test with digit arguments
            var input = "0123456789";
            Test(input, Keys(
                _.Alt9, _.LeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                _.Alt9, _.RightArrow, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Alt5, _.LeftArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.AltMinus, _.Alt2, _.LeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.Alt2, _.RightArrow, CheckThat(() => AssertCursorLeftIs(9)),
                _.AltMinus, _.Alt7, _.RightArrow, CheckThat(() => AssertCursorLeftIs(2))));
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
