using System;
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
        public void MenuCompletions_FilterByTyping()
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
        public void MenuCompletions_Navigation1()
        {
            // Test 'RightArrow' and 'LeftArrow' with the following menu:
            //   Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12
            //   Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13
            //   Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14

            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            Test("Get-Many0", Keys(
                "Get-Many", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Many0   ",
                    TokenClassification.None,
                    "Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "3", NextLine,
                    TokenClassification.None, "Get-Many0   ",
                    TokenClassification.Selection, "Get-Many3   ",
                    TokenClassification.None,
                    "Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "6", NextLine,
                    TokenClassification.None, "Get-Many0   Get-Many3   ",
                    TokenClassification.Selection, "Get-Many6   ",
                    TokenClassification.None, "Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "9", NextLine,
                    TokenClassification.None, "Get-Many0   Get-Many3   Get-Many6   ",
                    TokenClassification.Selection, "Get-Many9   ",
                    TokenClassification.None, "Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "12", NextLine,
                    TokenClassification.None, "Get-Many0   Get-Many3   Get-Many6   Get-Many9   ",
                    TokenClassification.Selection, "Get-Many12  ", NextLine,
                    TokenClassification.None,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "1", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    TokenClassification.Selection, "Get-Many1   ",
                    TokenClassification.None,
                    "Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "12", NextLine,
                    TokenClassification.None, "Get-Many0   Get-Many3   Get-Many6   Get-Many9   ",
                    TokenClassification.Selection, "Get-Many12  ", NextLine,
                    TokenClassification.None,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "9", NextLine,
                    TokenClassification.None, "Get-Many0   Get-Many3   Get-Many6   ",
                    TokenClassification.Selection, "Get-Many9   ",
                    TokenClassification.None, "Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "6", NextLine,
                    TokenClassification.None, "Get-Many0   Get-Many3   ",
                    TokenClassification.Selection, "Get-Many6   ",
                    TokenClassification.None, "Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Many0   ",
                    TokenClassification.None,
                    "Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "14", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  ",
                    TokenClassification.Selection, "Get-Many14  ", NextLine)),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Many0   ",
                    TokenClassification.None,
                    "Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.Enter,
                _.Enter
                ));
        }

        [SkippableFact]
        public void MenuCompletions_Navigation2()
        {
            // Test 'RightArrow' with the following menu:
            //   Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12
            //   Get-Less1   Get-Less4   Get-Less7   Get-Less10
            //   Get-Less2   Get-Less5   Get-Less8   Get-Less11

            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            Test("Get-Less0", Keys(
                "Get-Less", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Less0   ",
                    TokenClassification.None,
                    "Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.RightArrow, _.RightArrow, _.RightArrow, _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "12", NextLine,
                    TokenClassification.None, "Get-Less0   Get-Less3   Get-Less6   Get-Less9   ",
                    TokenClassification.Selection, "Get-Less12  ", NextLine,
                    TokenClassification.None,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "1", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    TokenClassification.Selection, "Get-Less1   ",
                    TokenClassification.None,
                    "Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.RightArrow, _.RightArrow, _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "10", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   ",
                    TokenClassification.Selection, "Get-Less10  ", NextLine,
                    TokenClassification.None,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "2", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    TokenClassification.Selection, "Get-Less2   ",
                    TokenClassification.None,
                    "Get-Less5   Get-Less8   Get-Less11")),
                _.RightArrow, _.RightArrow, _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "11", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   ",
                    TokenClassification.Selection, "Get-Less11  ")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Less0   ",
                    TokenClassification.None,
                    "Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.Enter,
                _.Enter
            ));
        }

        [SkippableFact]
        public void MenuCompletions_Navigation3()
        {
            // Test 'LeftArrow' with the following menu:
            //   Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12
            //   Get-Less1   Get-Less4   Get-Less7   Get-Less10
            //   Get-Less2   Get-Less5   Get-Less8   Get-Less11

            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            Test("Get-Less6", Keys(
                "Get-Less", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Less0   ",
                    TokenClassification.None,
                    "Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "11", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   ",
                    TokenClassification.Selection, "Get-Less11  ")),
                _.LeftArrow, _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "2", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    TokenClassification.Selection, "Get-Less2   ",
                    TokenClassification.None,
                    "Get-Less5   Get-Less8   Get-Less11")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "10", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   ",
                    TokenClassification.Selection, "Get-Less10  ", NextLine,
                    TokenClassification.None,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.LeftArrow, _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "1", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    TokenClassification.Selection, "Get-Less1   ",
                    TokenClassification.None,
                    "Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "12", NextLine,
                    TokenClassification.None, "Get-Less0   Get-Less3   Get-Less6   Get-Less9   ",
                    TokenClassification.Selection, "Get-Less12  ", NextLine,
                    TokenClassification.None,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.LeftArrow, _.LeftArrow,
                _.Enter,
                _.Enter
            ));
        }

        [SkippableFact]
        public void MenuCompletions_Navigation4()
        {
            // Test 'UpArrow' and 'DownArrow' with the following menu:
            //   Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12
            //   Get-Less1   Get-Less4   Get-Less7   Get-Less10
            //   Get-Less2   Get-Less5   Get-Less8   Get-Less11

            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            Test("Get-Less0", Keys(
                "Get-Less", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Less0   ",
                    TokenClassification.None,
                    "Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "1", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    TokenClassification.Selection, "Get-Less1   ",
                    TokenClassification.None,
                    "Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "2", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    TokenClassification.Selection, "Get-Less2   ",
                    TokenClassification.None,
                    "Get-Less5   Get-Less8   Get-Less11")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "3", NextLine,
                    TokenClassification.None, "Get-Less0   ",
                    TokenClassification.Selection, "Get-Less3   ",
                    TokenClassification.None,
                    "Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "2", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    TokenClassification.Selection, "Get-Less2   ",
                    TokenClassification.None,
                    "Get-Less5   Get-Less8   Get-Less11")),
                _.UpArrow, _.UpArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Less0   ",
                    TokenClassification.None,
                    "Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "12", NextLine,
                    TokenClassification.None, "Get-Less0   Get-Less3   Get-Less6   Get-Less9   ",
                    TokenClassification.Selection, "Get-Less12  ", NextLine,
                    TokenClassification.None,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "11", NextLine,
                    TokenClassification.None,
                    "Get-Less0   Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   ",
                    TokenClassification.Selection, "Get-Less11  ")),
                _.DownArrow, _.DownArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Less",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Less0   ",
                    TokenClassification.None,
                    "Get-Less3   Get-Less6   Get-Less9   Get-Less12", NextLine,
                    "Get-Less1   Get-Less4   Get-Less7   Get-Less10", NextLine,
                    "Get-Less2   Get-Less5   Get-Less8   Get-Less11")),
                _.Enter,
                _.Enter
            ));
        }

        [SkippableFact]
        public void MenuCompletions_Navigation5()
        {
            // Test 'UpArrow', 'DownArrow', 'LeftArrow', and 'RightArrow' with the following menu:
            //   Get-MockDynamicParameters  Get-Module

            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            Test("Get-MockDynamicParameters", Keys(
                "Get-Mo", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "dule", NextLine,
                    TokenClassification.None, "Get-MockDynamicParameters  ",
                    TokenClassification.Selection, "Get-Module                 ")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "dule", NextLine,
                    TokenClassification.None, "Get-MockDynamicParameters  ",
                    TokenClassification.Selection, "Get-Module                 ")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "dule", NextLine,
                    TokenClassification.None, "Get-MockDynamicParameters  ",
                    TokenClassification.Selection, "Get-Module                 ")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "dule", NextLine,
                    TokenClassification.None, "Get-MockDynamicParameters  ",
                    TokenClassification.Selection, "Get-Module                 ")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(2,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module")),
                _.Enter,
                _.Enter
            ));
        }

        [SkippableFact]
        public void MenuCompletions_Navigation6()
        {
            // Test 'UpArrow', 'DownArrow', 'LeftArrow', and 'RightArrow' with the following menu:
            //   Get-NewDynamicParameters  Get-NewStyle
            //   Get-NewIdea

            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            Test("Get-NewDynamicParameters", Keys(
                "Get-New", _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "DynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-NewDynamicParameters  ",
                    TokenClassification.None, "Get-NewStyle", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Style", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  ",
                    TokenClassification.Selection, "Get-NewStyle              ", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Idea", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  Get-NewStyle", NextLine,
                    TokenClassification.Selection, "Get-NewIdea               ")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "DynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-NewDynamicParameters  ",
                    TokenClassification.None, "Get-NewStyle", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Idea", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  Get-NewStyle", NextLine,
                    TokenClassification.Selection, "Get-NewIdea               ")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Style", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  ",
                    TokenClassification.Selection, "Get-NewStyle              ", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "DynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-NewDynamicParameters  ",
                    TokenClassification.None, "Get-NewStyle", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Idea", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  Get-NewStyle", NextLine,
                    TokenClassification.Selection, "Get-NewIdea               ")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Style", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  ",
                    TokenClassification.Selection, "Get-NewStyle              ", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "DynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-NewDynamicParameters  ",
                    TokenClassification.None, "Get-NewStyle", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Style", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  ",
                    TokenClassification.Selection, "Get-NewStyle              ", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "Idea", NextLine,
                    TokenClassification.None, "Get-NewDynamicParameters  Get-NewStyle", NextLine,
                    TokenClassification.Selection, "Get-NewIdea               ")),
                _.UpArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-New",
                    TokenClassification.Selection, "DynamicParameters", NextLine,
                    TokenClassification.Selection, "Get-NewDynamicParameters  ",
                    TokenClassification.None, "Get-NewStyle", NextLine,
                    TokenClassification.None, "Get-NewIdea")),
                _.Enter,
                _.Enter
            ));
        }

        [SkippableFact]
        public void MenuCompletions_Navigation7()
        {
            // Trigger the menu completion from the last line in the screen buffer, which will cause the screen
            // to scroll up. Then test 'DownArrow' and 'UpArrow' with the following menu to verify if scrolling
            // was handled correctly:
            //   Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12
            //   Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13
            //   Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14

            var basicScrollingConsole = new BasicScrollingConsole(keyboardLayout: _, width: 60, height: 10);
            TestSetup(basicScrollingConsole, KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            // Write 12 new-lines, so that the next input will be at the last line of the screen buffer.
            basicScrollingConsole.Write(new string('\n', 12));
            AssertCursorLeftTopIs(0, 9);

            Test("Get-Many0", Keys(
                "Get-Many",
                CheckThat(() => AssertCursorLeftTopIs(8, 9)),
                _.Ctrl_Spacebar,
                // Menu completion will trigger scrolling.
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Many0   ",
                    TokenClassification.None,
                    "Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.DownArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "1", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    TokenClassification.Selection, "Get-Many1   ",
                    TokenClassification.None,
                    "Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.DownArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "2", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    TokenClassification.Selection, "Get-Many2   ",
                    TokenClassification.None,
                    "Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.DownArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "3", NextLine,
                    TokenClassification.None, "Get-Many0   ",
                    TokenClassification.Selection, "Get-Many3   ",
                    TokenClassification.None,
                    "Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.DownArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "4", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   ",
                    TokenClassification.Selection, "Get-Many4   ",
                    TokenClassification.None,
                    "Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.DownArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "5", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   ",
                    TokenClassification.Selection, "Get-Many5   ",
                    TokenClassification.None,
                    "Get-Many8   Get-Many11  Get-Many14")),

                _.UpArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "4", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   ",
                    TokenClassification.Selection, "Get-Many4   ",
                    TokenClassification.None,
                    "Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.UpArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "3", NextLine,
                    TokenClassification.None, "Get-Many0   ",
                    TokenClassification.Selection, "Get-Many3   ",
                    TokenClassification.None,
                    "Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.UpArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "2", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    TokenClassification.Selection, "Get-Many2   ",
                    TokenClassification.None,
                    "Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.UpArrow, _.UpArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Many0   ",
                    TokenClassification.None,
                    "Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),

                _.LeftArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "14", NextLine,
                    TokenClassification.None,
                    "Get-Many0   Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  ",
                    TokenClassification.Selection, "Get-Many14  ", NextLine)),

                _.RightArrow,
                CheckThat(() => AssertCursorLeftTopIs(8, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Command, "Get-Many",
                    TokenClassification.Selection, "0", NextLine,
                    TokenClassification.Selection, "Get-Many0   ",
                    TokenClassification.None,
                    "Get-Many3   Get-Many6   Get-Many9   Get-Many12", NextLine,
                    "Get-Many1   Get-Many4   Get-Many7   Get-Many10  Get-Many13", NextLine,
                    "Get-Many2   Get-Many5   Get-Many8   Get-Many11  Get-Many14")),
                _.Enter,
                _.Enter),
                resetCursor: false);
        }

        [SkippableFact]
        public void MenuCompletions_ClearProperly()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            _console.Clear();
            int width = _console.BufferWidth;
            string placeholderCommand = new string('A', width - 12); // 12 = "Get-Module".Length + 2

            Test($"{placeholderCommand};Get-Module", Keys(
                placeholderCommand, ';',
                "Get-Mo", _.Ctrl_Spacebar,
                // At this point, the editing line buffer takes 2 physical lines.
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, placeholderCommand,
                    TokenClassification.None, ';',
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters",
                    NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module                 ",
                    NextLine,
                    NextLine)),
                _.RightArrow,
                // Navigating to the next item will cause the editing line to fit in
                // one physical line, so the new menu is moved up and lines from the
                // previous menu need to be properly cleared.
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, placeholderCommand,
                    TokenClassification.None, ';',
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "dule",
                    NextLine,
                    TokenClassification.None, "Get-MockDynamicParameters  ",
                    TokenClassification.Selection, "Get-Module                 ",
                    NextLine,
                    NextLine)),
                _.LeftArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, placeholderCommand,
                    TokenClassification.None, ';',
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters",
                    NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module                 ",
                    NextLine,
                    NextLine)),
                _.DownArrow,
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, placeholderCommand,
                    TokenClassification.None, ';',
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "dule",
                    NextLine,
                    TokenClassification.None, "Get-MockDynamicParameters  ",
                    TokenClassification.Selection, "Get-Module                 ",
                    NextLine,
                    NextLine)),
                _.Enter,
                _.Enter
                ));
        }

        [SkippableFact]
        public void MenuCompletions_WorkWithListView()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            int listWidth = CheckWindowSize();
            var emphasisColors = Tuple.Create(PSConsoleReadLineOptions.DefaultEmphasisColor, _console.BackgroundColor);
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.ListView);

            _console.Clear();
            SetHistory("Get-Mocha -AddMilk -AddSugur -ExtraCup", "Get-MoreBook -Kind Fiction -FlatCover");

            Test("Get-Module", Keys(
                "Get-Mo",
                CheckThat(() => AssertScreenIs(3,
                    TokenClassification.Command, "Get-Mo",
                    NextLine,
                    TokenClassification.ListPrediction, '>',
                    TokenClassification.None, ' ',
                    emphasisColors, "Get-Mo",
                    TokenClassification.None, "reBook -Kind Fiction -FlatCover",
                    TokenClassification.None, new string(' ', listWidth - 48), // 48 is the length of '> Get-MoreBook -Kind Fiction -FlatCover' plus '[History]'.
                    TokenClassification.None, '[',
                    TokenClassification.ListPrediction, "History",
                    TokenClassification.None, ']',
                    NextLine,
                    TokenClassification.ListPrediction, '>',
                    TokenClassification.None, ' ',
                    emphasisColors, "Get-Mo",
                    TokenClassification.None, "cha -AddMilk -AddSugur -ExtraCup",
                    TokenClassification.None, new string(' ', listWidth - 49), // 49 is the length of '> Get-Mocha -AddMilk -AddSugur -ExtraCup' plus '[History]'.
                    TokenClassification.None, '[',
                    TokenClassification.ListPrediction, "History",
                    TokenClassification.None, ']')),
                _.Ctrl_Spacebar,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "ckDynamicParameters",
                    NextLine,
                    TokenClassification.Selection, "Get-MockDynamicParameters  ",
                    TokenClassification.None, "Get-Module",
                    NextLine,
                    NextLine,
                    NextLine)),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-Mo",
                    TokenClassification.Selection, "dule",
                    NextLine,
                    TokenClassification.None, "Get-MockDynamicParameters  ",
                    TokenClassification.Selection, "Get-Module                 ",
                    NextLine,
                    NextLine,
                    NextLine)),
                _.Enter,
                _.Enter
            ));
        }

        [SkippableFact]
        public void MenuCompletions_HandleScrolling1()
        {
            // This test case covers the fix to https://github.com/PowerShell/PSReadLine/issues/2928.
            var basicScrollingConsole = new BasicScrollingConsole(keyboardLayout: _, width: 133, height: 10);
            TestSetup(basicScrollingConsole, KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            // Write 12 new-lines, so that the next input will be at the last line of the screen buffer.
            basicScrollingConsole.Write(new string('\n', 12));
            AssertCursorLeftTopIs(0, 9);

            // Input length: 131; BufferWidth: 133. MenuComplete on '[reg' will first get '[regex', which makes the line
            // fit exactly the whole buffer width, and thus will cause screen scrolling when the current line is at the
            // last line of the screen buffer.
            string input = @"$instMods = 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA' -replace [reg] $env:HOMEPATH ,""`$env:HOMEPATH""";

            Test(input, Keys(
                input,
                CheckThat(() => AssertCursorLeftTopIs(131, 9)),
                _.Ctrl_LeftArrow, _.Ctrl_LeftArrow, _.Ctrl_LeftArrow, _.Ctrl_LeftArrow,
                _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertCursorLeftTopIs(98, 9)),
                _.Ctrl_Spacebar,
                CheckThat(() => AssertCursorLeftTopIs(98, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Variable, "$instMods",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, '=',
                    TokenClassification.None, ' ',
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "reg",
                    TokenClassification.Selection, "ex",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, @"""`$env:HOMEPATH""",
                    TokenClassification.None, _emptyLine,
                    TokenClassification.Selection, "Regex                                    ",
                    TokenClassification.None, "RegionInfo                               RegisterPSSessionConfigurationCommand", NextLine,
                    TokenClassification.None, "RegexCompilationInfo                     RegisterArgumentCompleterCommand         RegistryProviderSetItemDynamicParameter")),

                _.RightArrow,
                CheckThat(() => AssertCursorLeftTopIs(119, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Variable, "$instMods",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, '=',
                    TokenClassification.None, ' ',
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "System.Globalization.Reg",
                    TokenClassification.Selection, "ionInfo",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, @"""`$env:HOMEPATH""",
                    NextLine,
                    TokenClassification.None, "Regex                                    ",
                    TokenClassification.Selection, "RegionInfo                               ",
                    TokenClassification.None, "RegisterPSSessionConfigurationCommand", NextLine,
                    TokenClassification.None, "RegexCompilationInfo                     RegisterArgumentCompleterCommand         RegistryProviderSetItemDynamicParameter")),

                _.Escape,
                CheckThat(() => AssertCursorLeftTopIs(98, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.Variable, "$instMods",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, '=',
                    TokenClassification.None, ' ',
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "reg",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, @"""`$env:HOMEPATH""",
                    NextLine,
                    NextLine,
                    NextLine,
                    NextLine)),

                _.Enter),
            resetCursor: false);
        }

        [SkippableFact]
        public void MenuCompletions_HandleScrolling2()
        {
            // This test case covers the fix to https://github.com/PowerShell/PSReadLine/issues/2948.
            var basicScrollingConsole = new BasicScrollingConsole(keyboardLayout: _, width: 133, height: 10);
            TestSetup(basicScrollingConsole, KeyMode.Cmd, new KeyHandler("Ctrl+Spacebar", PSConsoleReadLine.MenuComplete));

            // Write 12 new-lines, so that the next input will be at the last line of the screen buffer.
            basicScrollingConsole.Write(new string('\n', 12));
            AssertCursorLeftTopIs(0, 9);

            // Input length: 69; BufferWidth: 133.
            // MenuComplete on '[reg' contains one entry that will make the line fit exactly the whole buffer width,
            // and thus will cause screen scrolling. But that entry is not the first one in the menu.
            string input = @"'AAAAAAAAAAAAAAAAAAAAA' -replace [reg] $env:HOMEPATH ,'$env:HOMEPATH'";

            Test(input, Keys(
                input,
                CheckThat(() => AssertCursorLeftTopIs(69, 9)),
                _.Ctrl_LeftArrow, _.Ctrl_LeftArrow, _.Ctrl_LeftArrow, _.Ctrl_LeftArrow,
                _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertCursorLeftTopIs(37, 9)),
                _.Ctrl_Spacebar,
                CheckThat(() => AssertCursorLeftTopIs(37, 7)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 3,
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "reg",
                    TokenClassification.Selection, "ex",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, "'$env:HOMEPATH'",
                    NextLine,
                    TokenClassification.Selection, "Regex                                    ",
                    TokenClassification.None, "RegionInfo                               RegisterPSSessionConfigurationCommand", NextLine,
                    TokenClassification.None, "RegexCompilationInfo                     RegisterArgumentCompleterCommand         RegistryProviderSetItemDynamicParameter")),

                _.RightArrow,
                CheckThat(() => AssertCursorLeftTopIs(58, 7)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 3,
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "System.Globalization.Reg",
                    TokenClassification.Selection, "ionInfo",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, "'$env:HOMEPATH'",
                    NextLine,
                    TokenClassification.None, "Regex                                    ",
                    TokenClassification.Selection, "RegionInfo                               ",
                    TokenClassification.None, "RegisterPSSessionConfigurationCommand", NextLine,
                    TokenClassification.None, "RegexCompilationInfo                     RegisterArgumentCompleterCommand         RegistryProviderSetItemDynamicParameter")),

                _.RightArrow,
                CheckThat(() => AssertCursorLeftTopIs(67, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "Microsoft.PowerShell.Commands.Reg",
                    TokenClassification.Selection, "isterPSSessionConfigurationCommand",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, "'$env:HOMEPATH'",
                    TokenClassification.None, _emptyLine,
                    TokenClassification.None, "Regex                                    RegionInfo                               ",
                    TokenClassification.Selection, "RegisterPSSessionConfigurationCommand    ", NextLine,
                    TokenClassification.None, "RegexCompilationInfo                     RegisterArgumentCompleterCommand         RegistryProviderSetItemDynamicParameter")),

                _.RightArrow,
                CheckThat(() => AssertCursorLeftTopIs(49, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "System.Text.Reg",
                    TokenClassification.Selection, "ularExpressions.RegexCompilationInfo",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, "'$env:HOMEPATH'",
                    NextLine,
                    TokenClassification.None, "Regex                                    RegionInfo                               RegisterPSSessionConfigurationCommand",
                    NextLine,
                    TokenClassification.Selection, "RegexCompilationInfo                     ",
                    TokenClassification.None, "RegisterArgumentCompleterCommand         RegistryProviderSetItemDynamicParameter",
                    NextLine,
                    NextLine)),

                _.RightArrow,
                CheckThat(() => AssertCursorLeftTopIs(66, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "System.Management.Automation.Reg",
                    TokenClassification.Selection, "isterArgumentCompleterCommand",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, "'$env:HOMEPATH'",
                    NextLine,
                    TokenClassification.None, "Regex                                    RegionInfo                               RegisterPSSessionConfigurationCommand",
                    NextLine,
                    TokenClassification.None, "RegexCompilationInfo                     ",
                    TokenClassification.Selection, "RegisterArgumentCompleterCommand         ",
                    TokenClassification.None, "RegistryProviderSetItemDynamicParameter",
                    NextLine,
                    NextLine)),

                _.RightArrow,
                CheckThat(() => AssertCursorLeftTopIs(67, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "Microsoft.PowerShell.Commands.Reg",
                    TokenClassification.Selection, "istryProviderSetItemDynamicParameter",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, "'$env:HOMEPATH'",
                    NextLine,
                    TokenClassification.None, "Regex                                    RegionInfo                               RegisterPSSessionConfigurationCommand",
                    NextLine,
                    TokenClassification.None, "RegexCompilationInfo                     RegisterArgumentCompleterCommand         ",
                    TokenClassification.Selection, "RegistryProviderSetItemDynamicParameter  ",
                    NextLine)),

                _.Escape,
                CheckThat(() => AssertCursorLeftTopIs(37, 6)),
                CheckThat(() => AssertScreenIs(top: 12, lines: 4,
                    TokenClassification.String, "'AAAAAAAAAAAAAAAAAAAAA'",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, "-replace",
                    TokenClassification.None, " [",
                    TokenClassification.Type, "reg",
                    TokenClassification.None, "] ",
                    TokenClassification.Variable, "$env:HOMEPATH",
                    TokenClassification.None, ' ',
                    TokenClassification.Operator, ',',
                    TokenClassification.String, "'$env:HOMEPATH'",
                    NextLine,
                    NextLine,
                    NextLine,
                    NextLine)),

                _.Enter),
            resetCursor: false);
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
            case "Get-Less":
                replacementIndex = 0;
                replacementLength = 8;
                for (int i = 0; i < 13; i++)
                {
                    completions.Add(new CompletionResult("Get-Less" + i));
                }
                break;
            case "Get-New":
                replacementIndex = 0;
                replacementLength = 7;
                completions.Add(new CompletionResult("Get-NewDynamicParameters"));
                completions.Add(new CompletionResult("Get-NewIdea"));
                completions.Add(new CompletionResult("Get-NewStyle"));
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

            default:
                if (input.EndsWith("Get-Mo", StringComparison.OrdinalIgnoreCase))
                {
                    replacementIndex = input.IndexOf("Get-Mo", StringComparison.OrdinalIgnoreCase);
                    replacementLength = 6;
                    completions.Add(new CompletionResult("Get-MockDynamicParameters"));
                    completions.Add(new CompletionResult("Get-Module"));
                    break;
                }

                int index = input.IndexOf("[reg]");
                if (index > 0 && index + 4 == cursor)
                {
                    // cursor is pointing at ']'.
                    replacementIndex = index + 1;
                    replacementLength = 3;
                    completions.Add(
                        new CompletionResult(
                            "regex",
                            "Regex",
                            CompletionResultType.Type,
                            "regex"));
                    completions.Add(
                        new CompletionResult(
                            "System.Text.RegularExpressions.RegexCompilationInfo",
                            "RegexCompilationInfo",
                            CompletionResultType.Type,
                            "System.Text.RegularExpressions.RegexCompilationInfo"));
                    completions.Add(
                        new CompletionResult(
                            "System.Globalization.RegionInfo",
                            "RegionInfo",
                            CompletionResultType.Type,
                            "System.Globalization.RegionInfo"));
                    completions.Add(
                        new CompletionResult(
                            "System.Management.Automation.RegisterArgumentCompleterCommand",
                            "RegisterArgumentCompleterCommand",
                            CompletionResultType.Type,
                            "System.Management.Automation.RegisterArgumentCompleterCommand"));
                    completions.Add(
                        new CompletionResult(
                            "Microsoft.PowerShell.Commands.RegisterPSSessionConfigurationCommand",
                            "RegisterPSSessionConfigurationCommand",
                            CompletionResultType.Type,
                            "Microsoft.PowerShell.Commands.RegisterPSSessionConfigurationCommand"));
                    completions.Add(
                        new CompletionResult(
                            "Microsoft.PowerShell.Commands.RegistryProviderSetItemDynamicParameter",
                            "RegistryProviderSetItemDynamicParameter",
                            CompletionResultType.Type,
                            "Microsoft.PowerShell.Commands.RegistryProviderSetItemDynamicParameter"));
                    break;
                }

                break;
            }

            //new CommandCompletion(completions, currentMatchIndex, replacementIndex, replacementLength);
            return (CommandCompletion)ctor.Invoke(
                new object[] {completions, currentMatchIndex, replacementIndex, replacementLength});
        }
    }
}
