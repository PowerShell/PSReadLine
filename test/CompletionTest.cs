using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void TabComplete()
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
                _.Shift_Tab,
                CheckThat(() => AssertLineIs("$this"))));
        }

        [SkippableFact]
        public void InvalidCompletionResult()
        {
            TestSetup(KeyMode.Cmd);

            for (int i = 1; i <= 4; i++)
            {
                var input = $"invalid result {i}";
                Test(input, Keys(input, _.Tab));
            }
        }

        [SkippableFact]
        public void Complete()
        {
            TestSetup(KeyMode.Emacs);

            Test("ambiguous1", Keys(
                "ambig",
                _.Tab,
                CheckThat(() => AssertLineIs("ambiguous")),
                '1'));
        }

        [SkippableFact]
        public void PossibleCompletions()
        {
            TestSetup(KeyMode.Emacs);

            _console.Clear();
            // Test empty input, make sure line after the cursor is blank and cursor didn't move
            Test("", Keys(
                _.Alt_Equals,
                CheckThat(() =>
                {
                    AssertCursorLeftTopIs(0, 0);
                    AssertScreenIs(2, NextLine);
                })));

            const string promptLine1 = "c:\\windows";
            const string promptLine2 = "PS> ";
            using (var ps = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                ps.AddScript($@"function prompt {{ ""{promptLine1}`n{promptLine2}"" }}");
                ps.Invoke();
            }
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption {ExtraPromptLineCount = 1});

            _console.Clear();
            Test("psvar", Keys(
                "psvar",
                _.Alt_Equals,
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

            using (var ps = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                ps.AddCommand("Remove-Item").AddArgument("function:prompt");
                ps.Invoke();
            }

            _console.Clear();
            TestMustDing("none", Keys(
                "none",
                _.Alt_Equals,
                CheckThat(() => AssertScreenIs(2, TokenClassification.Command, "none", NextLine))));
        }

        [SkippableFact]
        public void PossibleCompletionsPrompt()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.PossibleCompletions));

            PSConsoleReadLine.GetOptions().CompletionQueryItems = 10;
            _console.Clear();
            Test("Get-Many", Keys(
                "Get-Many", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Many", NextLine,
                    TokenClassification.None, "Display all 15 possibilities? (y or n) _")),
                "n"));

            _console.Clear();
            Test("Get-Many", Keys(
                "Get-Many", _.Ctrl_Spacebar,
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

        [SkippableFact]
        public void MenuCompletions()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            Test("Get-Many4", Keys(
                "Get-Many", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Many0   ",
                    TokenClassification.None,
                    "Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                "4",
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many4", NextLine,
                    TokenClassification.Selection, "Get-Many4  ", NextLine,
                    "                                                          ", NextLine,
                    "                                                          ", NextLine)),
                _.Enter,
                _.Enter
                ));
        }

        [SkippableFact]
        public void ShowTooltips()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.PossibleCompletions));

            PSConsoleReadLine.GetOptions().ShowToolTips = true;
            _console.Clear();
            // TODO:
        }

        [SkippableFact]
        public void DirectoryCompletion()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "Get-Directory", _.Tab,
                CheckThat(() => AssertLineIs("abc" + Path.DirectorySeparatorChar)),
                _.Tab,
                CheckThat(() => AssertLineIs("'e f" + Path.DirectorySeparatorChar + "'")),
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Tab,
                CheckThat(() => AssertLineIs("a" + Path.DirectorySeparatorChar)),
                _.Tab,
                CheckThat(() => AssertLineIs("'a b" + Path.DirectorySeparatorChar + "'")),
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Tab,
                CheckThat(() => AssertLineIs("\"a b" + Path.DirectorySeparatorChar + "\"")),
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Ctrl_c, InputAcceptedNow));
        }

        internal static CommandCompletion MockedCompleteInput(string input, int cursor, Hashtable options, PowerShell powerShell)
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
                completions.Add(new CompletionResult("'a b" + Path.DirectorySeparatorChar + "'", "a b" + Path.DirectorySeparatorChar + "'", CompletionResultType.ProviderContainer, "a b" + Path.DirectorySeparatorChar + "'"));
                completions.Add(new CompletionResult("\"a b" + Path.DirectorySeparatorChar + "\"", "\"a b" + Path.DirectorySeparatorChar + "\"", CompletionResultType.ProviderContainer, "\"a b" + Path.DirectorySeparatorChar + "\""));
                break;
            case "invalid result 1":
                replacementIndex = -1;
                replacementLength = 1;
                completions.Add(new CompletionResult("result"));
                break;
            case "invalid result 2":
                replacementIndex = 0;
                replacementLength = -1;
                completions.Add(new CompletionResult("result"));
                break;
            case "invalid result 3":
                replacementIndex = int.MaxValue;
                replacementLength = 1;
                completions.Add(new CompletionResult("result"));
                break;
            case "invalid result 4":
                replacementIndex = 0;
                replacementLength = int.MaxValue;
                completions.Add(new CompletionResult("result"));
                break;
            case "ls -H":
                replacementIndex = cursor;
                replacementLength = 0;
                completions.Add(new CompletionResult("idden"));
                break;
            case "none":
                break;
            }

            return (CommandCompletion)ctor.Invoke(
                new object[] {completions, currentMatchIndex, replacementIndex, replacementLength});
        }
    }
}
