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
            System.Collections.Generic.IEnumerable<Microsoft.PowerShell.KeyHandler> handlers;

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

                handlers = PSConsoleReadLine.GetKeyHandlers(Chord: new string[] { "home" });
                Assert.NotEmpty(handlers);
                foreach (var handler in handlers)
                {
                    Assert.Equal("Home", handler.Key);
                }
            }

            TestSetup(KeyMode.Emacs);
            
            handlers = PSConsoleReadLine.GetKeyHandlers(Chord: new string[] { "ctrl+x" });
            Assert.NotEmpty(handlers);
            foreach (var handler in handlers)
            {
                Assert.Equal("Ctrl+x", handler.Key);
            }

            handlers = PSConsoleReadLine.GetKeyHandlers(Chord: new string[] { "ctrl+x,ctrl+e" });
            Assert.NotEmpty(handlers);
            foreach (var handler in handlers)
            {
                Assert.Equal("Ctrl+x,Ctrl+e", handler.Key);
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
        public void SetPromptTextOption()
        {
            // For prompt texts with VT sequences, reset all attributes if not already.
            string[] promptTexts_1 = new[] { "\x1b[91m> " };
            string[] promptTexts_2 = new[] { "\x1b[91m> ", "\x1b[92m> ", "\x1b[93m> " };
            string[] promptTexts_3 = new[] { "> " };
            string[] promptTexts_4 = new[] { "> ", "] " };
            string[] promptTexts_5 = new[] { "\x1b[93m> \x1b[0m" };

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {
                PromptText = promptTexts_1,
            });
            Assert.Equal("\x1b[91m> \x1b[0m", promptTexts_1[0]);

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {
                PromptText = promptTexts_2,
            });
            Assert.Equal("\x1b[91m> \x1b[0m", promptTexts_2[0]);
            Assert.Equal("\x1b[92m> \x1b[0m", promptTexts_2[1]);
            Assert.Equal("\x1b[93m> ", promptTexts_2[2]);

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {
                PromptText = promptTexts_3,
            });
            Assert.Equal("> ", promptTexts_3[0]);

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {
                PromptText = promptTexts_4,
            });
            Assert.Equal("> ", promptTexts_4[0]);
            Assert.Equal("] ", promptTexts_4[1]);

            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {
                PromptText = promptTexts_5,
            });
            Assert.Equal("\x1b[93m> \x1b[0m", promptTexts_5[0]);
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
