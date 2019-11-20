using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ViPasteAfterDeleteChar()
        {
            TestSetup(KeyMode.Vi);

            Test("abcd", Keys(
                "abcd", _.Escape,
                'x', CheckThat(() => AssertLineIs("abc")), CheckThat(() => AssertCursorLeftIs(2)),
                'p', CheckThat(() => AssertLineIs("abcd")), CheckThat(() => AssertCursorLeftIs(3)),
                'P', CheckThat(() => AssertLineIs("abcdd")), CheckThat(() => AssertCursorLeftIs(3)),
                'u'
                ));

            Test("abcd", Keys(
                "abcd", _.Escape,
                "h2x", CheckThat(() => AssertLineIs("ab")), CheckThat(() => AssertCursorLeftIs(1)),
                'p', CheckThat(() => AssertLineIs("abcd")), CheckThat(() => AssertCursorLeftIs(3)),
                'P', CheckThat(() => AssertLineIs("abccdd")), CheckThat(() => AssertCursorLeftIs(4)),
                'u'
                ));

            Test("abcd", Keys(
                "abcd", _.Escape,
                'X', CheckThat(() => AssertLineIs("abd")), CheckThat(() => AssertCursorLeftIs(2)),
                'p', CheckThat(() => AssertLineIs("abdc")), CheckThat(() => AssertCursorLeftIs(3)),
                'P', CheckThat(() => AssertLineIs("abdcc")), CheckThat(() => AssertCursorLeftIs(3)),
                "uuu"
                ));

            Test("abcd", Keys(
                "abcd", _.Escape,
                "2X", CheckThat(() => AssertLineIs("ad")), CheckThat(() => AssertCursorLeftIs(1)),
                'p', CheckThat(() => AssertLineIs("adbc")), CheckThat(() => AssertCursorLeftIs(3)),
                'P', CheckThat(() => AssertLineIs("adbbcc")), CheckThat(() => AssertCursorLeftIs(4)),
                "uuu"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterDelete()
        {
            TestSetup(KeyMode.Vi);

            Test("abcd", Keys(
                "abcd", _.Escape,
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                'p', CheckThat(() => AssertLineIs("abcd")), CheckThat(() => AssertCursorLeftIs(3)),
                'P', CheckThat(() => AssertLineIs("abcabcdd")), CheckThat(() => AssertCursorLeftIs(6)),
                "uuu"
                ));

            Test("abcd", Keys(
                "abcd", _.Escape,
                "hd", _.Dollar, CheckThat(() => AssertLineIs("ab")), CheckThat(() => AssertCursorLeftIs(1)),
                "hp", CheckThat(() => AssertLineIs("acdb")), CheckThat(() => AssertCursorLeftIs(2)),
                'P', CheckThat(() => AssertLineIs("accddb")), CheckThat(() => AssertCursorLeftIs(3)),
                "uuu"
                ));

            Test("abcd", Keys(
                "abcd", _.Escape,
                "dh", CheckThat(() => AssertLineIs("abd")), CheckThat(() => AssertCursorLeftIs(2)),
                'p', CheckThat(() => AssertLineIs("abdc")), CheckThat(() => AssertCursorLeftIs(3)),
                'P', CheckThat(() => AssertLineIs("abdcc")), CheckThat(() => AssertCursorLeftIs(3)),
                "uuu"
                ));

            Test("abcd", Keys(
                "abcd", _.Escape,
                "0dl", CheckThat(() => AssertLineIs("bcd")), CheckThat(() => AssertCursorLeftIs(0)),
                'p', CheckThat(() => AssertLineIs("bacd")), CheckThat(() => AssertCursorLeftIs(1)),
                _.Dollar, 'P', CheckThat(() => AssertLineIs("bacad")), CheckThat(() => AssertCursorLeftIs(3)),
                "uuu"
                ));

            Test("abcdef", Keys(
                "abcdef", _.Escape,
                "hhd0", CheckThat(() => AssertLineIs("def")), CheckThat(() => AssertCursorLeftIs(0)),
                'p', CheckThat(() => AssertLineIs("dabcef")), CheckThat(() => AssertCursorLeftIs(3)),
                'P', CheckThat(() => AssertLineIs("dababccef")), CheckThat(() => AssertCursorLeftIs(5)),
                "uuu"
                ));

            Test(" abc def", Keys(
                " abc def", _.Escape,
                "bd", _.Uphat, CheckThat(() => AssertLineIs(" def")), CheckThat(() => AssertCursorLeftIs(1)),
                'p', CheckThat(() => AssertLineIs(" dabc ef")), CheckThat(() => AssertCursorLeftIs(5)),
                'P', CheckThat(() => AssertLineIs(" dabcabc  ef")), CheckThat(() => AssertCursorLeftIs(8)),
                "uuu"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterDeleteBraces()
        {
            TestSetup(KeyMode.Vi);

            Test("abc(def)ghi", Keys(
                "abc(def)ghi", _.Escape,
                "hhhd", _.Percent, CheckThat(() => AssertLineIs("abcghi")),
                'p', CheckThat(() => AssertLineIs("abcg(def)hi")), CheckThat(() => AssertCursorLeftIs(8)),
                'P', CheckThat(() => AssertLineIs("abcg(def(def))hi")), CheckThat(() => AssertCursorLeftIs(12)),
                "uuu"
                ));

            Test("abc{def}ghi", Keys(
                "abc{def}ghi", _.Escape,
                "hhhd", _.Percent, CheckThat(() => AssertLineIs("abcghi")),
                'p', CheckThat(() => AssertLineIs("abcg{def}hi")), CheckThat(() => AssertCursorLeftIs(8)),
                'P', CheckThat(() => AssertLineIs("abcg{def{def}}hi")), CheckThat(() => AssertCursorLeftIs(12)),
                "uuu"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterDeleteWord()
        {
            TestSetup(KeyMode.Vi);

            Test("abc def", Keys(
                "abc def", _.Escape,
                _.Ctrl_w, CheckThat(() => AssertLineIs("abc f")), CheckThat(() => AssertCursorLeftIs(4)),
                'p', CheckThat(() => AssertLineIs("abc fde")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc fddee")), CheckThat(() => AssertCursorLeftIs(7)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                _.Ctrl_u, CheckThat(() => AssertLineIs("f")), CheckThat(() => AssertCursorLeftIs(0)),
                'p', CheckThat(() => AssertLineIs("fabc de")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("fabc dabc dee")), CheckThat(() => AssertCursorLeftIs(11)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                "bdw", CheckThat(() => AssertLineIs("abc ")), CheckThat(() => AssertCursorLeftIs(3)),
                'p', CheckThat(() => AssertLineIs("abc def")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc dedeff")), CheckThat(() => AssertCursorLeftIs(8)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                "db", CheckThat(() => AssertLineIs("abc f")), CheckThat(() => AssertCursorLeftIs(4)),
                'p', CheckThat(() => AssertLineIs("abc fde")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc fddee")), CheckThat(() => AssertCursorLeftIs(7)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                "bdW", CheckThat(() => AssertLineIs("abc ")), CheckThat(() => AssertCursorLeftIs(3)),
                'p', CheckThat(() => AssertLineIs("abc def")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc dedeff")), CheckThat(() => AssertCursorLeftIs(8)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                "dB", CheckThat(() => AssertLineIs("abc f")), CheckThat(() => AssertCursorLeftIs(4)),
                'p', CheckThat(() => AssertLineIs("abc fde")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc fddee")), CheckThat(() => AssertCursorLeftIs(7)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                "bde", CheckThat(() => AssertLineIs("abc ")), CheckThat(() => AssertCursorLeftIs(3)),
                'p', CheckThat(() => AssertLineIs("abc def")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc dedeff")), CheckThat(() => AssertCursorLeftIs(8)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                "bdE", CheckThat(() => AssertLineIs("abc ")), CheckThat(() => AssertCursorLeftIs(3)),
                'p', CheckThat(() => AssertLineIs("abc def")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc dedeff")), CheckThat(() => AssertCursorLeftIs(8)),
                "uuu"
                ));
        }

        [SkippableFact()]
        public void ViDeleteLine_EmptyBuffer_Defect1197()
        {
            TestSetup(KeyMode.Vi);

            Test("", Keys(
                _.Escape, "dd", CheckThat(() => AssertLineIs(""))
            ));
        }

        [SkippableFact]
        public void ViPasteAfterDeleteLine()
        {
            TestSetup(KeyMode.Vi);

            Test("abc def", Keys(
                "abc def", _.Escape,
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                'p', CheckThat(() => AssertLineIs("abc def")), CheckThat(() => AssertCursorLeftIs(6)),
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                'P', CheckThat(() => AssertLineIs("abc def")), CheckThat(() => AssertCursorLeftIs(6)),
                "uuuu"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test("012 456", Keys(
                "012 456", _.Escape,
                "byyP", CheckThat(() => AssertLineIs("012 456\n012 456")), CheckThat(() => AssertCursorLeftIs(0)),
                "u", CheckThat(() => AssertLineIs("012 456")), CheckThat(() => AssertCursorLeftIs(4)),
                "p", CheckThat(() => AssertLineIs("012 456\n012 456")), CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 0)),
                "u", CheckThat(() => AssertLineIs("012 456")), CheckThat(() => AssertCursorLeftIs(4))
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankMovement()
        {
            TestSetup(KeyMode.Vi);

            Test("012 456", Keys(
                "012 456", _.Escape,
                "bylP", CheckThat(() => AssertLineIs("012 4456")), CheckThat(() => AssertCursorLeftIs(4)),
                "u2ylP", CheckThat(() => AssertLineIs("012 45456")), CheckThat(() => AssertCursorLeftIs(5)),
                "ullylp", CheckThat(() => AssertLineIs("012 4566")), CheckThat(() => AssertCursorLeftIs(7)),
                "u"
                ));

            Test("012 456", Keys(
                "012 456", _.Escape,
                "by", _.Spacebar, "P", CheckThat(() => AssertLineIs("012 4456")), CheckThat(() => AssertCursorLeftIs(4)),
                "u2y", _.Spacebar, "P", CheckThat(() => AssertLineIs("012 45456")), CheckThat(() => AssertCursorLeftIs(5)),
                "ully", _.Spacebar, "p", CheckThat(() => AssertLineIs("012 4566")), CheckThat(() => AssertCursorLeftIs(7)),
                "u"
                ));

            Test("012 456", Keys(
                "012 456", _.Escape,
                "bbeyhP", CheckThat(() => AssertLineIs("0112 456")), CheckThat(() => AssertCursorLeftIs(2)),
                "u2yhP", CheckThat(() => AssertLineIs("01012 456")), CheckThat(() => AssertCursorLeftIs(3)),
                "u0yhP", CheckThat(() => AssertLineIs("0012 456")),
                "u"
                ));

            Test("012 456", Keys(
                "012 456", _.Escape,
                "by", _.Dollar, "P", CheckThat(() => AssertLineIs("012 456456")), CheckThat(() => AssertCursorLeftIs(6)),
                "u", _.Dollar, "y", _.Dollar, "P", CheckThat(() => AssertLineIs("012 4566")), CheckThat(() => AssertCursorLeftIs(6)),
                "u", CheckThat(() => AssertLineIs("012 456")), CheckThat(() => AssertCursorLeftIs(6))
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankWord()
        {
            TestSetup(KeyMode.Vi);

            Test("012 456", Keys(
                "012 456", _.Escape,
                "ybp", CheckThat(() => AssertLineIs("012 45645")), CheckThat(() => AssertCursorLeftIs(8)),
                "u0ybP", CheckThat(() => AssertLineIs("45012 456")), CheckThat(() => AssertCursorLeftIs(1)),
                "u", _.Dollar, "2ybp", CheckThat(() => AssertLineIs("012 456012 45")), CheckThat(() => AssertCursorLeftIs(12)),
                "u", _.Dollar, "3ybp", CheckThat(() => AssertLineIs("012 456012 45")), CheckThat(() => AssertCursorLeftIs(12)),
                "uh2ybp", CheckThat(() => AssertLineIs("012 45012 46")), CheckThat(() => AssertCursorLeftIs(10)),
                "u"
                ));

            Test("012 456 ", Keys(
                "012 456 ", _.Escape,
                "ybp", CheckThat(() => AssertLineIs("012 456 456")), CheckThat(() => AssertCursorLeftIs(10)),
                "u0ybP", CheckThat(() => AssertLineIs("456012 456 ")), CheckThat(() => AssertCursorLeftIs(2)),
                "u", _.Dollar, "2ybp", CheckThat(() => AssertLineIs("012 456 012 456")), CheckThat(() => AssertCursorLeftIs(14)),
                "u", _.Dollar, "3ybp", CheckThat(() => AssertLineIs("012 456 012 456")), CheckThat(() => AssertCursorLeftIs(14)),
                "uh2ybp", CheckThat(() => AssertLineIs("012 456012 45 ")), CheckThat(() => AssertCursorLeftIs(12)),
                "u"
                ));

            Test("012 456", Keys(
                "012 456", _.Escape,
                "0ywP", CheckThat(() => AssertLineIs("012 012 456")), CheckThat(() => AssertCursorLeftIs(3)),
                "u02ywP", CheckThat(() => AssertLineIs("012 456012 456")), CheckThat(() => AssertCursorLeftIs(6)),
                "u03ywP", CheckThat(() => AssertLineIs("012 456012 456")), CheckThat(() => AssertCursorLeftIs(6)),
                "u0lywP", CheckThat(() => AssertLineIs("012 12 456")), CheckThat(() => AssertCursorLeftIs(3)),
                "u"
                ));

            Test(" 123  678 ", Keys(
                " 123  678 ", _.Escape,
                "0ywP", CheckThat(() => AssertLineIs("  123  678 ")), CheckThat(() => AssertCursorLeftIs(0)),
                "u02ywP", CheckThat(() => AssertLineIs(" 123   123  678 ")), CheckThat(() => AssertCursorLeftIs(5)),
                "u03ywP", CheckThat(() => AssertLineIs(" 123  678  123  678 ")), CheckThat(() => AssertCursorLeftIs(9)),
                "u0lywP", CheckThat(() => AssertLineIs(" 123  123  678 ")), CheckThat(() => AssertCursorLeftIs(5)),
                "u"
                ));

            Test("012 456", Keys(
                "012 456", _.Escape,
                "0yeP", CheckThat(() => AssertLineIs("012012 456")), CheckThat(() => AssertCursorLeftIs(2)),
                "u02yeP", CheckThat(() => AssertLineIs("012 456012 456")), CheckThat(() => AssertCursorLeftIs(6)),
                "u03yeP", CheckThat(() => AssertLineIs("012 456012 456")), CheckThat(() => AssertCursorLeftIs(6)),
                "u0lyeP", CheckThat(() => AssertLineIs("01212 456")), CheckThat(() => AssertCursorLeftIs(2)),
                "u"
                ));

            Test(" 123  678 ", Keys(
                " 123  678 ", _.Escape,
                "0yeP", CheckThat(() => AssertLineIs(" 123 123  678 ")), CheckThat(() => AssertCursorLeftIs(3)),
                "u02yeP", CheckThat(() => AssertLineIs(" 123  678 123  678 ")), CheckThat(() => AssertCursorLeftIs(8)),
                "u03yeP", CheckThat(() => AssertLineIs(" 123  678  123  678 ")), CheckThat(() => AssertCursorLeftIs(9)),
                "u0lyeP", CheckThat(() => AssertLineIs(" 123123  678 ")), CheckThat(() => AssertCursorLeftIs(3)),
                "u"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankBeginningOfLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test("012", Keys(
                "012", _.Escape,
                "y0P", CheckThat(() => AssertLineIs("01012")), CheckThat(() => AssertCursorLeftIs(1)),
                "u"
                ));

            Test("\"\nHello\n World!\n\"", Keys(
                _.DQuote, _.Enter,
                "Hello", _.Enter,
                " World!", _.Enter,
                _.DQuote, _.Escape,
                _.k, "5l", // move the cursor to the 'd' character of "World!"
                "y0", CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 0)),
                "P", CheckThat(() => AssertLineIs("\"\nHello\n Worl World!\n\"")), CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 4)),
                "u", CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 0))
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankEndOfLine()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test("\"\nHello\nWorld!\n\"", Keys(
                _.DQuote, _.Enter,
                "Hello", _.Enter,
                "World!", _.Enter, 
                _.DQuote, _.Escape,
                _.k, _.l, // move to the 'o' character of 'World!'
                "y$P", CheckThat(() => AssertLineIs("\"\nHello\nWorld!orld!\n\"")), CheckThat(() => AssertCursorLeftIs(continuationPrefixLength + 5)),
                "u"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankFirstNoneBlank()
        {
            TestSetup(KeyMode.Vi);

            Test("012", Keys(
                "012", _.Escape,
                "y", _.Uphat, "P", CheckThat(() => AssertLineIs("01012")), CheckThat(() => AssertCursorLeftIs(3)),
                "u"
                ));

            Test(" 123  ", Keys(
                " 123  ", _.Escape,
                "y", _.Uphat, "P", CheckThat(() => AssertLineIs(" 123 123  ")), CheckThat(() => AssertCursorLeftIs(8)),
                "u"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankPercent()
        {
            TestSetup(KeyMode.Vi);

            Test("012{45}78", Keys(
                "012{45}78", _.Escape, "hh",
                "y", _.Percent, "p", CheckThat(() => AssertLineIs("012{45}{45}78")), CheckThat(() => AssertCursorLeftIs(10)),
                "u", CheckThat(() => AssertCursorLeftIs(7))
                ));

            Test("012(45)78", Keys(
                "012(45)78", _.Escape, "hh",
                "y", _.Percent, "p", CheckThat(() => AssertLineIs("012(45)(45)78")), CheckThat(() => AssertCursorLeftIs(10)),
                "u", CheckThat(() => AssertCursorLeftIs(7))
                ));

            Test("012[45]78", Keys(
                "012[45]78", _.Escape, "hh",
                "y", _.Percent, "p", CheckThat(() => AssertLineIs("012[45][45]78")), CheckThat(() => AssertCursorLeftIs(10)),
                "u", CheckThat(() => AssertCursorLeftIs(7))
                ));

            Test("012{45}", Keys(
                "012{45}", _.Escape, "hhh", CheckThat(() => AssertCursorLeftIs(3)),
                "y", _.Percent, "P", CheckThat(() => AssertLineIs("012{45}{45}")),
                "u"
                ));

            Test("{123}56", Keys(
                "{123}56", _.Escape, "hh", CheckThat(() => AssertCursorLeftIs(4)),
                "y", _.Percent, "p", CheckThat(() => AssertLineIs("{123}{123}56")),
                "u"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankPreviousGlob()
        {
            TestSetup(KeyMode.Vi);

            Test("012++567++012", Keys(
                "012++567++012", _.Escape,
                "yBp", CheckThat(() => AssertLineIs("012++567++012012++567++01")),
                "u"
                ));

            Test(" 012++567++012", Keys(
                " 012++567++012", _.Escape,
                "yBp", CheckThat(() => AssertLineIs(" 012++567++012012++567++01")),
                "u"
                ));

            Test("012+ 567++012", Keys(
                "012+ 567++012", _.Escape,
                "yBp", CheckThat(() => AssertLineIs("012+ 567++012567++01")),
                "u", _.Dollar, "2yBp", CheckThat(() => AssertLineIs("012+ 567++012012+ 567++01")),
                "u"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankNextGlob()
        {
            TestSetup(KeyMode.Vi);

            Test("012++567++abc", Keys(
                "012++567++abc", _.Escape,
                "0yWP", CheckThat(() => AssertLineIs("012++567++abc012++567++abc")),
                "u"
                ));

            Test("012++567++abc ", Keys(
                "012++567++abc ", _.Escape,
                "0yWP", CheckThat(() => AssertLineIs("012++567++abc 012++567++abc ")),
                "u"
                ));

            Test("012++567 +abc ", Keys(
                "012++567 +abc ", _.Escape,
                "0yWP", CheckThat(() => AssertLineIs("012++567 012++567 +abc ")),
                "u02yWP", CheckThat(() => AssertLineIs("012++567 +abc 012++567 +abc ")),
                "u"
                ));
        }

        [SkippableFact]
        public void ViPasteAfterYankEndOfGlob()
        {
            TestSetup(KeyMode.Vi);

            Test("012++567++abc", Keys(
                "012++567++abc", _.Escape,
                "0yEP", CheckThat(() => AssertLineIs("012++567++abc012++567++abc")),
                "u"
                ));

            Test("012++567 +abc", Keys(
                "012++567 +abc", _.Escape,
                "0yEP", CheckThat(() => AssertLineIs("012++567012++567 +abc")),
                "u02yEP", CheckThat(() => AssertLineIs("012++567 +abc012++567 +abc")),
                "u"
                ));
        }

        [SkippableFact]
        public void ViYankAndPasteLogicalLines()
        {
            TestSetup(KeyMode.Vi);

            Test("\"\nline1\nline2\nline1\nline2\n\"", Keys(
                _.DQuote, _.Enter,
                "line1", _.Enter,
                "line2", _.Enter,
                _.DQuote, _.Escape,
                _.k, _.k,
                "2yy", 'P'
                ));
        }

        [SkippableFact]
        public void ViYankAndPasteLogicalLines_LastLine()
        {
            TestSetup(KeyMode.Vi);

            Test("\"\nHello\nWorld!\nWorld!\n\"", Keys(
                _.DQuote, _.Enter,
                "Hello", _.Enter,
                "World!", _.Enter,
                _.DQuote, _.Escape,
                _.k,
                "yy",
                _.j, // move to last line
                'P'
                ));
        }
    }
}
