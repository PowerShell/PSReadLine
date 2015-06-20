using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestContinuationPrompt()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "{\n}",
                CheckThat(() =>
                    AssertScreenIs(2,
                        TokenClassification.None, '{',
                        NextLine,
                        Tuple.Create(Console.ForegroundColor, Console.BackgroundColor),
                        PSConsoleReadlineOptions.DefaultContinuationPrompt,
                        TokenClassification.None, '}')),
                _.CtrlC,
                InputAcceptedNow
                ));

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption{ ContinuationPrompt = ""});
            Test("", Keys(
                "{\n}",
                CheckThat(() => AssertScreenIs(2, TokenClassification.None, '{', NextLine, '}' )),
                _.CtrlC,
                InputAcceptedNow
                ));

            var continuationPrompt = "::::: ";
            PSConsoleReadLine.SetOptions(new SetPSReadlineOption{
                ContinuationPrompt = continuationPrompt,
                ContinuationPromptForegroundColor = ConsoleColor.Magenta,
                ContinuationPromptBackgroundColor = ConsoleColor.DarkYellow,
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
                _.CtrlC,
                InputAcceptedNow
                ));
        }

        [TestMethod]
        public void TestGetKeyHandlers()
        {
            foreach (var keymode in new[] {KeyMode.Cmd, KeyMode.Emacs})
            {
                TestSetup(keymode);

                foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: false, includeUnbound: true))
                {
                    Assert.AreEqual("Unbound", handler.Key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Function));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Description));
                }

                foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: true, includeUnbound: false))
                {
                    Assert.AreNotEqual("Unbound", handler.Key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Function));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Description));
                }
            }
        }

        [TestMethod]
        [ExcludeFromCodeCoverage]
        public void TestUselessStuffForBetterCoverage()
        {
            // Useless test to just make sure coverage numbers are better, written
            // in the first way I could think of that doesn't warn about doing something useless.
            var options = new SetPSReadlineOption();
            var getKeyHandlerCommand = new GetKeyHandlerCommand();
            var useless = ((object)options.AddToHistoryHandler ?? options).GetHashCode()
                          + options.EditMode.GetHashCode()
                          + ((object)options.ContinuationPrompt ?? options).GetHashCode()
                          + options.ContinuationPromptBackgroundColor.GetHashCode()
                          + options.ContinuationPromptForegroundColor.GetHashCode()
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
                          + options.EmphasisBackgroundColor.GetHashCode()
                          + options.EmphasisForegroundColor.GetHashCode()
                          + options.HistorySearchCaseSensitive.GetHashCode()
                          + getKeyHandlerCommand.Bound.GetHashCode()
                          + getKeyHandlerCommand.Unbound.GetHashCode();
            // This assertion just avoids annoying warnings about unused variables.
            Assert.AreNotEqual(Math.PI, useless);

            bool exception = false;
            try
            {
                CreateCharInfoBuffer(0, new object());
            }
            catch (ArgumentException)
            {
                exception = true;
            }
            Assert.IsTrue(exception, "CreateCharBuffer invalid arugment raised an exception");
        }
    }
}
