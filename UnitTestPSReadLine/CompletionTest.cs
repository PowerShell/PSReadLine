using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Reflection;
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

            Console.Clear();
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
            Console.Clear();
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

            Console.Clear();
            TestMustDing("none", Keys(
                "none",
                _.AltEquals,
                CheckThat(() => AssertScreenIs(2, TokenClassification.Command, "none", NextLine))));
        }

        [TestMethod]
        public void TestPossibleCompletionsPrompt()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.PossibleCompletions));

            PSConsoleReadLine.GetOptions().CompletionQueryItems = 10;
            Console.Clear();
            Test("Get-Many", Keys(
                "Get-Many", _.CtrlSpace,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Many", NextLine,
                    TokenClassification.None, "Display all 15 possibilities? (y or n) _")),
                "n"));

            Console.Clear();
            Test("Get-Many", Keys(
                "Get-Many", _.CtrlSpace,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Many", NextLine,
                    TokenClassification.None, "Display all 15 possibilities? (y or n) _")),
                "y",
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14"))));
        }

        [TestMethod]
        public void TestShowTooltips()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.PossibleCompletions));

            PSConsoleReadLine.GetOptions().ShowToolTips = true;
            Console.Clear();
            Test("Get-Tooltips", Keys(
                "Get-Tooltips", _.CtrlSpace,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Tooltips", NextLine,
                    TokenClassification.None,
                    "item1  - useful description goes here"))));
        }

        [TestMethod]
        public void TestDirectoryCompletion()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "Get-Directory", _.Tab,
                CheckThat(() => AssertLineIs("abc\\")),
                _.Tab,
                CheckThat(() => AssertLineIs("'e f\\'")),
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Tab,
                CheckThat(() => AssertLineIs("a\\")),
                _.Tab,
                CheckThat(() => AssertLineIs("'a b\\'")),
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Tab,
                CheckThat(() => AssertLineIs("\"a b\\\"")),
                CheckThat(() => AssertCursorLeftIs(5)),
                _.CtrlC, InputAcceptedNow));
        }

        static internal CommandCompletion MockedCompleteInput(string input, int cursor, Hashtable options, PowerShell powerShell)
        {
            var ctor = typeof (CommandCompletion).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, 
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
            case "Get-Many":
                replacementIndex = 0;
                replacementLength = 8;
                for (int i = 0; i < 15; i++)
                {
                    completions.Add(new CompletionResult("Get-Many" + i));
                }
                break;
            case "Get-Tooltips":
                replacementIndex = 0;
                replacementLength = 12;
                completions.Add(new CompletionResult("something really long", "item1", CompletionResultType.Command, "useful description goes here"));
                break;
            case "Get-Directory":
                replacementIndex = 0;
                replacementLength = 13;
                completions.Add(new CompletionResult("abc", "abc", CompletionResultType.ProviderContainer, "abc"));
                completions.Add(new CompletionResult("'e f'", "'e f'", CompletionResultType.ProviderContainer, "'e f'"));
                completions.Add(new CompletionResult("a", "a", CompletionResultType.ProviderContainer, "a"));
                completions.Add(new CompletionResult("'a b\\'", "a b\\'", CompletionResultType.ProviderContainer, "a b\\'"));
                completions.Add(new CompletionResult("\"a b\\\"", "\"a b\\\"", CompletionResultType.ProviderContainer, "\"a b\\\""));
                break;
            case "none":
                break;
            }

            return (CommandCompletion)ctor.Invoke(
                new object[] {completions, currentMatchIndex, replacementIndex, replacementLength});
        }
    }
}
