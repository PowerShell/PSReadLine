using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestDigitArgumentValues()
        {
            int argValue = 0;
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Ctrl+Z", (key, arg) => argValue = (int)arg));
            
            Test("", Keys(
                _.Alt1, _.CtrlZ, CheckThat(() => Assert.AreEqual(1, argValue)),
                _.Alt2, _.CtrlZ, CheckThat(() => Assert.AreEqual(2, argValue)),
                _.Alt3, _.CtrlZ, CheckThat(() => Assert.AreEqual(3, argValue)),
                _.Alt4, _.CtrlZ, CheckThat(() => Assert.AreEqual(4, argValue)),
                _.Alt5, _.CtrlZ, CheckThat(() => Assert.AreEqual(5, argValue)),
                _.Alt6, _.CtrlZ, CheckThat(() => Assert.AreEqual(6, argValue)),
                _.Alt7, _.CtrlZ, CheckThat(() => Assert.AreEqual(7, argValue)),
                _.Alt8, _.CtrlZ, CheckThat(() => Assert.AreEqual(8, argValue)),
                _.Alt9, _.CtrlZ, CheckThat(() => Assert.AreEqual(9, argValue)),
                _.Alt0, _.CtrlZ, CheckThat(() => Assert.AreEqual(0, argValue)),
                _.Alt1, _.Alt2, _.CtrlZ, CheckThat(() => Assert.AreEqual(12, argValue)),
                _.Alt2, _.Alt3, _.Alt4, _.CtrlZ, CheckThat(() => Assert.AreEqual(234, argValue)),
                _.Alt3, _.Alt4, _.Alt5, _.Alt6, _.CtrlZ, CheckThat(() => Assert.AreEqual(3456, argValue)),
                _.Alt4, _.Alt5, _.Alt6, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.AreEqual(45678, argValue)),
                _.AltMinus, _.Alt1, _.CtrlZ, CheckThat(() => Assert.AreEqual(-1, argValue)),
                _.AltMinus, _.Alt2, _.CtrlZ, CheckThat(() => Assert.AreEqual(-2, argValue)),
                _.Alt1, _.Alt2, _.CtrlZ, CheckThat(() => Assert.AreEqual(12, argValue)),
                _.Alt1, _.AltMinus, _.CtrlZ, CheckThat(() => Assert.AreEqual(-1, argValue)),
                _.Alt1, _.Alt2, _.AltMinus, _.CtrlZ, CheckThat(() => Assert.AreEqual(-12, argValue)),
                _.Alt2, _.AltMinus, _.Alt3, _.Alt4, _.CtrlZ, CheckThat(() => Assert.AreEqual(-234, argValue)),
                _.Alt3, _.Alt4, _.AltMinus, _.Alt5, _.Alt6, _.CtrlZ, CheckThat(() => Assert.AreEqual(-3456, argValue)),
                _.Alt4, _.Alt5, _.Alt6, _.AltMinus, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.AreEqual(-45678, argValue)),
                _.Alt9, _.AltMinus, _.AltMinus, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.AreEqual(978, argValue)),
                _.AltMinus, _.AltMinus, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.AreEqual(78, argValue))
                ));
        }

        [TestMethod]
        public void TestDigitArgumentPrompt()
        {
            TestSetup(KeyMode.Emacs);
            
            Test("", Keys(
                _.Alt1, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 1")),
                _.CtrlG,
                _.AltMinus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: -1")),
                _.CtrlG,
                _.AltMinus, _.AltMinus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 1")),
                _.CtrlG,
                _.AltMinus, _.Alt3, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: -3")),
                _.CtrlG,
                _.AltMinus, _.AltMinus, _.Alt3, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 3")),
                _.CtrlG,
                _.Alt2, _.AltMinus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: -2")),
                _.CtrlG,
                _.Alt2, _.AltMinus, _.AltMinus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 2"))
                ));
        }

        [TestMethod]
        public void TestDigitArgumentWithSelfInsert()
        {
            TestSetup(KeyMode.Emacs);

            for (int i = 0; i < 9; i++)
            {
                var line = new string('a', i);
                var digitArgKey = new ConsoleKeyInfo(
                    (char)((int)'0' + i), (ConsoleKey)(ConsoleKey.D0 + i), false, true, false);
                Test(line, Keys(digitArgKey, 'a'));

                line = new string('z', i * 10);
                Test(line, Keys(digitArgKey, _.Alt0, 'z'));
            }
        }
    }
}
