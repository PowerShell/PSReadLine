using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void ViTestWordMovement()
        {
            TestSetup(KeyMode.Vi);

            Test("012 456 890", Keys(
                "012 456 890",
                _.Escape,
                CheckThat(() => AssertCursorLeftIs(10)),
                'b',
                CheckThat(() => AssertCursorLeftIs(8)),
                'b',
                CheckThat(() => AssertCursorLeftIs(4)),
                'w',
                CheckThat(() => AssertCursorLeftIs(8)),
                'w',
                CheckThat(() => AssertCursorLeftIs(10)),
                'w',
                CheckThat(() => AssertCursorLeftIs(10)),
                'b',
                CheckThat(() => AssertCursorLeftIs(8)),
                'b',
                CheckThat(() => AssertCursorLeftIs(4)),
                'b',
                CheckThat(() => AssertCursorLeftIs(0)),
                'b',
                CheckThat(() => AssertCursorLeftIs(0)),
                "2w",
                CheckThat(() => AssertCursorLeftIs(8)),
                "w2b",
                CheckThat(() => AssertCursorLeftIs(4)),
                'b',
                CheckThat(() => AssertCursorLeftIs(0)),
                "3W",
                CheckThat(() => AssertCursorLeftIs(10)),
                'B',
                CheckThat(() => AssertCursorLeftIs(8)),
                'B',
                CheckThat(() => AssertCursorLeftIs(4)),
                'W',
                CheckThat(() => AssertCursorLeftIs(8)),
                'W',
                CheckThat(() => AssertCursorLeftIs(10)),
                'W',
                CheckThat(() => AssertCursorLeftIs(10)),
                'B',
                CheckThat(() => AssertCursorLeftIs(8)),
                'B',
                CheckThat(() => AssertCursorLeftIs(4)),
                'B',
                CheckThat(() => AssertCursorLeftIs(0)),
                'B',
                CheckThat(() => AssertCursorLeftIs(0)),
                "2W",
                CheckThat(() => AssertCursorLeftIs(8)),
                "W2B",
                CheckThat(() => AssertCursorLeftIs(4)),
                'B',
                CheckThat(() => AssertCursorLeftIs(0)),
                "3W",
                CheckThat(() => AssertCursorLeftIs(10)),
                "3B",
                CheckThat(() => AssertCursorLeftIs(0)),
                'e',
                CheckThat(() => AssertCursorLeftIs(2)),
                'e',
                CheckThat(() => AssertCursorLeftIs(6)),
                'e',
                CheckThat(() => AssertCursorLeftIs(10)),
                "3b",
                CheckThat(() => AssertCursorLeftIs(0)),
                "2e",
                CheckThat(() => AssertCursorLeftIs(6)),
                "3B",
                CheckThat(() => AssertCursorLeftIs(0)),
                'E',
                CheckThat(() => AssertCursorLeftIs(2)),
                'E',
                CheckThat(() => AssertCursorLeftIs(6)),
                'E',
                CheckThat(() => AssertCursorLeftIs(10)),
                "3b",
                CheckThat(() => AssertCursorLeftIs(0)),
                "2E",
                CheckThat(() => AssertCursorLeftIs(6))
                ));

            Test("012 456 890", Keys(
                "012", _.Space, "456", _.Space, "890", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "b", CheckThat(() => AssertCursorLeftIs(8)),
                "b", CheckThat(() => AssertCursorLeftIs(4)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "b", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test(" 12 45 78", Keys(
                " 12 45 78", CheckThat(() => AssertCursorLeftIs(9)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(8)),
                "b", CheckThat(() => AssertCursorLeftIs(7)),
                "b", CheckThat(() => AssertCursorLeftIs(4)),
                "b", CheckThat(() => AssertCursorLeftIs(1)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "b", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012 456 890", Keys(
                "012", _.Space, "456", _.Space, "890", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "w", CheckThat(() => AssertCursorLeftIs(4)),
                "w", CheckThat(() => AssertCursorLeftIs(8)),
                "w", CheckThat(() => AssertCursorLeftIs(10)),
                "w", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test("012 456 890", Keys(
                "012", _.Space, "456", _.Space, "890", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "W", CheckThat(() => AssertCursorLeftIs(4)),
                "W", CheckThat(() => AssertCursorLeftIs(8)),
                "W", CheckThat(() => AssertCursorLeftIs(10)),
                "W", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test("012  567  012", Keys(
                "012", _.Space, _.Space, "567", _.Space, _.Space, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "b", CheckThat(() => AssertCursorLeftIs(10)),
                "b", CheckThat(() => AssertCursorLeftIs(5)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "b", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012  567  012", Keys(
                "012", _.Space, _.Space, "567", _.Space, _.Space, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "B", CheckThat(() => AssertCursorLeftIs(10)),
                "B", CheckThat(() => AssertCursorLeftIs(5)),
                "B", CheckThat(() => AssertCursorLeftIs(0)),
                "B", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012  567  012", Keys(
                "012", _.Space, _.Space, "567", _.Space, _.Space, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "w", CheckThat(() => AssertCursorLeftIs(5)),
                "w", CheckThat(() => AssertCursorLeftIs(10)),
                "w", CheckThat(() => AssertCursorLeftIs(12)),
                "w", CheckThat(() => AssertCursorLeftIs(12))
                ));

            Test("012  567  012", Keys(
                "012", _.Space, _.Space, "567", _.Space, _.Space, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "W", CheckThat(() => AssertCursorLeftIs(5)),
                "W", CheckThat(() => AssertCursorLeftIs(10)),
                "W", CheckThat(() => AssertCursorLeftIs(12)),
                "W", CheckThat(() => AssertCursorLeftIs(12))
                ));

            Test(" 123  678", Keys(
                _.Space, "123", _.Space, _.Space, "678", CheckThat(() => AssertCursorLeftIs(9)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(8)),
                "b", CheckThat(() => AssertCursorLeftIs(6)),
                "b", CheckThat(() => AssertCursorLeftIs(1)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "w", CheckThat(() => AssertCursorLeftIs(1)),
                "w", CheckThat(() => AssertCursorLeftIs(6)),
                "w", CheckThat(() => AssertCursorLeftIs(8)),
                "w", CheckThat(() => AssertCursorLeftIs(8))
                ));

            Test(" 123  678", Keys(
                _.Space, "123", _.Space, _.Space, "678", CheckThat(() => AssertCursorLeftIs(9)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(8)),
                "B", CheckThat(() => AssertCursorLeftIs(6)),
                "B", CheckThat(() => AssertCursorLeftIs(1)),
                "B", CheckThat(() => AssertCursorLeftIs(0)),
                "B", CheckThat(() => AssertCursorLeftIs(0)),
                "W", CheckThat(() => AssertCursorLeftIs(1)),
                "W", CheckThat(() => AssertCursorLeftIs(6)),
                "W", CheckThat(() => AssertCursorLeftIs(8)),
                "W", CheckThat(() => AssertCursorLeftIs(8))
                ));

            Test(" 123  678  ", Keys(
                _.Space, "123", _.Space, _.Space, "678", _.Space, _.Space, CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "b", CheckThat(() => AssertCursorLeftIs(6)),
                "b", CheckThat(() => AssertCursorLeftIs(1)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "w", CheckThat(() => AssertCursorLeftIs(1)),
                "w", CheckThat(() => AssertCursorLeftIs(6)),
                "hh", CheckThat(() => AssertCursorLeftIs(4)),
                "w", CheckThat(() => AssertCursorLeftIs(6)),
                "w", CheckThat(() => AssertCursorLeftIs(10)),
                "w", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test(" 123  678  ", Keys(
                _.Space, "123", _.Space, _.Space, "678", _.Space, _.Space, CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "B", CheckThat(() => AssertCursorLeftIs(6)),
                "B", CheckThat(() => AssertCursorLeftIs(1)),
                "B", CheckThat(() => AssertCursorLeftIs(0)),
                "B", CheckThat(() => AssertCursorLeftIs(0)),
                "W", CheckThat(() => AssertCursorLeftIs(1)),
                "W", CheckThat(() => AssertCursorLeftIs(6)),
                "hh", CheckThat(() => AssertCursorLeftIs(4)),
                "W", CheckThat(() => AssertCursorLeftIs(6)),
                "W", CheckThat(() => AssertCursorLeftIs(10)),
                "W", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test(" 123  678 ", Keys(
                " 123  678 ", CheckThat(() => AssertCursorLeftIs(10)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(9)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "e", CheckThat(() => AssertCursorLeftIs(3)),
                "e", CheckThat(() => AssertCursorLeftIs(8)),
                "e", CheckThat(() => AssertCursorLeftIs(9)),
                "e", CheckThat(() => AssertCursorLeftIs(9))
                ));

            Test(" 123  678  ", Keys(
                " 123  678  ", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "e", CheckThat(() => AssertCursorLeftIs(3)),
                "e", CheckThat(() => AssertCursorLeftIs(8)),
                "e", CheckThat(() => AssertCursorLeftIs(10)),
                "e", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test(" 123  678  ", Keys(
                _.Space, "123", _.Space, _.Space, "678", _.Space, _.Space, CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "E", CheckThat(() => AssertCursorLeftIs(3)),
                "E", CheckThat(() => AssertCursorLeftIs(8)),
                "E", CheckThat(() => AssertCursorLeftIs(10)),
                "E", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test("012 456", Keys(
                "012 456", _.Escape,
                "02e", CheckThat(() => AssertCursorLeftIs(6))
                ));
        }

        [TestMethod]
        public void ViTestDotWordMovement()
        {
            TestSetup(KeyMode.Vi);

            Test("012.456.890", Keys(
                "012", _.Period, "456", _.Period, "890", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "b", CheckThat(() => AssertCursorLeftIs(8)),
                "b", CheckThat(() => AssertCursorLeftIs(7)),
                "b", CheckThat(() => AssertCursorLeftIs(4)),
                "b", CheckThat(() => AssertCursorLeftIs(3)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "b", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012.456.890", Keys(
                "012", _.Period, "456", _.Period, "890", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "B", CheckThat(() => AssertCursorLeftIs(0)),
                "B", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012.456.890", Keys(
                "012", _.Period, "456", _.Period, "890", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "w", CheckThat(() => AssertCursorLeftIs(3)),
                "w", CheckThat(() => AssertCursorLeftIs(4)),
                "w", CheckThat(() => AssertCursorLeftIs(7)),
                "w", CheckThat(() => AssertCursorLeftIs(8)),
                "w", CheckThat(() => AssertCursorLeftIs(10)),
                "w", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test("012.456.890", Keys(
                "012", _.Period, "456", _.Period, "890", CheckThat(() => AssertCursorLeftIs(11)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(10)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "W", CheckThat(() => AssertCursorLeftIs(10)),
                "W", CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test("012..567..012", Keys(
                "012", _.Period, _.Period, "567", _.Period, _.Period, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "b", CheckThat(() => AssertCursorLeftIs(10)),
                "b", CheckThat(() => AssertCursorLeftIs(8)),
                "b", CheckThat(() => AssertCursorLeftIs(5)),
                "b", CheckThat(() => AssertCursorLeftIs(3)),
                "b", CheckThat(() => AssertCursorLeftIs(0)),
                "b", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012..567..012", Keys(
                "012", _.Period, _.Period, "567", _.Period, _.Period, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "B", CheckThat(() => AssertCursorLeftIs(0)),
                "B", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012..567..012", Keys(
                "012", _.Period, _.Period, "567", _.Period, _.Period, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "w", CheckThat(() => AssertCursorLeftIs(3)),
                "w", CheckThat(() => AssertCursorLeftIs(5)),
                "w", CheckThat(() => AssertCursorLeftIs(8)),
                "w", CheckThat(() => AssertCursorLeftIs(10)),
                "w", CheckThat(() => AssertCursorLeftIs(12)),
                "w", CheckThat(() => AssertCursorLeftIs(12))
                ));

            Test("012..567..012", Keys(
                "012", _.Period, _.Period, "567", _.Period, _.Period, "012", CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "W", CheckThat(() => AssertCursorLeftIs(12)),
                "W", CheckThat(() => AssertCursorLeftIs(12))
                ));

            Test(" 123..678..123", Keys(
                _.Space, "123", _.Period, _.Period, "678", _.Period, _.Period, "123", CheckThat(() => AssertCursorLeftIs(14)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(13)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "e", CheckThat(() => AssertCursorLeftIs(3)),
                "e", CheckThat(() => AssertCursorLeftIs(5)),
                "e", CheckThat(() => AssertCursorLeftIs(8)),
                "e", CheckThat(() => AssertCursorLeftIs(10)),
                "e", CheckThat(() => AssertCursorLeftIs(13)),
                "e", CheckThat(() => AssertCursorLeftIs(13))
                ));

            Test(" 123..678..123", Keys(
                _.Space, "123", _.Period, _.Period, "678", _.Period, _.Period, "123", CheckThat(() => AssertCursorLeftIs(14)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(13)),
                "0", CheckThat(() => AssertCursorLeftIs(0)),
                "E", CheckThat(() => AssertCursorLeftIs(13)),
                "E", CheckThat(() => AssertCursorLeftIs(13)),
                "B", CheckThat(() => AssertCursorLeftIs(1)),
                "B", CheckThat(() => AssertCursorLeftIs(0))
                ));
        }

        [TestMethod]
        public void ViTestCursorMovement()
        {
            TestSetup(KeyMode.Vi);

            Test("a", Keys('a', CheckThat(() => AssertCursorLeftIs(1))));
            Test("ac", Keys("ac", CheckThat(() => AssertCursorLeftIs(2))));
            Test("ace", Keys("ace", CheckThat(() => AssertCursorLeftIs(3))));
            Test(" abcde", Keys(
                "ace",
                CheckThat(() => AssertCursorLeftIs(3)),
                _.Escape,
                CheckThat(() => AssertCursorLeftIs(2)),
                'h',
                CheckThat(() => AssertCursorLeftIs(1)),
                "ib",
                CheckThat(() => AssertCursorLeftIs(2)),
                _.Escape,
                'l',
                CheckThat(() => AssertCursorLeftIs(2)),
                "ad",
                CheckThat(() => AssertCursorLeftIs(4)),
                _.Escape,
                _.Space,
                CheckThat(() => AssertCursorLeftIs(4)),
                'l',
                CheckThat(() => AssertCursorLeftIs(4)),
                "3h",
                CheckThat(() => AssertCursorLeftIs(1)),
                "2l",
                CheckThat(() => AssertCursorLeftIs(3)),
                '0',
                CheckThat(() => AssertCursorLeftIs(0)),
                '$',
                CheckThat(() => AssertCursorLeftIs(4)),
                "0i",
                _.Space,
                CheckThat(() => AssertCursorLeftIs(1)),
                _.Escape,
                _.Dollar,
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Uphat,
                CheckThat(() => AssertCursorLeftIs(1))
                ));
        }

        [TestMethod]
        public void ViTestGotoBrace()
        {
            TestSetup(KeyMode.Vi);

            Test("0[2(4{6]8)a}c", Keys(
                "0[2(4{6]8)a}c",
                CheckThat(() => AssertCursorLeftIs(13)),
                _.Escape, CheckThat(() => AssertCursorLeftIs(12)),
                'h', CheckThat(() => AssertCursorLeftIs(11)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(5)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(11)),
                "hh", CheckThat(() => AssertCursorLeftIs(9)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(3)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(9)),
                "hh", CheckThat(() => AssertCursorLeftIs(7)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(1)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(7))
                ));

            foreach (char c in new[] { '(', ')', '{', '}', '[', ']' })
            {
                string input = "abcd" + c;
                TestMustDing("", Keys(
                    input,
                    CheckThat(() => AssertCursorLeftIs(5)),
                    _.Escape,
                    CheckThat(() => AssertCursorLeftIs(4)),
                    _.Percent,
                    CheckThat(() => AssertCursorLeftIs(4)),
                    "ddi"
                    ));
            }
        }

        [TestMethod]
        public void ViTestCharacterSearch()
        {
            TestSetup(KeyMode.Vi);

            Test("", Keys(
                "0123456789",
                CheckThat(() => AssertCursorLeftIs(10)),
                _.Escape,
                "0",
                CheckThat(() => AssertCursorLeftIs(0)),
                "f8",
                CheckThat(() => AssertCursorLeftIs(8)),
                "F1",
                CheckThat(() => AssertCursorLeftIs(1)),
                "$a0123456789",
                CheckThat(() => AssertCursorLeftIs(20)),
                _.Escape,
                "2F1",
                CheckThat(() => AssertCursorLeftIs(1)),
                "2f8",
                CheckThat(() => AssertCursorLeftIs(18)),
                "F1;",
                CheckThat(() => AssertCursorLeftIs(1)),
                _.Comma,
                CheckThat(() => AssertCursorLeftIs(11)),
                "0f8;",
                CheckThat(() => AssertCursorLeftIs(18)),
                _.Comma,
                CheckThat(() => AssertCursorLeftIs(8)),
                "dd"
                ));

            Test("", Keys(
                "0123456789",
                CheckThat(() => AssertCursorLeftIs(10)),
                _.Escape,
                "0",
                CheckThat(() => AssertCursorLeftIs(0)),
                "t8",
                CheckThat(() => AssertCursorLeftIs(7)),
                "T1",
                CheckThat(() => AssertCursorLeftIs(2)),
                "$a0123456789",
                CheckThat(() => AssertCursorLeftIs(20)),
                _.Escape,
                "2T1",
                CheckThat(() => AssertCursorLeftIs(2)),
                "2t8",
                CheckThat(() => AssertCursorLeftIs(17)),
                "dd"
                ));

            TestMustDing("01234", Keys(
                "01234",
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Escape,
                "F9",
                CheckThat(() => AssertCursorLeftIs(4))
                ));

            TestMustDing("01234", Keys(
                "01234",
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Escape,
                "0f9",
                CheckThat(() => AssertCursorLeftIs(0))
                ));

            TestMustDing("01234", Keys(
                "01234",
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Escape,
                "T9",
                CheckThat(() => AssertCursorLeftIs(4))
                ));

            TestMustDing("01234", Keys(
                "01234",
                CheckThat(() => AssertCursorLeftIs(5)),
                _.Escape,
                "0t9",
                CheckThat(() => AssertCursorLeftIs(0))
                ));
        }

        [TestMethod]
        public void ViTestColumnMovement()
        {
            TestSetup(KeyMode.Vi);

            Test("0123456789012345678901234567890", Keys(
                "0123456789012345678901234567890",
                CheckThat(() => AssertCursorLeftIs(31)),
                _.Escape,
                "11|",
                CheckThat(() => AssertCursorLeftIs(10)),
                "1|",
                CheckThat(() => AssertCursorLeftIs(0))
                ));

            TestMustDing("0123456789012345678901234567890", Keys(
                "0123456789012345678901234567890",
                CheckThat(() => AssertCursorLeftIs(31)),
                _.Escape,
                CheckThat(() => AssertCursorLeftIs(30)),
               "11|",
                CheckThat(() => AssertCursorLeftIs(10)),
                "33|",
                CheckThat(() => AssertCursorLeftIs(30))
                ));
        }

        [TestMethod]
        public void ViTestBOLErrorCase()
        {
            TestSetup(KeyMode.Vi);

            Test("h", Keys(
                _.Escape, 'h', _.Space, "ih"
                ));
        }

        [TestMethod]
        public void ViTestCharDelete()
        {
            TestSetup(KeyMode.Vi);

            Test("", Keys(
                "abcdefg", _.Escape, CheckThat(() => AssertLineIs("abcdefg")),
                "0dfg", CheckThat(() => AssertLineIs("")),
                'u', CheckThat(() => AssertLineIs("abcdefg")), CheckThat(() => AssertCursorLeftIs(6)),
                "0dff", CheckThat(() => AssertLineIs("g")),
                'u', CheckThat(() => AssertLineIs("abcdefg")), CheckThat(() => AssertCursorLeftIs(6)),
                "0dfg"
                ));

            Test("", Keys(
                "abcdefg", _.Escape, CheckThat(() => AssertLineIs("abcdefg")),
                "dFa", _.Escape, CheckThat(() => AssertLineIs("")),
                'u', CheckThat(() => AssertCursorLeftIs(6)),
                "dFb", CheckThat(() => AssertLineIs("a")),
                'u', CheckThat(() => AssertCursorLeftIs(6)),
                "dFa"
                ));

            Test("0123456", Keys(
                "0123456", _.Escape, CheckThat(() => AssertLineIs("0123456")),
                "0dt6", CheckThat(() => AssertLineIs("6")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "0dt5", CheckThat(() => AssertLineIs("56")),
                'u', CheckThat(() => AssertLineIs("0123456")),
                "0ldt6", CheckThat(() => AssertLineIs("06")),
                'u', CheckThat(() => AssertLineIs("0123456")),
                "0ldt5", CheckThat(() => AssertLineIs("056")),
                'u'
                ));

            Test("0123456", Keys(
                "0123456", _.Escape, CheckThat(() => AssertLineIs("0123456")),
                "dT0", CheckThat(() => AssertLineIs("0")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "hdT0", CheckThat(() => AssertLineIs("06")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "0dT0"
                ));
        }

        [TestMethod]
        public void ViTestDefect456()
        {
            TestSetup(KeyMode.Vi);

            Test("", Keys(
                _.Escape, "kjw"
                ));
        }
    }
}
