#if UNIT_TESTS

using System.Text;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public sealed class MultiLineBufferHelperTests
    {
        [Fact]
        public void MultiLineBufferHelper_LinewiseYank_Lines()
        {
            var buffer = new StringBuilder("line1\nline2\nline3\nline4");

            // system under test

            var range = MultiLineBufferHelper.GetRange(buffer, 1, 2);

            // assert

            Assert.Equal(6, range.Offset);
            Assert.Equal(12, range.Count);
        }

        [Fact]
        public void MultilineBufferHelper_LinewiseYank_MoreLinesThanAvailable()
        {
            var buffer = new StringBuilder("line1\nline2");

            // system under test

            var range = MultiLineBufferHelper.GetRange(buffer, 1, 42);

            // assert

            Assert.Equal(6, range.Offset);
            Assert.Equal(5, range.Count);
        }
    }
}

#endif // UNIT_TESTS