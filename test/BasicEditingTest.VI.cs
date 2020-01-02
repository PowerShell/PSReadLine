using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ViInput()
        {
            TestSetup(KeyMode.Vi);

            Test("exit", Keys(
                "exit",
                _.Enter,
                CheckThat(() => AssertCursorLeftIs(0))
                ));
        }

        [SkippableFact]
        public void ViAppend()
        {
            TestSetup(KeyMode.Vi);

            Test("wiley", Keys(
                "i",
                _.Escape, "ae", CheckThat(() => AssertLineIs("ie")),
                _.Escape, "il", CheckThat(() => AssertLineIs("ile")),
                _.Escape, "Iw", CheckThat(() => AssertLineIs("wile")),
                _.Escape, "Ay", CheckThat(() => AssertLineIs("wiley"))
                ));
        }

        [SkippableFact]
        public void ViChangeMovement()
        {
            TestSetup(KeyMode.Vi);

            Test("0123f", Keys(
                "fgedcba",
                _.Escape,
                "0cla", _.Escape, CheckThat(() => AssertCursorLeftIs(0)), CheckThat(() => AssertLineIs("agedcba")),
                "llchb", _.Escape, CheckThat(() => AssertCursorLeftIs(1)), CheckThat(() => AssertLineIs("abedcba")),
                "lc", _.Spacebar, "c", _.Escape, CheckThat(() => AssertCursorLeftIs(2)), CheckThat(() => AssertLineIs("abcdcba")),
                "lc", _.Dollar, "def", _.Escape, CheckThat(() => AssertLineIs("abcdef")), CheckThat(() => AssertCursorLeftIs(5)),
                "c00123", _.Escape, CheckThat(() => AssertCursorLeftIs(3)), CheckThat(() => AssertLineIs("0123f"))
                ));

            Test("67exit89", Keys(
                "67{abc}89", _.Escape, CheckThat(() => AssertLineIs("67{abc}89")),
                "hhc", _.Percent, "(123)", _.Escape, CheckThat(() => AssertLineIs("67(123)89")),
                "c", _.Percent, "{dorayme}", _.Escape, CheckThat(() => AssertLineIs("67{dorayme}89")),
                "c", _.Percent, "exit", _.Escape, CheckThat(() => AssertLineIs("67exit89"))
                ));

            Test(" goodbyeo", Keys(
                " hello", _.Escape, CheckThat(() => AssertLineIs(" hello")),
                "c", _.Uphat, "goodbye", _.Escape, CheckThat(() => AssertLineIs(" goodbyeo"))
                ));

            Test("gOOD12345", Keys(
                "abc def ghi", _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                "bbcw123", _.Escape, CheckThat(() => AssertLineIs("abc 123 ghi")),
                "lcbxyz", _.Escape, CheckThat(() => AssertLineIs("abc xyz ghi")),
                "wbcWdef", _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                "lcB321", _.Escape, CheckThat(() => AssertLineIs("abc 321 ghi")),
                "02cwxyz 789", _.Escape, CheckThat(() => AssertLineIs("xyz 789 ghi")),
                _.Dollar, "2cbabc 123", _.Escape, CheckThat(() => AssertLineIs("xyz abc 123i")),
                "02cW123 456", _.Escape, CheckThat(() => AssertLineIs("123 456 123i")),
                _.Dollar, "2cBabc xyz", _.Escape, CheckThat(() => AssertLineIs("123 abc xyzi")),
                "ceZ", _.Escape, CheckThat(() => AssertLineIs("123 abc xyZ")),
                "bb2ce456 789", _.Escape, CheckThat(() => AssertLineIs("123 456 789")),
                "cEZ", _.Escape, CheckThat(() => AssertLineIs("123 456 78Z")),
                "bb2cEabc xyz", _.Escape, CheckThat(() => AssertLineIs("123 abc xyz")),
                "5hChello", _.Escape, CheckThat(() => AssertLineIs("123 ahello")),
                "ccGoodbye", _.Escape, CheckThat(() => AssertLineIs("Goodbye")),
                "SgOODBYE", _.Escape, CheckThat(() => AssertLineIs("gOODBYE")),
                "2hs123", _.Escape, CheckThat(() => AssertLineIs("gOOD123YE")),
                "lrylre", CheckThat(() => AssertLineIs("gOOD123ye")),
                "hR45", _.Escape, CheckThat(() => AssertLineIs("gOOD12345"))
                ));

            Test("hello", Keys(
                _.Escape, "Chello", _.Escape, CheckThat(() => AssertLineIs("hello")),
                "0Cgoodbye", _.Escape, CheckThat(() => AssertLineIs("goodbye")),
                'u'
                ));
        }

        [SkippableFact]
        public void ViDefect623()
        {
            TestSetup(KeyMode.Vi);

            Test("012 4568", Keys(
                "012 4567", _.Escape, CheckThat(() => AssertCursorLeftIs(7)),
                "s8", _.Escape, CheckThat(() => AssertCursorLeftIs(7)), CheckThat(() => AssertLineIs("012 4568"))
                ));

            Test("asdf bzdf", Keys(
                "asdf asdf", _.Escape, "0wcfs", CheckThat(() => AssertCursorLeftIs(5)),
                "bz"
                ));
        }

        [SkippableFact]
        public void ViDefect628()
        {
            TestSetup(KeyMode.Vi);

            Test("alsf", Keys(
                "lsf lsf", _.Escape, "bi", _.Ctrl_u, 'a'
                ));

            Test("a", Keys(
                "lsf lsf", _.Ctrl_u, 'a'
                ));
        }

        [SkippableFact]
        public void Defect796()
        {
            TestSetup(KeyMode.Vi);

            Test("\"\n\n\"", Keys(
                _.DQuote, _.Enter, _.Escape, CheckThat(() => AssertCursorTopIs(1)),
                'o', CheckThat(() => AssertCursorTopIs(2)),
                _.DQuote
                ));
        }

        [SkippableFact]
        public void ViChangeMovementUndo()
        {
            TestSetup(KeyMode.Vi);

            Test("", Keys(
                "0123(567)9ab", _.Escape, "hhh", CheckThat(() => AssertCursorLeftIs(8)),
                'c', _.Percent, "45678", _.Escape, CheckThat(() => AssertLineIs("0123456789ab")), CheckThat(() => AssertCursorLeftIs(8)),
                'u', CheckThat(() => AssertLineIs("0123(567)9ab")), CheckThat(() => AssertCursorLeftIs(9)),
                'U'
                ));
            Test("", Keys(
                "0123456789a", _.Escape, "hhh", CheckThat(() => AssertCursorLeftIs(7)),
                'c', _.Dollar, "_ABCD", _.Escape, CheckThat(() => AssertLineIs("0123456_ABCD")),
                'u', CheckThat(() => AssertLineIs("0123456789a")),
                'U'
                ));
            Test("", Keys(
                " 123456789a", _.Escape, "hhh", CheckThat(() => AssertCursorLeftIs(7)),
                'c', _.Uphat, "ABCD", _.Escape, CheckThat(() => AssertLineIs(" ABCD789a")),
                'u', CheckThat(() => AssertLineIs(" 123456789a")),
                'U'
                ));
            Test("", Keys(
                "0123456789a", _.Escape, "hhh", CheckThat(() => AssertCursorLeftIs(7)),
                "c0", "ABCD", _.Escape, CheckThat(() => AssertLineIs("ABCD789a")),
                'u', CheckThat(() => AssertLineIs("0123456789a")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "bb", CheckThat(() => AssertCursorLeftIs(4)),
                "cwxxx", _.Escape, CheckThat(() => AssertLineIs("abc xxx ghi")),
                'u', CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "bb", CheckThat(() => AssertCursorLeftIs(4)),
                "2cwxxx", _.Escape, CheckThat(() => AssertLineIs("abc xxx")),
                'u', CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "bb", CheckThat(() => AssertCursorLeftIs(4)),
                "cWxxx", _.Escape, CheckThat(() => AssertLineIs("abc xxx ghi")),
                'u', CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "bb", CheckThat(() => AssertCursorLeftIs(4)),
                "cexxx", _.Escape, CheckThat(() => AssertLineIs("abc xxx ghi")),
                'u', _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "bb", CheckThat(() => AssertCursorLeftIs(4)),
                "2cexxx", _.Escape, CheckThat(() => AssertLineIs("abc xxx")),
                'u', _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "bb", CheckThat(() => AssertCursorLeftIs(4)),
                "cExxx", _.Escape, CheckThat(() => AssertLineIs("abc xxx ghi")),
                'u', _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "hhhh", CheckThat(() => AssertCursorLeftIs(6)),
                "cbxxx", _.Escape, CheckThat(() => AssertLineIs("abc xxxf ghi")),
                'u', _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "hhhh", CheckThat(() => AssertCursorLeftIs(6)),
                "2cbxxx", _.Escape, CheckThat(() => AssertLineIs("xxxf ghi")),
                'u', _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "abc def ghi", _.Escape, "hhhh", CheckThat(() => AssertCursorLeftIs(6)),
                "cBxxx", _.Escape, CheckThat(() => AssertLineIs("abc xxxf ghi")),
                'u', _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                'U'
                ));
            Test("", Keys(
                "0123456789", _.Escape,
                "chabc", _.Escape, CheckThat(() => AssertLineIs("01234567abc9")),
                'u', CheckThat(() => AssertLineIs("0123456789")),
                'U'
                ));
            Test("", Keys(
                "0123456789", _.Escape,
                "0clabc", _.Escape, CheckThat(() => AssertLineIs("abc123456789")),
                'u', CheckThat(() => AssertLineIs("0123456789")),
                'U'
                ));
            Test("", Keys(
                "0123456789", _.Escape,
                "0llCABC", _.Escape, CheckThat(() => AssertLineIs("01ABC")),
                'u', CheckThat(() => AssertLineIs("0123456789")),
                'U'
                ));
            Test("", Keys(
                "0123456789", _.Escape,
                "ccABCDEFG", _.Escape, CheckThat(() => AssertLineIs("ABCDEFG")),
                'u', CheckThat(() => AssertLineIs("0123456789")),
                'U'
                ));
            Test("", Keys(
                "0123456789", _.Escape,
                "SABCDEFG", _.Escape, CheckThat(() => AssertLineIs("ABCDEFG")),
                'u', CheckThat(() => AssertLineIs("0123456789")),
                'U'
                ));
            Test("", Keys(
                "0123456789", _.Escape,
                "4hsABC", _.Escape, CheckThat(() => AssertLineIs("01234ABC6789")),
                'u', CheckThat(() => AssertLineIs("0123456789")),
                'U'
                ));
        }

        [SkippableFact]
        public void ViDelete()
        {
            TestSetup(KeyMode.Vi);

            Test("bc", Keys(
                "a", _.Escape,
                CheckThat(() => AssertLineIs("a")),
                CheckThat(() => AssertCursorLeftIs(0)),
                "s", CheckThat(() => AssertLineIs("")),
                "bc"));

            Test("", Keys(
                "0123456789", _.Escape, CheckThat(() => AssertCursorLeftIs(9)),
                "x", CheckThat(() => AssertLineIs("012345678")), CheckThat(() => AssertCursorLeftIs(8)),
                "X", CheckThat(() => AssertLineIs("01234568")), CheckThat(() => AssertCursorLeftIs(7)),
                _.Backspace, CheckThat(() => AssertLineIs("01234568")), CheckThat(() => AssertCursorLeftIs(6)),
                _.Delete, CheckThat(() => AssertLineIs("0123458")), CheckThat(() => AssertCursorLeftIs(6)),
                "0x", CheckThat(() => AssertLineIs("123458")), CheckThat(() => AssertCursorLeftIs(0)),
                _.Delete, CheckThat(() => AssertLineIs("23458")), CheckThat(() => AssertCursorLeftIs(0)),
                "2x", CheckThat(() => AssertLineIs("458")), CheckThat(() => AssertCursorLeftIs(0)),
                "ll2X", CheckThat(() => AssertLineIs("8")), CheckThat(() => AssertCursorLeftIs(0)),
                "x", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("0123", Keys(
                "0123", _.Escape,
                "0X", CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("", Keys(
                "012345678901234567890", _.Escape, CheckThat(() => AssertCursorLeftIs(20)),
                "dh", CheckThat(() => AssertLineIs("01234567890123456780")), CheckThat(() => AssertCursorLeftIs(19)),
                "dl", CheckThat(() => AssertLineIs("0123456789012345678")), CheckThat(() => AssertCursorLeftIs(18)),
                'd', _.Spacebar, CheckThat(() => AssertLineIs("012345678901234567")), CheckThat(() => AssertCursorLeftIs(17)),
                "2dh", CheckThat(() => AssertLineIs("0123456789012347")), CheckThat(() => AssertCursorLeftIs(15)),
                "h2dl", CheckThat(() => AssertLineIs("01234567890123")), CheckThat(() => AssertCursorLeftIs(13)),
                "0dh", CheckThat(() => AssertLineIs("01234567890123")), CheckThat(() => AssertCursorLeftIs(0)),
                "dl", CheckThat(() => AssertLineIs("1234567890123")), CheckThat(() => AssertCursorLeftIs(0)),
                "2dl", CheckThat(() => AssertLineIs("34567890123")), CheckThat(() => AssertCursorLeftIs(0)),
                "8ld", _.Dollar, CheckThat(() => AssertLineIs("34567890")), CheckThat(() => AssertCursorLeftIs(7)),
                "3hD", CheckThat(() => AssertLineIs("3456")), CheckThat(() => AssertCursorLeftIs(3)),
                "hd0", CheckThat(() => AssertLineIs("56")), CheckThat(() => AssertCursorLeftIs(0)),
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "bbdw", CheckThat(() => AssertLineIs("012 890")), CheckThat(() => AssertCursorLeftIs(4)),
                "edb", CheckThat(() => AssertLineIs("012 0")), CheckThat(() => AssertCursorLeftIs(4)),
                "bdw", CheckThat(() => AssertLineIs("0")), CheckThat(() => AssertCursorLeftIs(0)),
                'd', _.Dollar, CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "bbdW", CheckThat(() => AssertLineIs("012 890")), CheckThat(() => AssertCursorLeftIs(4)),
                "edB", CheckThat(() => AssertLineIs("012 0")), CheckThat(() => AssertCursorLeftIs(4)),
                "bdW", CheckThat(() => AssertLineIs("0")), CheckThat(() => AssertCursorLeftIs(0)),
                'd', _.Dollar, CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "02dw", CheckThat(() => AssertLineIs("890")), CheckThat(() => AssertCursorLeftIs(0)),
                "dw", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "02dW", CheckThat(() => AssertLineIs("890")), CheckThat(() => AssertCursorLeftIs(0)),
                "dW", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "2db", CheckThat(() => AssertLineIs("012 0")), CheckThat(() => AssertCursorLeftIs(4)),
                "db", CheckThat(() => AssertLineIs("0")), CheckThat(() => AssertCursorLeftIs(0)),
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "2dB", CheckThat(() => AssertLineIs("012 0")), CheckThat(() => AssertCursorLeftIs(4)),
                "dB", CheckThat(() => AssertLineIs("0")), CheckThat(() => AssertCursorLeftIs(0)),
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("012 456 890", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "bbde", CheckThat(() => AssertLineIs("012  890")), CheckThat(() => AssertCursorLeftIs(4)),
                "de", CheckThat(() => AssertLineIs("012 ")), CheckThat(() => AssertCursorLeftIs(3)),
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                'u', CheckThat(() => AssertLineIs("012 ")),
                'u', CheckThat(() => AssertLineIs("012  890")),
                'u'
                ));

            Test("012 456 890", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "02de", CheckThat(() => AssertLineIs(" 890")), CheckThat(() => AssertCursorLeftIs(0)),
                "u03de", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                "u04de", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                'u'
                ));

            Test("012 456 890", Keys(
                "012 456 890", _.Escape, CheckThat(() => AssertLineIs("012 456 890")), CheckThat(() => AssertCursorLeftIs(10)),
                "bbdE", CheckThat(() => AssertLineIs("012  890")), CheckThat(() => AssertCursorLeftIs(4)),
                "dE", CheckThat(() => AssertLineIs("012 ")), CheckThat(() => AssertCursorLeftIs(3)),
                "dd", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                'u', CheckThat(() => AssertLineIs("012 ")),
                'u', CheckThat(() => AssertLineIs("012  890")),
                'u'
                ));

            Test("012 456 89a", Keys(
                "012 456 89a", _.Escape, CheckThat(() => AssertLineIs("012 456 89a")), CheckThat(() => AssertCursorLeftIs(10)),
                "d0", CheckThat(() => AssertLineIs("a")), CheckThat(() => AssertCursorLeftIs(0)),
                'u'
                ));

            Test(" 12 456 89a", Keys(
                " 12 456 89a", _.Escape, CheckThat(() => AssertLineIs(" 12 456 89a")), CheckThat(() => AssertCursorLeftIs(10)),
                "d", _.Uphat, CheckThat(() => AssertLineIs(" a")), CheckThat(() => AssertCursorLeftIs(1)),
                'u'
                ));

            Test("0123", Keys(
                "0123", _.Escape,
                "d1h", CheckThat(() => AssertLineIs("013")),
                "ud3h", CheckThat(() => AssertLineIs("3")),
                "ud4h", CheckThat(() => AssertLineIs("3")),
                "u0dl", CheckThat(() => AssertLineIs("123")),
                "u0d4l", CheckThat(() => AssertLineIs("")),
                "u0d5l", CheckThat(() => AssertLineIs("")),
                "u0d10l", CheckThat(() => AssertLineIs("")),
                "u05d5l", CheckThat(() => AssertLineIs("0123"))
                ));

            Test("nslookup www.google.com", Keys(
                "nslookup www", _.Period, "google", _.Period, "com", _.Escape,
                "0d2w", CheckThat(() => AssertLineIs(".google.com")),
                "u0d3w", CheckThat(() => AssertLineIs("google.com")),
                "u0d4w", CheckThat(() => AssertLineIs(".com")),
                "u0d5w", CheckThat(() => AssertLineIs("com")),
                "u0d6w", CheckThat(() => AssertLineIs("")),
                "u0d7w", CheckThat(() => AssertLineIs("")),
                'u'
                ));

            Test("nslookup www.google.com", Keys(
                "nslookup www", _.Period, "google", _.Period, "com", _.Escape,
                "0d1W", CheckThat(() => AssertLineIs("www.google.com")),
                "u0d2W", CheckThat(() => AssertLineIs("")),
                "u0d3W", CheckThat(() => AssertLineIs("")),
                'u'
                ));

            Test("nslookup www.google.com", Keys(
                "nslookup www", _.Period, "google", _.Period, "com", _.Escape,
                "0d1e", CheckThat(() => AssertLineIs(" www.google.com")),
                "u0d2e", CheckThat(() => AssertLineIs(".google.com")),
                "u0d3e", CheckThat(() => AssertLineIs("google.com")),
                "u0d4e", CheckThat(() => AssertLineIs(".com")),
                "u0d5e", CheckThat(() => AssertLineIs("com")),
                "u0d6e", CheckThat(() => AssertLineIs("")),
                "u0d7e", CheckThat(() => AssertLineIs("")),
                'u'
                ));

            Test("nslookup www.google.com", Keys(
                "nslookup www", _.Period, "google", _.Period, "com", _.Escape,
                "0d1E", CheckThat(() => AssertLineIs(" www.google.com")),
                "u0d2E", CheckThat(() => AssertLineIs("")),
                "u0d3E", CheckThat(() => AssertLineIs("")),
                'u'
                ));

            Test("nslookup www.google.com", Keys(
                "nslookup www", _.Period, "google", _.Period, "com", _.Escape,
                "d1b", CheckThat(() => AssertLineIs("nslookup www.google.m")),
                "ud2b", CheckThat(() => AssertLineIs("nslookup www.googlem")),
                "ud3b", CheckThat(() => AssertLineIs("nslookup www.m")),
                "ud4b", CheckThat(() => AssertLineIs("nslookup wwwm")),
                "ud5b", CheckThat(() => AssertLineIs("nslookup m")),
                "ud6b", CheckThat(() => AssertLineIs("m")),
                "ud7b", CheckThat(() => AssertLineIs("m")),
                'u'
                ));

            Test("nslookup www.google.com", Keys(
                "nslookup www", _.Period, "google", _.Period, "com", _.Escape,
                "d1B", CheckThat(() => AssertLineIs("nslookup m")),
                "ud2B", CheckThat(() => AssertLineIs("m")),
                "ud3B", CheckThat(() => AssertLineIs("m")),
                'u'
                ));

            Test("Ins delete", Keys(
                "Ins delete1", CheckThat(() => AssertLineIs("Ins delete1")), CheckThat(() => AssertCursorLeftIs(11)),
                _.LeftArrow, CheckThat(() => AssertLineIs("Ins delete1")), CheckThat(() => AssertCursorLeftIs(10)),
                _.Delete, CheckThat(() => AssertLineIs("Ins delete")), CheckThat(() => AssertCursorLeftIs(10)),
                _.Delete, CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test("Ins delete", Keys(
                "Ins x delete", _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.Delete, _.Delete, CheckThat(() => AssertCursorLeftIs(4))
                ));

            Test("Ins delete", Keys(
                "xxIns delete", _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow,
                _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.Delete, _.Delete, CheckThat(() => AssertCursorLeftIs(0))
                ));
        }

        [SkippableFact]
        public void ViGlobDelete()
        {
            TestSetup(KeyMode.Vi);

            Test("012", Keys(
                "012", _.Escape,
                "0dW", CheckThat(() => AssertLineIs("")),
                "u"
                ));

            Test("0 2+4 6", Keys(
                "0 2+4 6", _.Escape, CheckThat(() => AssertLineIs("0 2+4 6")), CheckThat(() => AssertCursorLeftIs(6)),
                "dB", CheckThat(() => AssertLineIs("0 6")), CheckThat(() => AssertCursorLeftIs(2)),
                "u", CheckThat(() => AssertLineIs("0 2+4 6")), CheckThat(() => AssertCursorLeftIs(6)),
                "2dB", CheckThat(() => AssertLineIs("6")), CheckThat(() => AssertCursorLeftIs(0)),
                "u", CheckThat(() => AssertLineIs("0 2+4 6")), CheckThat(() => AssertCursorLeftIs(6))
                ));

            Test("0 2+4 6", Keys(
                "0 2+4 6", _.Escape, CheckThat(() => AssertLineIs("0 2+4 6")), CheckThat(() => AssertCursorLeftIs(6)),
                "dE", CheckThat(() => AssertLineIs("0 2+4 ")), CheckThat(() => AssertCursorLeftIs(5)),
                "uBdE", CheckThat(() => AssertLineIs("0  6")), CheckThat(() => AssertCursorLeftIs(2)),
                "u0ldE", CheckThat(() => AssertLineIs("0 6")), CheckThat(() => AssertCursorLeftIs(1)),
                "u0l2dE", CheckThat(() => AssertLineIs("0")), CheckThat(() => AssertCursorLeftIs(0)),
                "u03dE", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                "u", CheckThat(() => AssertLineIs("0 2+4 6")), CheckThat(() => AssertCursorLeftIs(6))
                ));

            Test("0 2+4 6", Keys(
                "0 2+4 6", _.Escape, CheckThat(() => AssertLineIs("0 2+4 6")), CheckThat(() => AssertCursorLeftIs(6)),
                "dW", CheckThat(() => AssertLineIs("0 2+4 ")), CheckThat(() => AssertCursorLeftIs(5)),
                "uBdW", CheckThat(() => AssertLineIs("0 6")), CheckThat(() => AssertCursorLeftIs(2)),
                "u0ldW", CheckThat(() => AssertLineIs("02+4 6")), CheckThat(() => AssertCursorLeftIs(1)),
                "u0l2dW", CheckThat(() => AssertLineIs("06")), CheckThat(() => AssertCursorLeftIs(1)),
                "u0l3dW", CheckThat(() => AssertLineIs("0")), CheckThat(() => AssertCursorLeftIs(0)),
                "u03dW", CheckThat(() => AssertLineIs("")), CheckThat(() => AssertCursorLeftIs(0)),
                "u", CheckThat(() => AssertLineIs("0 2+4 6")), CheckThat(() => AssertCursorLeftIs(6))
                ));
        }

        [SkippableFact]
        public void ViPercent()
        {
            TestSetup(KeyMode.Vi);

            Test("{{}}", Keys(
                "{{}}", _.Escape, CheckThat(() => AssertCursorLeftIs(3)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(0)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(3))
                ));

            Test("(())", Keys(
                "(())", _.Escape, CheckThat(() => AssertCursorLeftIs(3)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(0)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(3))
                ));

            Test("[[]]", Keys(
                "[[]]", _.Escape, CheckThat(() => AssertCursorLeftIs(3)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(0)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(3))
                ));

            Test("(1{3{5)789)b}c", Keys(
                "(1{3{5)789)b}c", _.Escape, CheckThat(() => AssertLineIs("(1{3{5)789)b}c")), CheckThat(() => AssertCursorLeftIs(13)),
                "hd", _.Percent, CheckThat(() => AssertLineIs("(1{3c")), CheckThat(() => AssertCursorLeftIs(4)),
                "u", CheckThat(() => AssertLineIs("(1{3{5)789)b}c")), CheckThat(() => AssertCursorLeftIs(13))
                ));

            Test("(1{3[5)7}9)b]", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(13)),
                "hhhd", _.Percent, CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(10)),
                "u", CheckThat(() => AssertLineIs("(1{3[5)7}9)b]")), CheckThat(() => AssertCursorLeftIs(12))
                ));

            Test("(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(13)),
                "hhhhhd", _.Percent, CheckThat(() => AssertLineIs("(19)b]c")), CheckThat(() => AssertCursorLeftIs(2)),
                "u", CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(9))
                ));

            Test("(1{3[5)7}9)b]d", Keys(
                "(1{3[5)7}9)b]d", _.Escape, CheckThat(() => AssertLineIs("(1{3[5)7}9)b]d")), CheckThat(() => AssertCursorLeftIs(13)),
                "h", _.Percent, "d", _.Percent, CheckThat(() => AssertLineIs("(1{3d")), CheckThat(() => AssertCursorLeftIs(4)),
                "u", CheckThat(() => AssertLineIs("(1{3[5)7}9)b]d")), CheckThat(() => AssertCursorLeftIs(13))
                ));

            Test("(1{3[5)7}9)b]", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(13)),
                "hhh", CheckThat(() => AssertCursorLeftIs(10)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(10)),
                "d", _.Percent, CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(10)),
                "u", CheckThat(() => AssertLineIs("(1{3[5)7}9)b]")), CheckThat(() => AssertCursorLeftIs(12))
                ));

            Test("(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(13)),
                "hhhhh", _.Percent, "d", _.Percent, CheckThat(() => AssertLineIs("(19)b]c")), CheckThat(() => AssertCursorLeftIs(2)),
                "u", CheckThat(() => AssertLineIs("(1{3[5)7}9)b]c")), CheckThat(() => AssertCursorLeftIs(9))
                ));

            Test("012 [ 67 ] bc", Keys(
                "012 [ 67 ] bc", _.Escape, CheckThat(() => AssertLineIs("012 [ 67 ] bc")), CheckThat(() => AssertCursorLeftIs(12)),
                "hhh", CheckThat(() => AssertCursorLeftIs(9)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(4)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(9)),
                'd', _.Percent, CheckThat(() => AssertLineIs("012  bc")),
                "uh", CheckThat(() => AssertLineIs("012 [ 67 ] bc")), CheckThat(() => AssertCursorLeftIs(9)),
                'c', _.Percent, "99", _.Escape, CheckThat(() => AssertLineIs("012 99 bc")),
                'u'
                ));

            Test("012 { 67 } bc", Keys(
                "012 { 67 } bc", _.Escape, CheckThat(() => AssertLineIs("012 { 67 } bc")), CheckThat(() => AssertCursorLeftIs(12)),
                "hhh", CheckThat(() => AssertCursorLeftIs(9)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(4)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(9)),
                'd', _.Percent, CheckThat(() => AssertLineIs("012  bc")), CheckThat(() => AssertCursorLeftIs(4)),
                'u', CheckThat(() => AssertLineIs("012 { 67 } bc")), CheckThat(() => AssertCursorLeftIs(10)),
                'h', _.Percent, CheckThat(() => AssertCursorLeftIs(4)),
                'd', _.Percent, CheckThat(() => AssertLineIs("012  bc")), CheckThat(() => AssertCursorLeftIs(4)),
                'u', CheckThat(() => AssertCursorLeftIs(10))
                ));

            Test("012 ( 67 ) bc", Keys(
                "012 ( 67 ) bc", _.Escape, CheckThat(() => AssertLineIs("012 ( 67 ) bc")), CheckThat(() => AssertCursorLeftIs(12)),
                "hhh", CheckThat(() => AssertCursorLeftIs(9)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(4)),
                _.Percent, CheckThat(() => AssertCursorLeftIs(9)),
                'd', _.Percent, CheckThat(() => AssertLineIs("012  bc")), CheckThat(() => AssertCursorLeftIs(4)),
                'u', CheckThat(() => AssertLineIs("012 ( 67 ) bc")), CheckThat(() => AssertCursorLeftIs(10)),
                'h', _.Percent, CheckThat(() => AssertCursorLeftIs(4)),
                'd', _.Percent, CheckThat(() => AssertLineIs("012  bc")), CheckThat(() => AssertCursorLeftIs(4)),
                'u', CheckThat(() => AssertCursorLeftIs(10))
                ));
        }

        [SkippableFact]
        public void ViCTRL()
        {
            TestSetup(KeyMode.Vi);

            Test("abc def ghi", Keys(
                CheckThat(() => AssertLineIs("")),
                "abc def ghi", _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                _.Ctrl_w, CheckThat(() => AssertLineIs("abc def i")),
                'u'
                ));

            Test("abc def ghi", Keys(
                "abc def ghi", _.Escape, CheckThat(() => AssertLineIs("abc def ghi")),
                _.Ctrl_u, CheckThat(() => AssertLineIs("i")),
                'u'
                ));

            Test("abc", Keys(
                "abc", _.Escape, _.Ctrl_d, InputAcceptedNow
                ));
        }

        [SkippableFact]
        public void ViMisc()
        {
            TestSetup(KeyMode.Vi);

            Test("abcdefg", Keys(
                "abcdefg", _.Escape, CheckThat(() => AssertLineIs("abcdefg")),
                '0', _.Tilde, _.Tilde, _.Tilde, _.Tilde, _.Tilde, _.Tilde, _.Tilde, CheckThat(() => AssertLineIs("ABCDEFG")),
                "uuuuuuu"
                ));

            Test("abcd", Keys(
                "abcd", _.Escape, CheckThat(() => AssertLineIs("abcd")),
                "h", _.Ctrl_t, CheckThat(() => AssertLineIs("acbd")),
                _.Ctrl_t, CheckThat(() => AssertLineIs("acdb")),
                CheckThat(() => AssertCursorLeftIs(3)),
                "0", _.Ctrl_t, CheckThat(() => AssertLineIs("acdb")),
                "uu"
                ));
        }

        [SkippableFact]
        public void ViChange()
        {
            TestSetup(KeyMode.Vi);

            Test("012 45", Keys(
                "012 45", _.Escape,
                "0cwabc", _.Escape, CheckThat(() => AssertLineIs("abc 45")),
                "u", CheckThat(() => AssertLineIs("012 45")), CheckThat(() => AssertCursorLeftIs(4)),
                "0cwabc", _.Escape, CheckThat(() => AssertLineIs("abc 45")),
                "u", CheckThat(() => AssertCursorLeftIs(4)),
                "0cwabc", _.Escape, "wcwef", _.Escape, CheckThat(() => AssertLineIs("abc ef")),
                "uu", CheckThat(() => AssertLineIs("012 45")),
                "02cwabcdef", _.Escape, CheckThat(() => AssertLineIs("abcdef")),
                "u03cwghi klm", _.Escape, CheckThat(() => AssertLineIs("ghi klm")),
                'u'
                ));

            Test("012+45", Keys(
                "012+45", _.Escape,
                "0cWabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                'u',
                "0cWabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                "u0Pld$"
                ));

            Test("012+45 789", Keys(
                "012+45 789", _.Escape,
                "0cWabc", _.Escape, CheckThat(() => AssertLineIs("abc 789")),
                'u', CheckThat(() => AssertLineIs("012+45 789")),
                "02cWabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                'u', CheckThat(() => AssertLineIs("012+45 789")),
                "03cWabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                'u', CheckThat(() => AssertLineIs("012+45 789")),
                "04cWabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                'u'
                ));

            Test("012+456", Keys(
                "012+456", _.Escape,
                "cBabc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                "u$cBabc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                "u$2cBabc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                'u'
                ));

            Test("0 2 4 6", Keys(
                "0 2 4 6", _.Escape,
                "cBabc", _.Escape, CheckThat(() => AssertLineIs("0 2 abc6")),
                "u$cBabc", _.Escape, CheckThat(() => AssertLineIs("0 2 abc6")),
                "u$2cBabc", _.Escape, CheckThat(() => AssertLineIs("0 abc6")),
                "u$3cBabc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                "u$4cBabc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                'u'
                ));

            Test("012+456", Keys(
                "012+456", _.Escape,
                "0cEabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                "u0cEabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                "u02cEabc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                'u'
                ));

            Test("012 456", Keys(
                "012 456", _.Escape,
                "0cEabc", _.Escape, CheckThat(() => AssertLineIs("abc 456")), CheckThat(() => AssertCursorLeftIs(2)),
                "u0cEabc", _.Escape, CheckThat(() => AssertLineIs("abc 456")), CheckThat(() => AssertCursorLeftIs(2)),
                "u02cEabc", _.Escape, CheckThat(() => AssertLineIs("abc")), CheckThat(() => AssertCursorLeftIs(2)),
                "u03cEabc", _.Escape, CheckThat(() => AssertLineIs("abc")), CheckThat(() => AssertCursorLeftIs(2)),
                'u'
                ));

            Test("0[23]5", Keys(
                "0[23]5", _.Escape,
                "0wc", _.Percent, "abc", _.Escape, CheckThat(() => AssertLineIs("0abc5")), CheckThat(() => AssertCursorLeftIs(3)),
                "u0wc", _.Percent, "abc", _.Escape, CheckThat(() => AssertLineIs("0abc5")), CheckThat(() => AssertCursorLeftIs(3)),
                "u0w2c", _.Percent, "abc", _.Escape, CheckThat(() => AssertLineIs("0abc5")), CheckThat(() => AssertCursorLeftIs(3)),
                "u0w3c", _.Percent, "abc", _.Escape, CheckThat(() => AssertLineIs("0abc5")), CheckThat(() => AssertCursorLeftIs(3)),
                'u'
                ));

            Test("", Keys(
                _.Escape, 'r', _.Spacebar, CheckThat(() => AssertCursorLeftIs(0))
                ));

            Test("$", Keys(
                _.Escape, _.Dollar, 'i', _.Dollar, CheckThat(() => AssertCursorLeftIs(1))
                ));
        }

        [SkippableFact]
        public void ViDefect651()
        {
            TestSetup(KeyMode.Vi, new KeyHandler("Tab", PSConsoleReadLine.MenuComplete));

            Test("ls -Hidden", Keys(
                "abcd", _.Escape, "0Cls -H", _.Tab, CheckThat(() => AssertLineIs("ls -Hidden")),
                _.Enter
                ));
        }


        [SkippableFact]
        public void ViInsertLine()
        {
            int adder = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;
            TestSetup(KeyMode.Vi);

            Test("line1\n", Keys(
                _.Escape, "Oline1", CheckThat(() => AssertCursorLeftIs(5))
                ));

            Test("\nline1", Keys(
                _.Escape, "oline1", CheckThat(() => AssertCursorLeftIs(5 + adder)), CheckThat(() => AssertLineIs("\nline1")),
                _.Escape, CheckThat(() => AssertCursorLeftIs(4 + adder))
                ));

            Test("", Keys(
                "line2", _.Escape, CheckThat(() => AssertLineIs("line2")),
                "Oline1", _.Escape, CheckThat(() => AssertLineIs("line1\nline2")), CheckThat(() => AssertCursorLeftIs(4)),
                "joline3", _.Escape, CheckThat(() => AssertLineIs("line1\nline2\nline3")),
                'u', CheckThat(() => AssertLineIs("line1\nline2")),
                'u', CheckThat(() => AssertLineIs("line2")),
                'u', CheckThat(() => AssertLineIs("line")),
                "uuuu"
                ));

            Test("", Keys(
                "line2", _.Escape, '0', CheckThat(() => AssertLineIs("line2")),
                "Oline1", _.Escape, CheckThat(() => AssertLineIs("line1\nline2")),
                'j', _.Dollar, "oline3", _.Escape, CheckThat(() => AssertLineIs("line1\nline2\nline3")),
                'u', CheckThat(() => AssertLineIs("line1\nline2")),
                'u', CheckThat(() => AssertLineIs("line2")),
                'u', CheckThat(() => AssertLineIs("line")),
                "uuuu"
                ));

            Test("", Keys(
                _.Escape, "oline4", CheckThat(() => AssertLineIs("\nline4")), CheckThat(() => AssertCursorLeftIs(5 + adder)),
                _.Escape, "Oline2", CheckThat(() => AssertLineIs("\nline2\nline4")), CheckThat(() => AssertCursorLeftIs(5 + adder)),
                _.Escape, "oline3", CheckThat(() => AssertLineIs("\nline2\nline3\nline4")),
                _.Escape, CheckThat(() => AssertLineIs("\nline2\nline3\nline4")), CheckThat(() => AssertCursorLeftIs(4 + adder)),
                'u', CheckThat(() => AssertLineIs("\nline2\nline4")),
                'u', CheckThat(() => AssertLineIs("\nline4")),
                'u'
                ));
        }

        [SkippableFact]
        public void ViJoinLines()
        {
            TestSetup(KeyMode.Vi);

            Test("", Keys(
                "line1", _.Escape, "oline2", CheckThat(() => AssertLineIs("line1\nline2")),
                 _.Escape, "kJ", CheckThat(() => AssertLineIs("line1 line2")),
                'u', CheckThat(() => AssertLineIs("line1\nline2")),
                'u', CheckThat(() => AssertLineIs("line1")),
                'u', CheckThat(() => AssertLineIs("line")),
                "uuuu"
                ));

            Test("", Keys(
                "line1", _.Escape, "oline2", CheckThat(() => AssertLineIs("line1\nline2")),
                 _.Escape, _.Dollar, "J", CheckThat(() => AssertLineIs("line1\nline2")),
                'u', CheckThat(() => AssertLineIs("line1")),
                'u', CheckThat(() => AssertLineIs("line")),
                "uuuu"
                ));
        }

        [SkippableFact]
        public void ViChangeChar()
        {
            TestSetup(KeyMode.Vi);

            Test("0123456", Keys(
                "0123456", _.Escape, CheckThat(() => AssertLineIs("0123456")),
                "0cf6abc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "0cf5abc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "0lcf6abc", _.Escape, CheckThat(() => AssertLineIs("0abc")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "cf6abc", _.Escape, CheckThat(() => AssertLineIs("0123456bc")),
                'u'
                ));

            Test("0123456", Keys(
                "0123456", _.Escape, CheckThat(() => AssertLineIs("0123456")),
                "cF0abc", _.Escape, CheckThat(() => AssertLineIs("abc")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "cF1abc", _.Escape, CheckThat(() => AssertLineIs("0abc")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "hcF0abc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "hcF1abc", _.Escape, CheckThat(() => AssertLineIs("0abc6")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "0cF0abc", _.Escape, CheckThat(() => AssertLineIs("0bc123456")),
                'u'
                ));

            Test("0123456", Keys(
                "0123456", _.Escape, CheckThat(() => AssertLineIs("0123456")),
                "0ct6abc", _.Escape, CheckThat(() => AssertLineIs("abc6")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "0lct6abc", _.Escape, CheckThat(() => AssertLineIs("0abc6")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "ct6abc", _.Escape, CheckThat(() => AssertLineIs("0123456bc")),
                'u'
                ));

            Test("0123456", Keys(
                "0123456", _.Escape, CheckThat(() => AssertLineIs("0123456")),
                "cT1abc", _.Escape, CheckThat(() => AssertLineIs("01abc")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "hcT1abc", _.Escape, CheckThat(() => AssertLineIs("01abc6")),
                'u', CheckThat(() => AssertLineIs("0123456")), CheckThat(() => AssertCursorLeftIs(6)),
                "0cT0abc", _.Escape, CheckThat(() => AssertLineIs("0bc123456")),
                'u'
                ));
        }

        [SkippableFact]
        public void ViComplete()
        {
            TestSetup(KeyMode.Vi);

            PSConsoleReadLine.SetKeyHandler(new[] { "Tab" }, PSConsoleReadLine.Complete, "", "");

            Test("ambiguousness", Keys(
                "ambag", CheckThat(() => AssertLineIs("ambag")),
                _.Escape, "hCig", _.Tab, CheckThat(() => AssertLineIs("ambiguous")),
                _.Escape, "Csness"
                ));
        }

        [SkippableFact]
        public void ViInsertModeMoveCursor()
        {
            TestSetup(KeyMode.Vi);

            Test("abc", Keys(
                "ab", CheckThat(() => AssertCursorLeftIs(2)),
                _.LeftArrow, CheckThat(() => AssertCursorLeftIs(1)),
                _.RightArrow, CheckThat(() => AssertCursorLeftIs(2)),
                _.RightArrow, // 'RightArrow' again does nothing, but doesn't crash
                "c"));
        }
    }
}
