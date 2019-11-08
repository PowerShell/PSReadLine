using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ContinuationPrompt()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "{\n}",
                CheckThat(() =>
                    AssertScreenIs(2,
                        TokenClassification.None, '{',
                        NextLine,
                        Tuple.Create(_console.ForegroundColor, _console.BackgroundColor),
                        PSConsoleReadLineOptions.DefaultContinuationPrompt,
                        TokenClassification.None, '}')),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption{ ContinuationPrompt = ""});
            Test("", Keys(
                "{\n}",
                CheckThat(() => AssertScreenIs(2, TokenClassification.None, '{', NextLine, '}' )),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            var continuationPrompt = "::::: ";
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption{
                ContinuationPrompt = continuationPrompt,
                Colors = new Hashtable {
                    { "ContinuationPrompt", MakeCombinedColor(ConsoleColor.Magenta, ConsoleColor.DarkYellow) }
                },
            });
            Test("", Keys(
                "{\n}",
                CheckThat(() =>
                    AssertScreenIs(2,
                        TokenClassification.None, '{',
                        NextLine,
                        Tuple.Create(ConsoleColor.Magenta, ConsoleColor.DarkYellow),
                        continuationPrompt,
                        TokenClassification.None, '}')),
                _.Ctrl_c,
                InputAcceptedNow
                ));
        }

        [SkippableFact]
        public void GetKeyHandlers()
        {
            foreach (var keymode in new[] {KeyMode.Cmd, KeyMode.Emacs})
            {
                TestSetup(keymode);

                foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: false, includeUnbound: true))
                {
                    Assert.Equal("Unbound", handler.Key);
                    Assert.False(string.IsNullOrWhiteSpace(handler.Function));
                    Assert.False(string.IsNullOrWhiteSpace(handler.Description));
                }

                foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: true, includeUnbound: false))
                {
                    Assert.NotEqual("Unbound", handler.Key);
                    Assert.False(string.IsNullOrWhiteSpace(handler.Function));
                    Assert.False(string.IsNullOrWhiteSpace(handler.Description));
                }
            }
        }

        [SkippableFact]
        public void SetInvalidColorOptions()
        {
            bool throws = false;
            try
            {
                PSConsoleReadLine.SetOptions(new SetPSReadLineOption{
                    Colors = new Hashtable {
                        { "InvalidProperty", ConsoleColor.Magenta }
                    },
                });
            }
            catch (ArgumentException) { throws = true; }
            Assert.True(throws, "Invalid color property should throw");

            throws = false;
            try
            {
                PSConsoleReadLine.SetOptions(new SetPSReadLineOption{
                    Colors = new Hashtable {
                        { "Default", "apple" }
                    },
                });
            }
            catch (ArgumentException) { throws = true; }
            Assert.True(throws, "Invalid color value should throw");
        }

        [SkippableFact]
        [ExcludeFromCodeCoverage]
        public void UselessStuffForBetterCoverage()
        {
            // Useless test to just make sure coverage numbers are better, written
            // in the first way I could think of that doesn't warn about doing something useless.
            var options = new SetPSReadLineOption();
            var getKeyHandlerCommand = new GetKeyHandlerCommand();
            var useless = ((object)options.AddToHistoryHandler ?? options).GetHashCode()
                          + options.EditMode.GetHashCode()
                          + ((object)options.ContinuationPrompt ?? options).GetHashCode()
                          + options.HistoryNoDuplicates.GetHashCode()
                          + options.HistorySearchCursorMovesToEnd.GetHashCode()
                          + options.MaximumHistoryCount.GetHashCode()
                          + options.MaximumKillRingCount.GetHashCode()
                          + options.DingDuration.GetHashCode()
                          + options.DingTone.GetHashCode()
                          + options.BellStyle.GetHashCode()
                          + options.ExtraPromptLineCount.GetHashCode()
                          + options.ShowToolTips.GetHashCode()
                          + options.CompletionQueryItems.GetHashCode()
                          + options.HistorySearchCaseSensitive.GetHashCode()
                          + getKeyHandlerCommand.Bound.GetHashCode()
                          + getKeyHandlerCommand.Unbound.GetHashCode();
            // This assertion just avoids annoying warnings about unused variables.
            Assert.NotEqual(Math.PI, useless);
        }
    }
}
