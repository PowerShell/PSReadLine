using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void ViTestPasteAfterDeleteChar()
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

        [TestMethod]
        public void ViTestPasteAfterDelete()
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

        [TestMethod]
        public void ViTestPasteAfterDeleteBraces()
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

        [TestMethod]
        public void ViTestPasteAfterDeleteWord()
        {
            TestSetup(KeyMode.Vi);

            Test("abc def", Keys(
                "abc def", _.Escape,
                _.CtrlW, CheckThat(() => AssertLineIs("abc f")), CheckThat(() => AssertCursorLeftIs(4)),
                'p', CheckThat(() => AssertLineIs("abc fde")), CheckThat(() => AssertCursorLeftIs(6)),
                'P', CheckThat(() => AssertLineIs("abc fddee")), CheckThat(() => AssertCursorLeftIs(7)),
                "uuu"
                ));

            Test("abc def", Keys(
                "abc def", _.Escape,
                _.CtrlU, CheckThat(() => AssertLineIs("f")), CheckThat(() => AssertCursorLeftIs(0)),
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
    }
}
