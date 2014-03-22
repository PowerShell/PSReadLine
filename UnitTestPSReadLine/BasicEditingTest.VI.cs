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
        public void ViTestInput()
        {
            TestSetup( KeyMode.Vi );

            Test( "exit", Keys(
                "exit",
                _.Enter,
                CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );
        }

        [TestMethod]
        public void ViTestAppend()
        {
            // Add one test for chords
            TestSetup( KeyMode.Vi );

            Test( "wiley", Keys( "i", _.Escape, "ae", _.Escape, "il", _.Escape, "Iw", _.Escape, "Ay" ) );
        }

        [TestMethod]
        public void ViTestChangeMovement()
        {
            TestSetup( KeyMode.Vi );

            Test( "0123f", Keys(
                "fgedcba",
                _.Escape,
                "0cla",
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                CheckThat( () => AssertLineIs( "agedcba" ) ),
                "lchb",
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                CheckThat( () => AssertLineIs( "abedcba" ) ),
                "c",
                _.Space,
                "c",
                CheckThat( () => AssertCursorLeftIs( 3 ) ),
                CheckThat( () => AssertLineIs( "abcdcba" ) ),
                "c",
                _.Dollar,
                "def",
                _.Escape,
                CheckThat( () => AssertLineIs( "abcdef" ) ),
                CheckThat( () => AssertCursorLeftIs( 5 ) ),
                "c00123",
                _.Escape,
                CheckThat( () => AssertCursorLeftIs( 3 ) ),
                CheckThat( () => AssertLineIs( "0123f" ) )
                ) );

            Test( "67exit89", Keys(
                "67[abc]89", _.Escape, CheckThat( () => AssertLineIs( "67[abc]89" ) ),
                "hhc", _.Percent, "(123)", _.Escape, CheckThat( () => AssertLineIs( "67(123)89") ),
                "c", _.Percent, "{dorayme}", _.Escape, CheckThat( () => AssertLineIs( "67{dorayme}89" ) ),
                "c", _.Percent, "exit", _.Escape, CheckThat( () => AssertLineIs( "67exit89" ) )
                ) );

            Test( " goodbyeo", Keys(
                " hello", _.Escape, CheckThat( () => AssertLineIs( " hello" ) ),
                "c", _.Uphat, "goodbye", _.Escape, CheckThat( () => AssertLineIs( " goodbyeo" ) )
                ) );

            Test( "gOOD12345", Keys(
                "abc def ghi", _.Escape, CheckThat( () => AssertLineIs( "abc def ghi" ) ),
                "bbcw123", _.Escape, CheckThat( () => AssertLineIs( "abc 123 ghi" ) ),
                "lcbxyz", _.Escape, CheckThat( () => AssertLineIs( "abc xyz ghi" ) ),
                "wbcWdef", _.Escape, CheckThat( () => AssertLineIs( "abc def ghi" ) ),
                "lcB321", _.Escape, CheckThat( () => AssertLineIs( "abc 321 ghi" ) ),
                "02cwxyz 789", _.Escape, CheckThat( () => AssertLineIs( "xyz 789 ghi" ) ),
                _.Dollar, "2cbabc 123", _.Escape, CheckThat( () => AssertLineIs( "xyz abc 123i" ) ),
                "02cW123 456", _.Escape, CheckThat( () => AssertLineIs( "123 456 123i" ) ),
                _.Dollar, "2cBabc xyz", _.Escape, CheckThat( () => AssertLineIs( "123 abc xyzi" ) ),
                "ceZ", _.Escape, CheckThat( () => AssertLineIs( "123 abc xyZ" ) ),
                "bb2ce456 789", _.Escape, CheckThat( () => AssertLineIs( "123 456 789" ) ),
                "cEZ", _.Escape, CheckThat( () => AssertLineIs( "123 456 78Z" ) ),
                "bb2cEabc xyz", _.Escape, CheckThat( () => AssertLineIs( "123 abc xyz" ) ),
                "5hChello", _.Escape, CheckThat( () => AssertLineIs( "123 ahello" ) ),
                "ccGoodbye", _.Escape, CheckThat( () => AssertLineIs( "Goodbye" ) ),
                "SgOODBYE", _.Escape, CheckThat( () => AssertLineIs( "gOODBYE" ) ),
                "2hs123", _.Escape, CheckThat( () => AssertLineIs( "gOOD123YE" ) ),
                "lrylre", CheckThat( () => AssertLineIs( "gOOD123ye" ) ),
                "hR45", _.Escape, CheckThat( () => AssertLineIs( "gOOD12345" ) )
                ) );
        }

        [TestMethod]
        public void ViTestDelete()
        {
            TestSetup(KeyMode.Vi);

            Test( "", Keys( 
                "0123456789", _.Escape, CheckThat( () => AssertCursorLeftIs( 9 ) ),
                "x", CheckThat( () => AssertLineIs( "012345678" ) ), CheckThat( () => AssertCursorLeftIs( 8 ) ),
                "X", CheckThat( () => AssertLineIs( "01234568" ) ), CheckThat( () => AssertCursorLeftIs( 7 ) ),
                _.Backspace, CheckThat( () => AssertLineIs( "01234568" ) ), CheckThat( () => AssertCursorLeftIs( 6 ) ),
                _.Delete, CheckThat( () => AssertLineIs( "0123458" ) ), CheckThat( () => AssertCursorLeftIs( 6 ) ),
                "0x", CheckThat( () => AssertLineIs( "123458" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                _.Delete, CheckThat( () => AssertLineIs( "23458" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "2x", CheckThat( () => AssertLineIs( "458" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "ll2X", CheckThat( () => AssertLineIs( "8" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "x", CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            TestMustDing( "0123", Keys( 
                "0123", _.Escape, 
                "0X", CheckThat( () => AssertCursorLeftIs( 0 ) ) 
                ) );

            Test( "", Keys(
                "012345678901234567890", _.Escape, CheckThat( () => AssertCursorLeftIs( 20 ) ),
                "dh", CheckThat( () => AssertLineIs( "01234567890123456780" ) ), CheckThat( () => AssertCursorLeftIs( 19 ) ),
                "dl", CheckThat( () => AssertLineIs( "0123456789012345678" ) ), CheckThat( () => AssertCursorLeftIs( 18 ) ),
                'd', _.Space, CheckThat( () => AssertLineIs( "012345678901234567" ) ), CheckThat( () => AssertCursorLeftIs( 17 ) ),
                "2dh", CheckThat( () => AssertLineIs( "0123456789012347" ) ), CheckThat( () => AssertCursorLeftIs( 15 ) ),
                "h2dl", CheckThat( () => AssertLineIs( "01234567890123" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) ),
                "0dh", CheckThat( () => AssertLineIs( "01234567890123" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "dl", CheckThat( () => AssertLineIs( "1234567890123" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "2dl", CheckThat( () => AssertLineIs( "34567890123" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "8ld", _.Dollar, CheckThat( () => AssertLineIs( "34567890" ) ), CheckThat( () => AssertCursorLeftIs( 7 ) ),
                "3hD", CheckThat( () => AssertLineIs( "3456" ) ), CheckThat( () => AssertCursorLeftIs( 3 ) ),
                "hd0", CheckThat( () => AssertLineIs( "56" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "dd", CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            Test( "", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "bbdw", CheckThat( () => AssertLineIs( "012 890" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "edb", CheckThat( () => AssertLineIs( "012 0" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "bdw", CheckThat( () => AssertLineIs( "0" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'd', _.Dollar, CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            Test( "", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "bbdW", CheckThat( () => AssertLineIs( "012 890" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "edB", CheckThat( () => AssertLineIs( "012 0" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "bdW", CheckThat( () => AssertLineIs( "0" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'd', _.Dollar, CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            Test( "", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "02dw", CheckThat( () => AssertLineIs( "890" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "dw", CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            Test( "", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "02dW", CheckThat( () => AssertLineIs( "890" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "dW", CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            Test( "", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "2db", CheckThat( () => AssertLineIs( "012 0" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "db", CheckThat( () => AssertLineIs( "0" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "dd", CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            Test( "", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "2dB", CheckThat( () => AssertLineIs( "012 0" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "dB", CheckThat( () => AssertLineIs( "0" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "dd", CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            Test( "012 456 890", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "bbde", CheckThat( () => AssertLineIs( "012  890" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "de", CheckThat( () => AssertLineIs( "012 " ) ), CheckThat( () => AssertCursorLeftIs( 3 ) ),
                "dd", CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'u', CheckThat( () => AssertLineIs( "012 " ) ),
                'u', CheckThat( () => AssertLineIs( "012  890" ) ),
                'u'
                ) );

            Test( "012 456 890", Keys(
                "012 456 890", _.Escape, CheckThat( () => AssertLineIs( "012 456 890" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "bbdE", CheckThat( () => AssertLineIs( "012  890" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "dE", CheckThat( () => AssertLineIs( "012 " ) ), CheckThat( () => AssertCursorLeftIs( 3 ) ),
                "dd", CheckThat( () => AssertLineIs( "" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'u', CheckThat( () => AssertLineIs( "012 " ) ),
                'u', CheckThat( () => AssertLineIs( "012  890" ) ),
                'u'
                ) );

            Test( "012 456 89a", Keys(
                "012 456 89a", _.Escape, CheckThat( () => AssertLineIs( "012 456 89a" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "d0", CheckThat( () => AssertLineIs( "a" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'u'
                ) );

            Test( " 12 456 89a", Keys(
                " 12 456 89a", _.Escape, CheckThat( () => AssertLineIs( " 12 456 89a" ) ), CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "d", _.Uphat, CheckThat( () => AssertLineIs( " a" ) ), CheckThat( () => AssertCursorLeftIs( 1 ) ),
                'u'
                ) );
        }

        [TestMethod]
        public void TestPercent()
        {

            Test( "(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) ),
                "hd", _.Percent, CheckThat( () => AssertLineIs( "(1{3c" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "u", CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) )
                ) );

            Test( "(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) ),
                "hhhd", _.Percent, CheckThat( () => AssertLineIs( "b]c" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "u", CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 11 ) )
                ) );

            Test( "(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) ),
                "hhhhhd", _.Percent, CheckThat( () => AssertLineIs( "(19)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 2 ) ),
                "u", CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 9 ) )
                ) );

            Test( "(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) ),
                "h", _.Percent, "d", _.Percent, CheckThat( () => AssertLineIs( "(1{3c" ) ), CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "u", CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) )
                ) );

            Test( "(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) ),
                "hhh", _.Percent, "d", _.Percent, CheckThat( () => AssertLineIs( "7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "u", CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 7 ) )
                ) );

            Test( "(1{3[5)7}9)b]c", Keys(
                "(1{3[5)7}9)b]c", _.Escape, CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 13 ) ),
                "hhhhh", _.Percent, "d", _.Percent, CheckThat( () => AssertLineIs( "(19)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 2 ) ),
                "u", CheckThat( () => AssertLineIs( "(1{3[5)7}9)b]c" ) ), CheckThat( () => AssertCursorLeftIs( 9 ) )
                ) );
        }

        [TestMethod]
        public void TestCTRL()
        {
            Test( "abc def ghi", Keys(
                "abc def ghi", _.Escape, CheckThat( () => AssertLineIs( "abc def ghi" ) ),
                _.CtrlW, CheckThat( () => AssertLineIs( "abc def i" ) ),
                'u'
                ) );
            Test( "abc def ghi", Keys(
                "abc def ghi", _.Escape, CheckThat( () => AssertLineIs( "abc def ghi" ) ),
                _.CtrlU, CheckThat( () => AssertLineIs( "i" ) ),
                'u'
                ) );
        }

        [TestMethod]
        public void TestMisc()
        {
            Test( "abcdefg", Keys(
                "abcdefg", _.Escape, CheckThat( () => AssertLineIs( "abcdefg" ) ),
                '0', _.Tilde, _.Tilde, _.Tilde, _.Tilde, _.Tilde, _.Tilde, _.Tilde, CheckThat( () => AssertLineIs( "ABCDEFG" ) ),
                "uuuuuuu"
                ) );

            Test( "abcd", Keys(
                "abcd", _.Escape, CheckThat( () => AssertLineIs( "abcd" ) ),
                "h", _.CtrlT, CheckThat( () => AssertLineIs( "acbd" ) ),
                _.CtrlT, CheckThat( () => AssertLineIs( "acdb" ) ),
                "uu"
                ) );
        }
    }
}
