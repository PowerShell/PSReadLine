using Microsoft.PowerShell;
using Xunit;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {

        [Fact]
        public void ViTestGetKeyHandlers()
        {
            TestSetup(KeyMode.Vi);

            foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: false, includeUnbound: true))
            {
                Assert.Equal("Unbound", handler.Key);
                Assert.False(string.IsNullOrWhiteSpace(handler.Function));
                Assert.False(string.IsNullOrWhiteSpace(handler.Description));
            }

            foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: true, includeUnbound: false))
            {
                Assert.NotEqual("Unbound", handler.Key);
                Assert.False(string.IsNullOrWhiteSpace(handler.Function));
                Assert.False(string.IsNullOrWhiteSpace(handler.Description));
            }
        }
    }
}
