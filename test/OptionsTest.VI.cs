using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [Fact]
        public void ViGetKeyHandlers()
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
