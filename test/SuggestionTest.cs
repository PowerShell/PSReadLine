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
                CheckThat(() => AssertCursorLeftIs(1))));

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
        public void DisablePrediction()
        {
            TestSetup(KeyMode.Cmd);
            SetHistory("echo -bar", "eca -zoo");
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {PredictionStyle = PredictionStyle.None});

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
    }
}
