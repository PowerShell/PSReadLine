using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ViMoveToLine_DesiredColumn()
        {
            TestSetup(KeyMode.Vi);

            const string buffer = "\"\n12345\n1234\n123\n12\n1\n\"";

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test(buffer, Keys(
                _.DQuote, _.Enter,
                "12345", _.Enter,
                "1234", _.Enter,
                "123", _.Enter,
                "12", _.Enter,
                "1", _.Enter,
                _.DQuote,
                _.Escape,

                // move to second line at column 4
                "ggj3l", CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 3)),
                // moving down on shorter lines will position the cursor at the end of each logical line
                _.j, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 3)),
                _.j, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 2)),
                // moving back up will position the cursor at the end of shorter lines or at the desired column number
                _.k, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 3)),
                _.k, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 3)),

                // move at end of line (column 5)
                _.Dollar, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 4)),
                // moving down on shorter lines will position the cursor at the end of each logical line
                _.j, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 3)),
                _.j, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 2)),
                // moving back up will position the cursor at the end of each logical line
                _.k, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 3)),
                _.k, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 4))
            ));
        }


        [SkippableFact]
        public void ViBackwardChar()
        {
            TestSetup(KeyMode.Vi);

            const string buffer = "\"\nline2\nline3\n\"";

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test(buffer, Keys(
                _.DQuote, _.Enter,
                "line2", _.Enter,
                "line3", _.Enter,
                _.DQuote,
                _.Escape,
                _.k, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 0)),
                // move left
                _.h, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 0)),
                _.l, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 1)),
                "2h", CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 0))
            ));
        }

        [SkippableFact]
        public void ViForwardChar()
        {
            TestSetup(KeyMode.Vi);

            const string buffer = "\"\nline2\nline3\n\"";

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test(buffer, Keys(
                _.DQuote, _.Enter,
                "line2", _.Enter,
                "line3", _.Enter,
                _.DQuote,
                _.Escape,
                _.k, _.k, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 0)),
                // move right
                _.l, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 1)),
                "10l", CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 4))
            ));
        }

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

        [SkippableFact]
        public void ViMoveToFirstNonBlankOfLogicalLineThenJumpToEndOfLogicalLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            const string buffer = "\"\n  line\"";

            Test(buffer, Keys(
                _.DQuote, _.Enter, "  line", _.DQuote, _.Escape, CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 6)),
                _.Underbar, CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 2, 1)),
                _.Dollar, CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 6, 1)),
                // also works forward
                '0', CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength, 1)),
                _.Underbar, CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 2, 1))
                ));
        }

        [SkippableFact]
        public void ViMoveToFirstNonBlankOfLogicalLine_NoOp_OnEmptyLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            const string buffer = "\"\n\n\"";

            Test(buffer, Keys(
                _.DQuote, _.Enter, _.Enter, _.DQuote, _.Escape, _.k,
                CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 0, 1)),
                _.Underbar, CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 0, 1))
            ));
        }

        [SkippableFact]
        public void ViMoveToEndOfLine_NoOp_OnEmptyLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            const string buffer = "\"\n\n\"";

            Test(buffer, Keys(
                _.DQuote, _.Enter, _.Enter, _.DQuote, _.Escape, _.k,
                CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 0, 1)),
                _.Dollar, CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 0, 1))
            ));
        }

        private void ViJumpMustDing(string expectedResult, params object[] keys)
        {
            TestSetup(KeyMode.Vi);
            TestMustDing(expectedResult, Keys(keys));
        }
    }
}
