using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestEndOfLine()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys( _.End, CheckThat(() => AssertCursorLeftIs(0)) ));

            var buffer = new string(' ', Console.BufferWidth);
            Test(buffer, Keys(
                buffer,
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.End,
                CheckThat(() => AssertCursorLeftTopIs(0, 1))
                ));

            buffer = new string(' ', Console.BufferWidth + 5);
            Test(buffer, Keys(
                buffer,
                _.Home,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.End,
                CheckThat(() => AssertCursorLeftTopIs(5, 1))
                ));
        }

        [TestMethod]
        public void TestCursorMovement()
        {
            TestSetup(KeyMode.Cmd);

            Test("abcde", Keys(
                // Left arrow at start of line.
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                "ace",
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(2)),
                'd',
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(2)),
                _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(1)),
                'b'
                ));
        }

        [TestMethod]
        public void TestGotoBrace()
        {
            TestSetup(KeyMode.Cmd);

            // Test empty input
            Test("", Keys(_.CtrlRBracket));

            Test("(11)", Keys("(11)", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("$a[11]", Keys("$a[11]", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(2))));
            Test("{11}", Keys("{11}", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("(11)", Keys("(11)", _.Home, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(3))));
            Test("$a[11]", Keys("$a[11]", _.Home, _.RightArrow, _.RightArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(5))));
            Test("{11}", Keys("{11}", _.Home, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(3))));

            // Test multiples, make sure we go to the right one.
            Test("((11))", Keys("((11))", _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(0))));
            Test("((11))", Keys("((11))", _.LeftArrow, _.LeftArrow, _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(1))));

            using (ShimsContext.Create())
            {
                bool ding = false;
                PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.Ding =
                    () => ding = true;

                // Make sure we don't match inside a string
                Test("", Keys(
                    "'()'", _.LeftArrow, _.LeftArrow,
                    _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(2)),
                    _.CtrlC, InputAcceptedNow));
                Assert.IsTrue(ding);

                foreach (var c in new[] {'(', ')', '{', '}', '[', ']'})
                {
                    ding = false;
                    PSConsoleUtilities.Fakes.ShimPSConsoleReadLine.Ding =
                        () => ding = true;
                    Test("", Keys(
                        'a', c, _.LeftArrow,
                        _.CtrlRBracket, CheckThat(() => AssertCursorLeftIs(1)),
                        _.CtrlC, InputAcceptedNow));
                    Assert.IsTrue(ding);
                }
            }
        }
    }
}
