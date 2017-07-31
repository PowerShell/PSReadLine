using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {

        [TestMethod]
        public void ViTestGetKeyHandlers()
        {
            TestSetup(KeyMode.Vi);

            foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: false, includeUnbound: true))
            {
                Assert.AreEqual("Unbound", handler.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Function));
                Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Description));
            }

            foreach (var handler in PSConsoleReadLine.GetKeyHandlers(includeBound: true, includeUnbound: false))
            {
                Assert.AreNotEqual("Unbound", handler.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Function));
                Assert.IsFalse(string.IsNullOrWhiteSpace(handler.Description));
            }
        }
    }
}
