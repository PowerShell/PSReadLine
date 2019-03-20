using System.Linq;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void BackwardWord()
        {
            TestSetup(KeyMode.Cmd);

            const string input = "  aaa  bbb  ccc  ";
            Test(input, Keys(
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(12)),
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(0))));

            // Test with digit arguments
            Test(input, Keys(
                _.Alt_3, _.Alt_3, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Alt_2, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.Alt_1, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_0, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_Minus, _.Alt_1, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(7)),
                _.Alt_2, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(0))));
        }

        [SkippableFact]
        public void EmacsBackwardWord()
        {
            TestSetup(KeyMode.Emacs);

            const string input = "  aaa  bbb  ccc  ";
            Test(input, Keys(
                _.Alt_b, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Alt_b, CheckThat(() => AssertCursorLeftIs(12)),
                _.Alt_b, CheckThat(() => AssertCursorLeftIs(7)),
                _.Alt_b, CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_b, CheckThat(() => AssertCursorLeftIs(0))));

            // Test with digit arguments
            Test(input, Keys(
                _.Alt_2, _.Alt_b, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Alt_1, _.Alt_b, CheckThat(() => AssertCursorLeftIs(12)),
                _.Alt_2, _.Alt_b, CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_Minus, _.Alt_2, _.Alt_b, CheckThat(() => AssertCursorLeftIs(10)),
                _.Alt_4, _.Alt_b, CheckThat(() => AssertCursorLeftIs(0))));
        }

        [SkippableFact]
        public void ForwardWord()
        {
            TestSetup(KeyMode.Emacs);

            Test("", Keys(_.Alt_f));

            var input = "echo   abc  def  ghi  jkl";
            Test(input, Keys(
                 input, _.Home, _.Alt_f,
                 CheckThat(() => AssertCursorLeftIs(4)),
                 _.RightArrow, _.Alt_f,
                 CheckThat(() => AssertCursorLeftIs(10))
                ));

            // Test with digit arguments
            Test(input, Keys(
                input, _.Home,
                _.Alt_4, _.Alt_f, CheckThat(() => AssertCursorLeftIs(20)),
                _.Alt_3, _.Alt_Minus, _.Alt_f, CheckThat(() => AssertCursorLeftIs(7))));
        }

        [SkippableFact]
        public void ShellBackwardWord()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+LeftArrow", PSConsoleReadLine.ShellBackwardWord));

            Test("", Keys(_.Ctrl_LeftArrow));

            var input = "echo a\\b[c]:dd \"a $b c $d e\" 42";
            Test(input, Keys(
                input,
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(29)),
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(15)),
                _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(5))));

            // Test with digit arguments
            Test(input, Keys(
                input,
                _.Alt_3, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.Alt_1, _.Alt_Minus, _.Ctrl_LeftArrow, CheckThat(() => AssertCursorLeftIs(15))));
        }

        [SkippableFact]
        public void ShellNextWord()
        {
            TestSetup(KeyMode.Cmd,
                new KeyHandler("Ctrl+RightArrow", PSConsoleReadLine.ShellNextWord));

            Test("aaa  bbb  ccc", Keys(
                _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(0)),
                "aaa  bbb  ccc",
                _.Home, CheckThat(() => AssertCursorLeftIs(0)),
                _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow, _.LeftArrow, _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.LeftArrow, _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(5)),
                _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(10)),
                _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(13))));

            var input = "echo a\\b[c]:dd \"a $b c $d e\" 42";
            Test(input, Keys(
                input, _.Home, Enumerable.Repeat(_.RightArrow, 5),
                _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(15)),
                _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(18)),
                _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(23))));

            // Test with digit arguments
            Test(input, Keys(
                input, _.Home,
                _.Alt_3, _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(18)),
                _.Home,
                _.Alt_4, _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(23)),
                _.Alt_Minus, _.Alt_3, _.Ctrl_RightArrow, CheckThat(() => AssertCursorLeftIs(5))
                ));
        }

        [SkippableFact]
        public void ShellForwardWord()
        {
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Alt+f", PSConsoleReadLine.ShellForwardWord));

            string input = "aaa  bbb  ccc";
            Test(input, Keys(
                _.Alt_f, CheckThat(() => AssertCursorLeftIs(0)),
                input,
                _.Ctrl_a, CheckThat(() => AssertCursorLeftIs(0)),
                _.Alt_f, CheckThat(() => AssertCursorLeftIs(3)),
                _.Alt_f, CheckThat(() => AssertCursorLeftIs(8)),
                _.Alt_f, CheckThat(() => AssertCursorLeftIs(13))));

            input = "echo a\\b[c]:dd \"a $b c $d e\" 42";
            Test(input, Keys(
                input, _.Home,
                Enumerable.Repeat(_.RightArrow, 5),
                _.Alt_f, CheckThat(() => AssertCursorLeftIs(14)),
                _.Alt_f, CheckThat(() => AssertCursorLeftIs(28)),
                _.Alt_f, CheckThat(() => AssertCursorLeftIs(31))));

            // Test with digit arguments
            Test(input, Keys(
                input, _.Home,
                _.Alt_2, _.Alt_f, CheckThat(() => AssertCursorLeftIs(14)),
                _.RightArrow, _.Alt_2, _.Alt_f, CheckThat(() => AssertCursorLeftIs(25)),
                _.Alt_Minus, _.Alt_4, _.Alt_f, CheckThat(() => AssertCursorLeftIs(5))));
        }
    }
}
