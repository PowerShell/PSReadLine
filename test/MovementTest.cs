using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void EndOfLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys( _.End, CheckThat(() => AssertCursorLeftIs(0)) ));

            var buffer = new string(' ', _console.BufferWidth);
            Test(buffer, Keys(
                buffer,
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.End,
                CheckThat(() => AssertCursorLeftTopIs(0, 1))
                ));

            buffer = new string(' ', _console.BufferWidth + 5);
            Test(buffer, Keys(
                buffer,
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.End,
                CheckThat(() => AssertCursorLeftTopIs(5, 1))
                ));
        }

        [SkippableFact]
        public void MultilineCursorMovement_WithWrappedLines()
        {
            TestSetup(KeyMode.Cmd);

            int continutationPromptLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;
            string line_0 = "4444";
            string line_1 = "33";
            string line_2 = "666666";
            string line_3 = "777";

            int wrappedLength_1 = 9;
            int wrappedLength_2 = 2;
            string wrappedLine_1 = new string('8', _console.BufferWidth - continutationPromptLength + wrappedLength_1); // Take 2 physical lines
            string wrappedLine_2 = new string('6', _console.BufferWidth - continutationPromptLength + wrappedLength_2); // Take 2 physical lines

            Test("", Keys(
                "",     _.Shift_Enter,        // physical line 0
                line_0, _.Shift_Enter,        // physical line 1
                line_1, _.Shift_Enter,        // physical line 2
                line_2, _.Shift_Enter,        // physical line 3
                wrappedLine_1, _.Shift_Enter, // physical line 4,5
                wrappedLine_2, _.Shift_Enter, // physical line 6,7
                line_3,                       // physical line 8

                // Starting at the end of the last line.
                // Verify that UpArrow goes to the end of the previous logical line.
                CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_3.Length, 8)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_3.Length, 8)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_3.Length, 8)),
                // Press Up/Down/Up
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(wrappedLength_2, 7)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_3.Length, 8)),
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(wrappedLength_2, 7)),
                // Press Up/Down/Up
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(wrappedLength_1, 5)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(wrappedLength_2, 7)),
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(wrappedLength_1, 5)),
                // Press Up/Down/Up
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_2.Length, 3)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(wrappedLength_1, 5)),
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_2.Length, 3)),
                // Press Up/Up
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_1.Length, 2)),
                _.UpArrow,   CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length, 1)),

                // Move to left for 1 character, so the cursor now is not at the end of line.
                // Verify that DownArrow/UpArrow goes to the previous logical line at the same column.
                _.LeftArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 1)),
                // Press Down all the way to the end
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_1.Length, 2)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 3)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 4)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 5)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 6)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(wrappedLength_2, 7)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_3.Length, 8)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_3.Length, 8)),
                // Press Up all the way to the physical line 1
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(wrappedLength_2, 7)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 6)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 5)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 4)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 3)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_1.Length, 2)),
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength + line_0.Length - 1, 1)),

                // Clear the input, we were just testing movement
                _.Escape
                ));
        }

        [SkippableFact]
        public void MultilineCursorMovement()
        {
            TestSetup(KeyMode.Cmd);

            var continutationPromptLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;
            Test("", Keys(
                "4444", _.Shift_Enter,
                "666666", _.Shift_Enter,
                "88888888", _.Shift_Enter,
                "666666", _.Shift_Enter,
                "4444", _.Shift_Enter,

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
                _.UpArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 0)),    // was (4,0), but seems wrong

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
                _.Shift_Enter,
                "88888888", _.Shift_Enter,
                "55555", _.Shift_Enter,
                "22", _.Shift_Enter,
                "55555", _.Shift_Enter,
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

                // Using the input previously entered, check for correct cursor movements when first line is blank
                _.Home, _.Home, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(8 + continutationPromptLength, 1)),
                _.Home, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                _.Home, CheckThat(() => AssertCursorLeftTopIs(0,0)),

                // Clear the input, we were just testing movement
                _.Escape
                ));
        }

        [SkippableFact]
        public void CursorMovement()
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
                _.Alt_9, _.LeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                _.Alt_9, _.RightArrow, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Alt_5, _.LeftArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.Alt_Minus, _.Alt_2, _.LeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.Alt_2, _.RightArrow, CheckThat(() => AssertCursorLeftIs(9)),
                _.Alt_Minus, _.Alt_7, _.RightArrow, CheckThat(() => AssertCursorLeftIs(2))));
        }

        [SkippableFact]
        public void GotoBrace()
        {
            Skip.IfNot(KeyboardHasCtrlRBracket);

            TestSetup(KeyMode.Cmd);

            // Test empty input
            Test("", Keys(_.Ctrl_RBracket));

            Test("(11)", Keys("(11)", _.LeftArrow, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("$a[11]", Keys("$a[11]", _.LeftArrow, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(2))));
            Test("{11}", Keys("{11}", _.LeftArrow, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("(11)", Keys("(11)", _.Home, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(3))));
            Test("$a[11]", Keys("$a[11]", _.Home, _.RightArrow, _.RightArrow, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(5))));
            Test("{11}", Keys("{11}", _.Home, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(3))));

            // Test multiples, make sure we go to the right one.
            Test("((11))", Keys("((11))", _.LeftArrow, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("((11))", Keys("((11))", _.LeftArrow, _.LeftArrow, _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(1))));

            // Make sure we don't match inside a string
            TestMustDing("", Keys(
                "'()'", _.LeftArrow, _.LeftArrow,
                _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(2)),
                _.Ctrl_c, InputAcceptedNow));

            foreach (var c in new[] {'(', ')', '{', '}', '[', ']'})
            {
                TestMustDing("", Keys(
                    'a', c, _.LeftArrow,
                    _.Ctrl_RBracket, CheckThat(() => AssertCursorLeftIs(1)),
                    _.Ctrl_c, InputAcceptedNow));
            }
        }

        [SkippableFact]
        public void CharacterSearch()
        {
            TestSetup(KeyMode.Cmd);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3", _.Home,
                _.F3, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.F3, '|', CheckThat(() => AssertCursorLeftIs(12))));
        }

        [SkippableFact]
        public void CharacterSearchEmacs()
        {
            Skip.IfNot(KeyboardHasCtrlRBracket);

            TestSetup(KeyMode.Emacs);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3", _.Home,
                _.Ctrl_RBracket, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.Ctrl_RBracket, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.End,
                _.Alt_Minus, _.Alt_2, _.Ctrl_RBracket, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.Home,
                _.Alt_2, _.Ctrl_RBracket, '|', CheckThat(() => AssertCursorLeftIs(12))));

            TestMustDing("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.Ctrl_RBracket, 'z'));
        }

        [SkippableFact]
        public void CharacterSearchApi()
        {
            int i = 0;
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z",
                (key, count) => PSConsoleReadLine.CharacterSearch(null, i++ == 0 ? (object)'|' : "|")));

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.Home,
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(5)),
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(12))));
        }

        [SkippableFact]
        public void CharacterSearchBackward()
        {
            TestSetup(KeyMode.Cmd);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.Shift_F3, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.Shift_F3, '|', CheckThat(() => AssertCursorLeftIs(5))));
        }

        [SkippableFact]
        public void CharacterSearchBackwardEmacs()
        {
            Skip.IfNot(KeyboardHasCtrlRBracket);

            TestSetup(KeyMode.Emacs);

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.Ctrl_Alt_RBracket, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.Ctrl_Alt_RBracket, '|', CheckThat(() => AssertCursorLeftIs(5)),
                _.Home,
                _.Alt_Minus, _.Alt_2, _.Ctrl_Alt_RBracket, '|', CheckThat(() => AssertCursorLeftIs(12)),
                _.End,
                _.Alt_2, _.Ctrl_Alt_RBracket, '|', CheckThat(() => AssertCursorLeftIs(5))));

            TestMustDing("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.Ctrl_Alt_RBracket, 'z'));
        }

        [SkippableFact]
        public void CharacterSearchBackwardApi()
        {
            int i = 0;
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z",
                (key, count) => PSConsoleReadLine.CharacterSearchBackward(null, i++ == 0 ? (object)'|' : "|")));

            Test("cmd1 | cmd2 | cmd3", Keys(
                "cmd1 | cmd2 | cmd3",
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(12)),
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(5))));
        }
    }
}
