using Microsoft.PowerShell;
using System;
using System.Collections;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void RenderSuggestion()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));

            // No matching history entry
            SetHistory("echo -bar", "eca -zoo");
            Test("a", Keys(
                'a', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'a')),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'a')),
                _.RightArrow, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'a')),
                CheckThat(() => AssertCursorLeftIs(1))
            ));

            // Different matches as more input coming
            SetHistory("echo -bar", "eca -zoo");
            Test("ech", Keys(
                'e', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.Prediction, "ca -zoo")),
                'c', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.Prediction, "a -zoo")),
                'h', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech",
                        TokenClassification.Prediction, "o -bar")),
                // Once accepted, the suggestion text should be blanked out.
                _.Enter, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech"))
            ));

            // Accept all or partial suggestion text with 'ForwardChar' and 'ForwardWord'
            SetHistory("echo -bar", "eca -zoo");
            Test("eca ", Keys(
                'e', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.Prediction, "ca -zoo")),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.Prediction, "ca -zoo")),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Prediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4)),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Prediction, "-zoo"))
            ));
        }

        [SkippableFact]
        public void CustomKeyBindingsToAcceptSuggestion()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Alt+g", PSConsoleReadLine.AcceptSuggestion),
                      new KeyHandler("Alt+f", PSConsoleReadLine.AcceptNextSuggestionWord));

            SetHistory("echo -bar", "eca -zoo");
            Test("eca ", Keys(
                "ec", CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.Prediction, "a -zoo")),
                CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_g, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.Prediction, "a -zoo")),
                CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Prediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4)),
                _.Alt_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Prediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4)),

                // Revert back to 'ec'.
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(2)),
                // Move cursor to column 0.
                _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.Alt_g, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.Prediction, "a -zoo")),
                CheckThat(() => AssertCursorLeftIs(2)),
                // Move cursor to column 0 again.
                _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.Alt_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Prediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4))
            ));
        }

        [SkippableFact]
        public void AcceptNextSuggestionWordCanAcceptMoreThanOneWords()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord),
                      new KeyHandler("Alt+f", PSConsoleReadLine.AcceptNextSuggestionWord));

            SetHistory("abc def ghi jkl");
            Test("abc def ghi jkl", Keys(
                'a', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'a',
                        TokenClassification.Prediction, "bc def ghi jkl")),
                _.Alt_3, _.Ctrl_f,
                CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " def ghi ",
                        TokenClassification.Prediction, "jkl")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'a',
                        TokenClassification.Prediction, "bc def ghi jkl")),
                _.Alt_3, _.Alt_f,
                CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " def ghi ",
                        TokenClassification.Prediction, "jkl")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'a',
                        TokenClassification.Prediction, "bc def ghi jkl")),
                _.Alt_8, _.Alt_f,
                CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " def ghi jkl"))
            ));
        }

        [SkippableFact]
        public void AcceptSuggestionWithSelection()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));

            SetHistory("git diff --cached", "git diff");
            Test("git diff", Keys(
                "git", CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.Prediction, " diff")),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff")),
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(3)),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff",
                        TokenClassification.Prediction, " --cached")),

                // Perform visual selection and then accept suggestion.
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.Prediction, " diff")),
                _.LeftArrow, _.Shift_RightArrow,
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetSelectionState(out var start, out var length);
                    Assert.Equal(2, start);
                    Assert.Equal(1, length);
                }),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff")),

                // Perform visual selection and then accept next suggestion word.
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.Prediction, " diff")),
                _.LeftArrow, _.Shift_RightArrow,
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetSelectionState(out var start, out var length);
                    Assert.Equal(2, start);
                    Assert.Equal(1, length);
                }),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff",
                        TokenClassification.Prediction, " --cached"))
            ));
        }

        [SkippableFact]
        public void DisablePrediction()
        {
            TestSetup(KeyMode.Cmd);
            SetHistory("echo -bar", "eca -zoo");
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {PredictionSource = PredictionSource.None});

            Test("ech", Keys(
                'e', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'e')),
                'c', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ec")),
                'h', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ech")),
                _.Enter, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ech"))
            ));
        }

        [SkippableFact]
        public void SetPredictionColor()
        {
            TestSetup(KeyMode.Cmd);
            var predictionColor = MakeCombinedColor(ConsoleColor.DarkYellow, ConsoleColor.Yellow);
            var predictionColorToCheck = Tuple.Create(ConsoleColor.DarkYellow, ConsoleColor.Yellow);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {Colors = new Hashtable(){{"Prediction", predictionColor}}});

            SetHistory("echo -bar", "eca -zoo");
            Test("ech", Keys(
                'e', CheckThat(() => AssertScreenIs(1, 
                        TokenClassification.Command, 'e',
                        predictionColorToCheck, "ca -zoo")),
                'c', CheckThat(() => AssertScreenIs(1, 
                        TokenClassification.Command, "ec",
                        predictionColorToCheck, "a -zoo")),
                'h', CheckThat(() => AssertScreenIs(1, 
                        TokenClassification.Command, "ech",
                        predictionColorToCheck, "o -bar")),
                // Once accepted, the suggestion text should be blanked out.
                _.Enter, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech"))
            ));
        }

        [SkippableFact]
        public void HistoryEditsCanUndoProperly()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));
            SetHistory("git checkout -b branch origin/bbbb");

            // Accept partial suggestion.
            Test("git checkout ", Keys(
                "git ch", CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " ch",
                    TokenClassification.Prediction, "eckout -b branch origin/bbbb")),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " checkout ",
                    TokenClassification.Prediction, "-b branch origin/bbbb")),
                _.Enter, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " checkout "))
            ));

            // Get the last command line from history, and revert the line.
            // 'RevertLine' will undo all edits of the history command.
            Test("", Keys(
                _.UpArrow, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " checkout ")),
                _.Escape));
        }

        [SkippableFact]
        public void AcceptSuggestionInVIMode()
        {
            TestSetup(KeyMode.Vi);

            SetHistory("echo -bar", "eca -zoo");
            Test("ech", Keys(
                'e', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.Prediction, "ca -zoo")),
                'c', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.Prediction, "a -zoo")),
                'h', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech",
                        TokenClassification.Prediction, "o -bar")),
                CheckThat(() => AssertCursorLeftIs(3)),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "echo",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-bar")),
                CheckThat(() => AssertCursorLeftIs(9)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech",
                        TokenClassification.Prediction, "o -bar")),
                CheckThat(() => AssertCursorLeftIs(3)),

                _.RightArrow, _.Escape,
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech")),
                CheckThat(() => AssertCursorLeftIs(2))
            ));
        }
    }
}
