using System;
using System.Management.Automation;
using System.Text;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ClearScreen()
        {
            TestSetup(KeyMode.Emacs);

            Test("echo 1\necho 2\necho 3", Keys(
                "echo 1",
                _.Shift_Enter,
                "echo 2",
                _.Shift_Enter,
                "echo 3"));
            AssertCursorTopIs(3);
            Test("echo foo", Keys(
                "echo foo"
                ), resetCursor: false);
            AssertCursorTopIs(4);
            Test("echo zed", Keys(
                "echo zed",
                _.Ctrl_l,
                CheckThat(() => AssertCursorTopIs(0))
                ), resetCursor: false);
        }

        [SkippableFact]
        public void Render()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                "abc -def <#123#> \"hello $name\" 1 + (1-2)",
                _.Home,
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-def",
                        TokenClassification.None, " ",
                        TokenClassification.Comment, "<#123#>",
                        TokenClassification.None, " ",
                        TokenClassification.String, "\"hello ",
                        TokenClassification.Variable, "$name",
                        TokenClassification.String, "\"",
                        TokenClassification.None, " ",
                        TokenClassification.Number, "1",
                        TokenClassification.None, " + (",
                        TokenClassification.Number, "1",
                        TokenClassification.Operator, "-",
                        TokenClassification.Number, "2",
                        TokenClassification.None, ")")),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            // This tests for priority to highlight a command regardless of token kind and nested tokens potential to bleed the parent token color to the next token
            Test("", Keys(
                ". -abc def;. abc$name -def",
                _.Home,
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.None, ". ",
                        TokenClassification.Command, "-abc",
                        TokenClassification.None, " def;. ",
                        TokenClassification.Command, "abc",
                        TokenClassification.Variable, "$name",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-def")),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            // Additional test for priority to highlight a command regardless of token kind and nested tokens potential to bleed the parent token color to the next token
            Test("", Keys(
                ". ++ abc$name -def",
                _.Home,
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.None, ". ",
                        TokenClassification.Command, "++",
                        TokenClassification.None, " abc",
                        TokenClassification.Variable, "$name",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-def")),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            // test that rendering doesn't cause an exception in a potential missing "EOS" token case.
            // this case could be a moving target, if the PowerShell parser is changed such as to eliminate the case.
            Test("", Keys(
                "process $abc\\name | def",
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.Keyword, "process",
                        TokenClassification.None, " ",
                        TokenClassification.Variable, "$abc",
                        TokenClassification.None, "\\name | def")),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            Test("", Keys(
                "process out put",
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.Keyword, "process",
                        TokenClassification.None, " out put")),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            Test("", Keys(
                "\"$([int];\"_$(1+2)\")\"",
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.String, "\"",
                        TokenClassification.None, "$(",
                        TokenClassification.None, "[",
                        TokenClassification.Type, "int",
                        TokenClassification.None, "];",
                        TokenClassification.String, "\"_",
                        TokenClassification.None, "$(",
                        TokenClassification.Number, "1",
                        TokenClassification.Operator, "+",
                        TokenClassification.Number, "2",
                        TokenClassification.None, ")",
                        TokenClassification.String, "\"",
                        TokenClassification.None, ")",
                        TokenClassification.String, "\"")),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            Test("", Keys(
                "\"a $b c $d e\"",
                CheckThat(() =>
                    AssertScreenIs(1,
                        TokenClassification.String, "\"a ",
                        TokenClassification.Variable, "$b",
                        TokenClassification.String, " c ",
                        TokenClassification.Variable, "$d",
                        TokenClassification.String, " e\"")),
                _.Ctrl_c,
                InputAcceptedNow
                ));

            Test("{}", Keys(
                '{', _.Enter,
                _.Backspace, CheckThat(() => AssertScreenIs(2, TokenClassification.None, '{', NextLine)),
                '}'));

            _console.Clear();
            string promptLine = "PS> ";
            Test("\"\"", Keys(
                '"',
                CheckThat(() => AssertScreenIs(1,
                                   TokenClassification.None,
                                   promptLine.Substring(0, promptLine.IndexOf('>')),
                                   Tuple.Create(ConsoleColor.Red, ConsoleColor.DarkRed), "> ",
                                   TokenClassification.String, "\"")),
                '"'), prompt: promptLine);
        }

        [SkippableFact]
        public void MultiLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("d|\nd", Keys(
                "d|",
                _.Enter, CheckThat(() => AssertCursorTopIs(1)),
                'd'));

            // Make sure <ENTER> when input is incomplete actually puts a newline
            // wherever the cursor is.
            var continationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;
            Test("{\n\nd\n}", Keys(
                '{',
                _.Enter,      CheckThat(() => AssertCursorTopIs(1)),
                'd',
                _.Enter,      CheckThat(() => AssertCursorTopIs(2)),
                _.Home,
                _.RightArrow, CheckThat(() => AssertCursorLeftTopIs(1, 0)),
                _.Enter,      CheckThat(() => AssertCursorLeftTopIs(continationPrefixLength, 1)),
                _.End,        CheckThat(() => AssertCursorLeftTopIs(continationPrefixLength, 3)),
                '}'));

            // Make sure <ENTER> when input successfully parses accepts the input regardless
            // of where the cursor is, plus it moves the cursor to the end (so the new prompt
            // doesn't overwrite the end of the previous long/multi-line command line.)
            Test("{\n}", Keys(
                "{\n}",
                _.Home,
                _.Enter, CheckThat(() => AssertCursorLeftTopIs(0, 2))));
        }

        [SkippableFact]
        public void LongLine()
        {
            TestSetup(KeyMode.Cmd);

            var sb = new StringBuilder();
            sb.Append('"');
            sb.Append('z', _console.BufferWidth);
            sb.Append('"');

            var input = sb.ToString();
            Test(input, Keys(input));
        }

        [SkippableFact]
        public void InvokePrompt()
        {
            TestSetup(KeyMode.Cmd, new KeyHandler("Ctrl+z", PSConsoleReadLine.InvokePrompt));

            // Test dumb prompt that doesn't return anything.
            using (var ps = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                ps.AddScript(@"function prompt {}");
                ps.Invoke();
            }
            Test("dir", Keys(
                "dir", _.Ctrl_z,
                CheckThat(() => AssertScreenIs(1,
                    Tuple.Create(_console.ForegroundColor, _console.BackgroundColor), "PS>",
                    TokenClassification.Command, "dir"))));

            // Test a boring prompt function
            using (var ps = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                ps.AddScript(@"function prompt { 'PSREADLINE> ' }");
                ps.Invoke();
            }
            Test("dir", Keys(
                "dir", _.Ctrl_z,
                CheckThat(() => AssertScreenIs(1,
                    Tuple.Create(_console.ForegroundColor, _console.BackgroundColor), "PSREADLINE> ",
                    TokenClassification.Command, "dir"))));

            // Tricky prompt - writes to console directly with colors, uses ^H trick to eliminate trailng space.
            using (var ps = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                ps.AddCommand("New-Variable").AddParameter("Name", "__console").AddParameter("Value", _console).Invoke();
                ps.Commands.Clear();
                ps.AddScript(@"
function prompt {
    $fg = $__console.ForegroundColor
    $bg = $__console.BackgroundColor
    $__console.ForegroundColor = [ConsoleColor]::Blue
    $__console.BackgroundColor = [ConsoleColor]::Magenta
    $__console.Write('PSREADLINE>')
    $__console.ForegroundColor = $fg
    $__console.BackgroundColor = $bg
    return ' ' + ([char]8)
}");
                ps.Invoke();
            }
            Test("dir", Keys(
                "dir", _.Ctrl_z,
                CheckThat(() => AssertScreenIs(1,
                    Tuple.Create(ConsoleColor.Blue, ConsoleColor.Magenta), "PSREADLINE>",
                    TokenClassification.Command, "dir"))));
        }
    }
}
