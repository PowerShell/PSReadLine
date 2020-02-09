using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
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

            var handlers = PSConsoleReadLine.GetKeyHandlers(Chord: new string[] { "home" });
            Assert.NotEmpty(handlers);
            foreach (var handler in handlers)
            {
                Assert.Contains("Home", handler.Key);
            }

            handlers = PSConsoleReadLine.GetKeyHandlers(Chord: new string[] { "d,0" });
            Assert.NotEmpty(handlers);
            foreach (var handler in handlers)
            {
                Assert.Equal("<d,0>", handler.Key);
            }
        }
    }
}
