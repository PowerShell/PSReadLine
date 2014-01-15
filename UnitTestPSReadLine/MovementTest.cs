using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
