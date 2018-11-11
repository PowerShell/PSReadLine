﻿using System;
using System.IO;
using System.Management.Automation.Language;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    using _ = Keys;

    public partial class ReadLine
    {
        [Fact]
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

        [Fact]
        public void ViMoveToLastLogicalLine_MustDing_ForEmptyLine()
        {
            const string buffer = "";
            var keys = new object[] {"", _.Escape, 'G',};
            ViJumpMustDing(buffer, keys);
        }

        [Fact]
        public void ViMoveToFirstLogicalLine_MustDing_ForEmptyLine()
        {
            const string buffer = "";
            var keys = new object[] {"", _.Escape, "gg",};
            ViJumpMustDing(buffer, keys);
        }

        [Fact]
        public void ViMoveToLastLogicalLine_MustDing_ForSingleLine()
        {
            const string buffer = "Ding";
            var keys = new object[] {"Ding", _.Escape, 'G',};
            ViJumpMustDing(buffer, keys);
        }

        [Fact]
        public void ViMoveToFirstLogicalLine_MustDing_ForSingleLine()
        {
            const string buffer = "Ding";
            var keys = new object[] {"Ding", _.Escape, "gg",};
            ViJumpMustDing(buffer, keys);
        }

        [Fact]
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

        [Fact]
        public void ViMoveToFirstNonBlankOfLogicalLine_NoOp_OnEmptyLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            const string buffer = "\"\n\n\"";

            Test(buffer, Keys(
                _.DQuote, _.Enter, _.Enter, _.DQuote, _.Escape, _.K,
                CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 0, 1)),
                _.Underbar, CheckThat(() => AssertCursorLeftTopIs(continuationPrefixLength + 0, 1))
            ));
        }

        [Fact]
        public void ViMoveToEndOfLine_NoOp_OnEmptyLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            const string buffer = "\"\n\n\"";

            Test(buffer, Keys(
                _.DQuote, _.Enter, _.Enter, _.DQuote, _.Escape, _.K,
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