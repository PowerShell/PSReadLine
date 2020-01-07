using Microsoft.PowerShell;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void Input()
        {
            TestSetup(KeyMode.Cmd);

            Test("exit", Keys(
                "exit",
                _.Enter,
                CheckThat(() => AssertCursorLeftIs(0))));
        }

        [SkippableFact]
        public void RevertLine()
        {
            // Add one test for chords
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+x,Escape", PSConsoleReadLine.RevertLine));

            Test("ls", Keys("di", _.Escape, "ls"));
            Test("ls", Keys("di", _.Ctrl_x, _.Escape, "ls"));

            TestSetup(KeyMode.Emacs);
            Test("ls", Keys("di", _.Escape, _.r, "ls"));
            Test("ls", Keys("di", _.Alt_r, "ls"));
        }

        [SkippableFact]
        public void CancelLine()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+C", PSConsoleReadLine.CancelLine));

            Test("", Keys("oops", _.Ctrl_C,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "oops",
                    Tuple.Create(ConsoleColor.Red, _console.BackgroundColor), "^C")),
                InputAcceptedNow));

            Test("", Keys("exit", _.Ctrl_C,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Keyword, "exit",
                    Tuple.Create(ConsoleColor.Red, _console.BackgroundColor), "^C")),
                InputAcceptedNow));

            // Test near/at/over buffer width input
            var width = _console.BufferWidth;
            var line1 = new string('a', width - 2);
            Test("", Keys(line1, _.Ctrl_C,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, line1,
                    Tuple.Create(ConsoleColor.Red, _console.BackgroundColor), "^C")),
                InputAcceptedNow));
            var line2 = new string('a', width - 1);
            Test("", Keys(line2, _.Ctrl_C,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, line2,
                    Tuple.Create(ConsoleColor.Red, _console.BackgroundColor), "^C")),
                InputAcceptedNow));
            var line3 = new string('a', width);
            Test("", Keys(line3, _.Ctrl_C,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, line3,
                    Tuple.Create(ConsoleColor.Red, _console.BackgroundColor), "^C")),
                InputAcceptedNow));
        }

        [SkippableFact]
        public void ForwardDeleteLine()
        {
            ConsoleKeyInfo deleteToEnd;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TestSetup(KeyMode.Cmd);
                deleteToEnd = _.Ctrl_End;
            }
            else
            {
                TestSetup(KeyMode.Emacs);
                deleteToEnd = _.Ctrl_k;
            }

            // Empty input (does nothing but don't crash)
            Test("", Keys("", deleteToEnd));

            // at end of input - doesn't change anything
            Test("abc", Keys("abc", deleteToEnd));

            // More normal usage - actually delete stuff
            Test("a", Keys("abc", _.LeftArrow, _.LeftArrow, deleteToEnd));
        }

        [SkippableFact]
        public void BackwardDeleteLine()
        {
            TestSetup(KeyMode.Cmd);

            // Empty input (does nothing but don't crash)
            Test("", Keys(_.Ctrl_Home));

            // at beginning of input - doesn't change anything
            Test("abc", Keys("abc", _.Home, _.Ctrl_Home));

            // More typical usage
            Test("c", Keys("abc", _.LeftArrow, _.Ctrl_Home));
            Test("", Keys("abc", _.Ctrl_Home));
        }

        [SkippableFact]
        public void BackwardDeleteChar()
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

        [SkippableFact]
        public void DeleteChar()
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

        [SkippableFact]
        public void DeleteCharAfterDigitArgument()
        {
            TestSetup(KeyMode.Cmd);

            Test("abc", Keys(
                "ab", _.LeftArrow, _.Alt_2, _.Delete,
                CheckThat(() => AssertLineIs("a")),
                CheckThat(() => AssertCursorLeftIs(1)),
                _.Delete, // 'Delete' again does nothing, but doesn't crash
                CheckThat(() => AssertLineIs("a")),
                CheckThat(() => AssertCursorLeftIs(1)),
                "bc"));
        }

        [SkippableFact]
        public void DeleteCharOrExit()
        {
            TestSetup(KeyMode.Emacs);

            Test("exit", Keys(_.Ctrl_d, InputAcceptedNow));

            Test("foo", Keys("foo", _.Ctrl_d));
            Test("oo", Keys("foo", _.Home, _.Ctrl_d));
            Test("exit", Keys("foo", _.Home, Enumerable.Repeat(_.Ctrl_d, 4), InputAcceptedNow));
        }

        [SkippableFact]
        public void SwapCharacters()
        {
            TestSetup(KeyMode.Emacs);

            TestMustDing("", Keys(_.Ctrl_t));
            TestMustDing("a", Keys("a", _.Ctrl_t));
            TestMustDing("abc", Keys("abc", _.Home, _.Ctrl_t));

            Test("abc", Keys(
                "abc", CheckThat(() => AssertLineIs("abc")),
                _.Ctrl_t, CheckThat(() => AssertLineIs("acb")),
                _.Ctrl_Underbar
                ));

            Test("abcd", Keys(
                "abcd", CheckThat(() => AssertLineIs("abcd")),
                _.Ctrl_a, _.Ctrl_t, CheckThat(() => AssertLineIs("abcd")),
                _.Ctrl_f, Enumerable.Repeat(_.Ctrl_t, 3), CheckThat(() => AssertLineIs("bcda")),
                _.Ctrl_t, CheckThat(() => AssertLineIs("bcad")),
                Enumerable.Repeat(_.Ctrl_Underbar, 4)
                ));
        }

        [SkippableFact]
        public void AcceptAndGetNext()
        {
            TestSetup(KeyMode.Emacs);

            // No history
            Test("", Keys(_.Ctrl_o, InputAcceptedNow));

            // One item in history
            SetHistory("echo 1");
            Test("", Keys(_.Ctrl_o, InputAcceptedNow));

            // Two items in history, make sure after Ctrl+O, second history item
            // is recalled.
            SetHistory("echo 1", "echo 2");
            Test("echo 1", Keys(_.UpArrow, _.UpArrow, _.Ctrl_o, InputAcceptedNow));
            Test("echo 2", Keys(_.Enter));

            // Test that the current saved line is saved after AcceptAndGetNext
            SetHistory("echo 1", "echo 2");
            Test("echo 1", Keys("e", _.UpArrow, _.UpArrow, _.Ctrl_o, InputAcceptedNow));
            Test("e", Keys(_.DownArrow, _.DownArrow, _.Enter));

            // Test that we can edit after recalling the current line
            SetHistory("echo 1", "echo 2");
            Test("echo 1", Keys("e", _.UpArrow, _.UpArrow, _.Ctrl_o, InputAcceptedNow));
            Test("eee", Keys(_.DownArrow, _.DownArrow, "ee", _.Enter));
        }

        [SkippableFact]
        public void AcceptAndGetNextWithHistorySearch()
        {
            TestSetup(KeyMode.Emacs,
                      new KeyHandler("UpArrow", PSConsoleReadLine.HistorySearchBackward),
                      new KeyHandler("DownArrow", PSConsoleReadLine.HistorySearchForward));

            // Test that after AcceptAndGetNext, the previous search is not applied
            SetHistory("echo 1", "echo 2", "zzz");
            Test("echo 1", Keys("e", _.UpArrow, _.UpArrow, _.Ctrl_o, InputAcceptedNow));
            Test("zzz", Keys(_.DownArrow, _.Enter));
        }

        [SkippableFact]
        public void AddLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("1\n2", Keys('1', _.Shift_Enter, '2'));
        }

        [SkippableFact]
        public void InsertLineAbove()
        {
            TestSetup(KeyMode.Cmd);

            var continutationPromptLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            // Test case - start with single line, cursor at end
            Test("56\n1234", Keys("1234", _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(0, 0)), "56"));

            // Test case - start with single line, cursor in home position
            Test("56\n1234", Keys("1234", _.Home, _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(0, 0)), "56"));

            // Test case - start with single line, cursor in middle
            Test("56\n1234", Keys("1234",
                                  _.LeftArrow, _.LeftArrow, _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                                  "56"));


            // Test case - start with multi-line, cursor at end of second line (end of input)
            Test("1234\n9ABC\n5678", Keys("1234", _.Shift_Enter, "5678",
                                          _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          "9ABC"));

            // Test case - start with multi-line, cursor at beginning of second line
            Test("1234\n9ABC\n5678", Keys("1234", _.Shift_Enter, "5678",
                                          _.LeftArrow, _.Home, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          "9ABC"));

            // Test case - start with multi-line, cursor at end of first line
            Test("9ABC\n1234\n5678", Keys("1234", _.Shift_Enter, "5678",
                                          _.UpArrow, _.LeftArrow, _.End, CheckThat(() => AssertCursorLeftTopIs(4, 0)),
                                          _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                                          "9ABC"));

            // Test case - start with multi-line, cursor at beginning of first line - temporarily having to press Home twice to
            // work around bug in home handler.
            Test("9ABC\n1234\n5678", Keys("1234", _.Shift_Enter, "5678",
                                          _.UpArrow, _.LeftArrow, _.Home, _.Home, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                                          _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                                          "9ABC"));

            // Test case - insert multiple blank lines
            Test("1234\n9ABC\n\n5678", Keys("1234", _.Shift_Enter, "5678",
                                          _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          "9ABC"));

            // Test case - create leading blank line, cursor to stay on same line
            Test("\n\n1234", Keys("1234",
                _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(0,0)),
                _.DownArrow, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                _.Ctrl_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1))));
        }

        [SkippableFact]
        public void InsertLineBelow()
        {
            TestSetup(KeyMode.Cmd);

            var continutationPromptLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            // Test case - start with single line, cursor at end
            Test("1234\n56", Keys("1234",
                                  _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                  "56"));

            // Test case - start with single line, cursor in home position
            Test("1234\n56", Keys("1234",
                                  _.Home, _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                  "56"));

            // Test case - start with single line, cursor in middle
            Test("1234\n56", Keys("1234",
                                  _.LeftArrow, _.LeftArrow, _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                  "56"));

            // Test case - start with multi-line, cursor at end of second line (end of input)
            Test("1234\n5678\n9ABC", Keys("1234", _.Shift_Enter, "5678",
                                          _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 2)),
                                          "9ABC"));

            // Test case - start with multi-line, cursor at beginning of second line
            Test("1234\n5678\n9ABC", Keys("1234", _.Shift_Enter, "5678",
                                          _.LeftArrow, _.Home, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 2)),
                                          "9ABC"));

            // Test case - start with multi-line, cursor at end of first line
            Test("1234\n9ABC\n5678", Keys("1234", _.Shift_Enter, "5678",
                                          _.UpArrow, _.LeftArrow, _.End, CheckThat(() => AssertCursorLeftTopIs(4, 0)),
                                          _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          "9ABC"));

            // Test case - start with multi-line, cursor at beginning of first line - temporarily having to press Home twice to
            // work around bug in home handler.
            Test("1234\n9ABC\n5678", Keys("1234", _.Shift_Enter, "5678",
                                          _.UpArrow, _.LeftArrow, _.Home, _.Home, CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                                          _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 1)),
                                          "9ABC"));

            // Test case - insert multiple blank lines
            Test("1234\n5678\n\n9ABC", Keys("1234", _.Shift_Enter, "5678",
                                          _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 2)),
                                          _.Ctrl_Shift_Enter, CheckThat(() => AssertCursorLeftTopIs(continutationPromptLength, 3)),
                                          "9ABC"));
        }

        [SkippableFact]
        public void MultilineHomeBugFixed()
        {
            TestSetup(KeyMode.Cmd);

            // Going from second line to first line, press left arrow and then home.
            // That puts cursor in column 1 instead of 0. Bug?  This could have something
            // to do with BeginningOfLine testing against i > 1 in multiline edit instead
            // of i > 0.
            Test("1234\n9ABC", Keys("1234", _.Shift_Enter, "9ABC", _.UpArrow, _.LeftArrow, _.Home, CheckThat(() => AssertCursorLeftTopIs(0, 0))));
        }

        [SkippableFact]
        public void Ignore()
        {
            TestSetup(KeyMode.Emacs);

            Test("ab", Keys("a", _.VolumeDown, _.VolumeMute, _.VolumeUp, "b"));
        }
    }
}
