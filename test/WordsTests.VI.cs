using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ViChangeWord()
        {
            TestSetup(KeyMode.Vi);

            Test("012 45", Keys(
                "012 45", _.Escape,
                "bcwef", _.Escape, CheckThat(() => AssertLineIs("012 ef")),
                "u", CheckThat(() => AssertLineIs("012 45")),
                "02cwabcdef", _.Escape, CheckThat(() => AssertLineIs("abcdef")),
                "u03cwghi klm", _.Escape, CheckThat(() => AssertLineIs("ghi klm")),
                'u'
                ));

            Test("012 45", Keys(
                "012 45", _.Escape,
                "0cwabc", _.Escape, "wcwef", _.Escape, CheckThat(() => AssertLineIs("abc ef")),
                "uu", CheckThat(() => AssertLineIs("012 45")),
                "02cwabcdef", _.Escape, CheckThat(() => AssertLineIs("abcdef")),
                "u03cwghi klm", _.Escape, CheckThat(() => AssertLineIs("ghi klm")),
                'u'
                ));

            Test("test()b", Keys(
                "test()b", _.Escape, CheckThat(() => AssertLineIs("test()b")), CheckThat(() => AssertCursorLeftIs(6)),
                'b', CheckThat(() => AssertCursorLeftIs(4)),
                "cw", CheckThat(() => AssertCursorLeftIs(4)),
                "[]", _.Escape, CheckThat(() => AssertLineIs("test[]b")),
                'u'
                ));

            Test("test()", Keys(
                "test()", CheckThat(() => AssertLineIs("test()")),
                _.Escape, CheckThat(() => AssertCursorLeftIs(5)),
                'b', CheckThat(() => AssertCursorLeftIs(4)),
                "cw", CheckThat(() => AssertLineIs("test")),
                "[]", _.Escape, CheckThat(() => AssertLineIs("test[]")),
                'u'
                ));

            Test("test()", Keys(
                "test()", CheckThat(() => AssertLineIs("test()")),
                _.Escape, CheckThat(() => AssertCursorLeftIs(5)),
                'b', CheckThat(() => AssertCursorLeftIs(4)),
                "dw", CheckThat(() => AssertLineIs("test")),
                "ubcw", CheckThat(() => AssertLineIs("test")),
                "[]", _.Escape, CheckThat(() => AssertLineIs("test[]")),
                'u'
                ));

            Test(@"vim .\xx\VisualEditing.vi.cs", Keys(
                "vim ", _.Period, _.Backslash, "PSReadLine", _.Backslash, "VisualEditing", _.Period, "vi", _.Period, "cs",
                CheckThat(() => AssertLineIs(@"vim .\PSReadLine\VisualEditing.vi.cs")),
                _.Escape, "Bll", CheckThat(() => AssertCursorLeftIs(6)),
                "cw", _.Escape, CheckThat(() => AssertCursorLeftIs(5)), CheckThat(() => AssertLineIs(@"vim .\\VisualEditing.vi.cs")),
                'u', CheckThat(() => AssertCursorLeftIs(16)), CheckThat(() => AssertLineIs(@"vim .\PSReadLine\VisualEditing.vi.cs")),
                "bcwxx", _.Escape, CheckThat(() => AssertCursorLeftIs(7))
                ));

            Test("$response.Headers['location']", Keys(
                _.Dollar, "response", _.Period, "Headers", _.LBracket, _.SQuote, "location", _.SQuote, _.RBracket, _.Escape,
                CheckThat(() => AssertLineIs("$response.Headers['location']")),
                "bb", CheckThat(() => AssertCursorLeftIs(19)),
                "cw", CheckThat(() => AssertLineIs("$response.Headers['']")),
                "territory", CheckThat(() => AssertLineIs("$response.Headers['territory']")),
                _.Escape, CheckThat(() => AssertCursorLeftIs(27)),
                'u'
                ));


            Test("test(int)", Keys(
                "test(int)", CheckThat(() => AssertLineIs("test(int)")),
                _.Escape, CheckThat(() => AssertCursorLeftIs(8)),
                'b', CheckThat(() => AssertCursorLeftIs(5)),
                "cw", CheckThat(() => AssertLineIs("test()")), CheckThat(() => AssertCursorLeftIs(5)),
                "float", CheckThat(() => AssertLineIs("test(float)")),
                _.Escape, 'u'
                ));
        }
    }
}
