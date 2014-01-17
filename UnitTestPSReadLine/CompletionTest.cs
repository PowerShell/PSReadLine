using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Reflection;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestTabComplete()
        {
            TestSetup(KeyMode.Cmd);

            Test("$true", Keys(
                "$tr",
                _.Tab,
                CheckThat(() => AssertCursorLeftIs(5))));

            // Validate no change on no match
            Test("$zz", Keys(
                "$zz",
                _.Tab,
                CheckThat(() => AssertCursorLeftIs(3))));

            Test("$this", Keys(
                "$t",
                _.Tab,
                CheckThat(() => AssertLineIs("$thing")),
                _.Tab,
                CheckThat(() => AssertLineIs("$this")),
                _.Tab,
                CheckThat(() => AssertLineIs("$true")),
                _.ShiftTab,
                CheckThat(() => AssertLineIs("$this"))));
        }

        [TestMethod]
        public void TestComplete()
        {
            TestSetup(KeyMode.Emacs);

            Test("ambiguous1", Keys(
                "ambig",
                _.Tab,
                CheckThat(() => AssertLineIs("ambiguous")),
                '1'));
        }

        [TestMethod]
        public void TestPossibleCompletions()
        {
            TestSetup(KeyMode.Emacs);

            ClearScreen();
            // Test empty input, make sure line after the cursor is blank and cursor didn't move
            Test("", Keys(
                _.AltEquals,
                CheckThat(() =>
                {
                    AssertCursorLeftTopIs(0, 0);
                    AssertScreenIs(2, NextLine);
                })));

            const string promptLine1 = "c:\\windows";
            const string promptLine2 = "PS> ";
            Test("psvar", Keys(
                "psvar",
                _.AltEquals,
                CheckThat(() => AssertScreenIs(5,
                                               TokenClassification.None, promptLine1,
                                               NextLine,
                                               promptLine2,
                                               TokenClassification.Command, "psvar",
                                               NextLine,
                                               "$pssomething",
                                               NextLine,
                                               TokenClassification.None, promptLine1,
                                               NextLine,
                                               promptLine2,
                                               TokenClassification.Command, "psvar"))),
                prompt: promptLine1 + "\n" + promptLine2);

            using (ShimsContext.Create())
            {
                bool ding = false;
                PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.Ding =
                    () => ding = true;

                ClearScreen();
                Test("none", Keys(
                    "none",
                    _.AltEquals,
                    CheckThat(() => AssertScreenIs(2, TokenClassification.Command, "none", NextLine))));
                Assert.IsTrue(ding);
            }
        }

        static private CommandCompletion MockedCompleteInput(string input, int cursor, Hashtable options, PowerShell powerShell)
        {
            var ctor = typeof (CommandCompletion).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null, 
                new [] {typeof (Collection<CompletionResult>), typeof (int), typeof (int), typeof (int)}, null);

            var completions = new Collection<CompletionResult>();
            const int currentMatchIndex = -1;
            var replacementIndex = 0;
            var replacementLength = 0;
            switch (input)
            {
            case "$t":
                replacementIndex = 0;
                replacementLength = 2;
                completions.Add(new CompletionResult("$thing"));
                completions.Add(new CompletionResult("$this"));
                completions.Add(new CompletionResult("$true"));
                break;
            case "$tr":
                replacementIndex = 0;
                replacementLength = 3;
                completions.Add(new CompletionResult("$true"));
                break;
            case "psvar":
                replacementIndex = 0;
                replacementLength = 5;
                completions.Add(new CompletionResult("$pssomething"));
                break;
            case "ambig":
                replacementIndex = 0;
                replacementLength = 5;
                completions.Add(new CompletionResult("ambiguous1"));
                completions.Add(new CompletionResult("ambiguous2"));
                completions.Add(new CompletionResult("ambiguous3"));
                break;
            case "none":
                break;
            }

            return (CommandCompletion)ctor.Invoke(
                new object[] {completions, currentMatchIndex, replacementIndex, replacementLength});
        }
    }
}
