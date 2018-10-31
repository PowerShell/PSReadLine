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
        public void ViMoveToFirstLine()
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
    }
}