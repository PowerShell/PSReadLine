using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ViMoveToFirstLogicalLineThenJumpToLastLogicalLine()
        {
            TestSetup(KeyMode.Vi);

            const string buffer = "\"Multiline buffer\n containing an empty line\n\nand text aligned on the left\n\"";

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test(buffer, Keys(
                _.DQuote, "Multiline buffer", _.Enter,
                " containing an empty line", _.Enter,
                _.Enter,
                "and text aligned on the left", _.Enter,
                _.DQuote,
                _.Escape, CheckThat(() => AssertCursorTopIs(4)),
                "gg", CheckThat(() => AssertCursorLeftTopIs(0, 0)),
                'G', CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength, 4))
            ));
        }

        [SkippableFact]
        public void ViMoveToLastLogicalLine_MustDing_ForEmptyLine()
        {
            const string buffer = "";
            var keys = new object[] {"", _.Escape, 'G',};
            ViJumpMustDing(buffer, keys);
        }

        [SkippableFact]
        public void ViMoveToFirstLogicalLine_MustDing_ForEmptyLine()
        {
            const string buffer = "";
            var keys = new object[] {"", _.Escape, "gg",};
            ViJumpMustDing(buffer, keys);
        }

        [SkippableFact]
        public void ViMoveToLastLogicalLine_MustDing_ForSingleLine()
        {
            const string buffer = "Ding";
            var keys = new object[] {"Ding", _.Escape, 'G',};
            ViJumpMustDing(buffer, keys);
        }

        [SkippableFact]
        public void ViMoveToFirstLogicalLine_MustDing_ForSingleLine()
        {
            const string buffer = "Ding";
            var keys = new object[] {"Ding", _.Escape, "gg",};
            ViJumpMustDing(buffer, keys);
        }

        private void ViJumpMustDing(string expectedResult, params object[] keys)
        {
            TestSetup(KeyMode.Vi);
            TestMustDing(expectedResult, Keys(keys));
        }
    }
}