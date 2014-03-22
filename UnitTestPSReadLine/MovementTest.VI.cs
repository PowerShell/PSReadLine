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
        public void ViTestWordMovement()
        {
            TestSetup( KeyMode.Vi );

            Test( "abc def ghi", Keys(
                "abc def ghi",
                _.Escape,
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                'b',
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                'b',
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                'w',
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                'w',
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                'w',
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                'b',
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                'b',
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                'b',
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'b',
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "2w",
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                "w2b",
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                'b',
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "3W",
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                'B',
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                'B',
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                'W',
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                'W',
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                'W',
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                'B',
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                'B',
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                'B',
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'B',
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "2W",
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                "W2B",
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                'B',
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "3W",
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "3B",
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'e',
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                'e',
                CheckThat( () => AssertCursorLeftIs( 6 ) ),
                'e',
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "3b",
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "2e",
                CheckThat( () => AssertCursorLeftIs( 6 ) ),
                "3B",
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                'E',
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                'E',
                CheckThat( () => AssertCursorLeftIs( 6 ) ),
                'E',
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "3b",
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "2E",
                CheckThat( () => AssertCursorLeftIs( 6 ) )
                ) );
        }

        [TestMethod]
        public void ViTestCursorMovement()
        {
            TestSetup( KeyMode.Vi );

            Test( "a", Keys( 'a', CheckThat( () => AssertCursorLeftIs( 1 ) ) ) );
            Test( "ac", Keys( "ac", CheckThat( () => AssertCursorLeftIs( 2 ) ) ) );
            Test( "ace", Keys( "ace", CheckThat( () => AssertCursorLeftIs( 3 ) ) ) );
            Test( " abcde", Keys( 
                "ace", 
                CheckThat( () => AssertCursorLeftIs( 3 ) ),
                _.Escape,
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                'h',
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                "ib",
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                _.Escape,
                'l',
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                "ad",
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                _.Escape,
                _.Space,
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                'l',
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "3h",
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                "2l",
                CheckThat( () => AssertCursorLeftIs( 3 ) ),
                '0',
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                '$',
                CheckThat( () => AssertCursorLeftIs( 4 ) ),
                "0i",
                _.Space,
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                _.Escape,
                _.Dollar,
                CheckThat( () => AssertCursorLeftIs( 5 ) ),
                _.Uphat,
                CheckThat( () => AssertCursorLeftIs( 1 ) )
                ) );
        }

        [TestMethod]
        public void ViTestGotoBrace()
        {
            TestSetup(KeyMode.Vi);

            // Test empty input
            Test("0[2(4{6]8)a}c", Keys(
                "0[2(4{6]8)a}c",
                CheckThat( () => AssertCursorLeftIs( 13 ) ),
                _.Escape,
                CheckThat( () => AssertCursorLeftIs( 12 ) ),
                'h',
                CheckThat( () => AssertCursorLeftIs( 11 ) ),
                _.Percent,
                CheckThat( () => AssertCursorLeftIs( 5 ) ),
                _.Percent,
                CheckThat( () => AssertCursorLeftIs( 11 ) ),
                "hh",
                CheckThat( () => AssertCursorLeftIs( 9 ) ),
                _.Percent,
                CheckThat( () => AssertCursorLeftIs( 3 ) ),
                _.Percent,
                CheckThat( () => AssertCursorLeftIs( 9 ) ),
                "hh",
                CheckThat( () => AssertCursorLeftIs( 7 ) ),
                _.Percent,
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                _.Percent,
                CheckThat( () => AssertCursorLeftIs( 7 ) )
                ) );

            foreach( char c in new[] { '(', ')', '{', '}', '[', ']' } )
            {
                string input = "abcd" + c;
                TestMustDing( "", Keys(
                    input,
                    CheckThat( () => AssertCursorLeftIs( 5 ) ),
                    _.Escape,
                    CheckThat( () => AssertCursorLeftIs( 4 ) ),
                    _.Percent,
                    CheckThat( () => AssertCursorLeftIs( 4 ) ),
                    "ddi"
                    ) );
            }
        }

        [TestMethod]
        public void ViTestCharacterSearch()
        {
            TestSetup( KeyMode.Vi );

            Test( "", Keys(
                "0123456789",
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                _.Escape,
                "0",
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "f8",
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                "F1",
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                "$a0123456789",
                CheckThat( () => AssertCursorLeftIs( 20 ) ),
                _.Escape,
                "2F1",
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                "2f8",
                CheckThat( () => AssertCursorLeftIs( 18 ) ),
                "F1;",
                CheckThat( () => AssertCursorLeftIs( 1 ) ),
                _.Comma,
                CheckThat( () => AssertCursorLeftIs( 11 ) ),
                "0f8;",
                CheckThat( () => AssertCursorLeftIs( 18 ) ),
                _.Comma,
                CheckThat( () => AssertCursorLeftIs( 8 ) ),
                "dd"
                ) );

            Test( "", Keys(
                "0123456789",
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                _.Escape,
                "0",
                CheckThat( () => AssertCursorLeftIs( 0 ) ),
                "t8",
                CheckThat( () => AssertCursorLeftIs( 7 ) ),
                "T1",
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                "$a0123456789",
                CheckThat( () => AssertCursorLeftIs( 20 ) ),
                _.Escape,
                "2T1",
                CheckThat( () => AssertCursorLeftIs( 2 ) ),
                "2t8",
                CheckThat( () => AssertCursorLeftIs( 17 ) ),
                "dd"
                ) );

            TestMustDing( "01234", Keys(
                "01234",
                CheckThat( () => AssertCursorLeftIs( 5 ) ),
                _.Escape,
                "F9",
                CheckThat( () => AssertCursorLeftIs( 4 ) )
                ) );

            TestMustDing( "01234", Keys(
                "01234",
                CheckThat( () => AssertCursorLeftIs( 5 ) ),
                _.Escape,
                "0f9",
                CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            TestMustDing( "01234", Keys(
                "01234",
                CheckThat( () => AssertCursorLeftIs( 5 ) ),
                _.Escape,
                "T9",
                CheckThat( () => AssertCursorLeftIs( 4 ) )
                ) );

            TestMustDing( "01234", Keys(
                "01234",
                CheckThat( () => AssertCursorLeftIs( 5 ) ),
                _.Escape,
                "0t9",
                CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );
        }

        [TestMethod]
        public void ViTestColumnMovement()
        {
            TestSetup(KeyMode.Vi);

            Test( "0123456789012345678901234567890", Keys(
                "0123456789012345678901234567890",
                CheckThat( () => AssertCursorLeftIs( 31 ) ),
                _.Escape,
                "11|",
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "1|",
                CheckThat( () => AssertCursorLeftIs( 0 ) )
                ) );

            TestMustDing( "0123456789012345678901234567890", Keys(
                "0123456789012345678901234567890",
                CheckThat( () => AssertCursorLeftIs( 31 ) ),
                _.Escape,
                CheckThat( () => AssertCursorLeftIs( 30 ) ),
               "11|",
                CheckThat( () => AssertCursorLeftIs( 10 ) ),
                "33|",
                CheckThat( () => AssertCursorLeftIs( 30 ) )
                ) );
        }
    }
}
