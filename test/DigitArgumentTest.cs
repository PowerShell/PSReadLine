using System;
using Microsoft.PowerShell;
using Xunit;

using PSKeyInfo = System.ConsoleKeyInfo;

namespace Test
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class ReadLine
    {
        [Fact]
        public void DigitArgumentValues()
        {
            int argValue = 0;
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Ctrl+z", (key, arg) => argValue = (int)arg));

            Test("", Keys(
                _.Alt1, _.CtrlZ, CheckThat(() => Assert.Equal(1, argValue)),
                _.Alt2, _.CtrlZ, CheckThat(() => Assert.Equal(2, argValue)),
                _.Alt3, _.CtrlZ, CheckThat(() => Assert.Equal(3, argValue)),
                _.Alt4, _.CtrlZ, CheckThat(() => Assert.Equal(4, argValue)),
                _.Alt5, _.CtrlZ, CheckThat(() => Assert.Equal(5, argValue)),
                _.Alt6, _.CtrlZ, CheckThat(() => Assert.Equal(6, argValue)),
                _.Alt7, _.CtrlZ, CheckThat(() => Assert.Equal(7, argValue)),
                _.Alt8, _.CtrlZ, CheckThat(() => Assert.Equal(8, argValue)),
                _.Alt9, _.CtrlZ, CheckThat(() => Assert.Equal(9, argValue)),
                _.Alt0, _.CtrlZ, CheckThat(() => Assert.Equal(0, argValue)),
                _.Alt1, _.Alt2, _.CtrlZ, CheckThat(() => Assert.Equal(12, argValue)),
                _.Alt2, _.Alt3, _.Alt4, _.CtrlZ, CheckThat(() => Assert.Equal(234, argValue)),
                _.Alt3, _.Alt4, _.Alt5, _.Alt6, _.CtrlZ, CheckThat(() => Assert.Equal(3456, argValue)),
                _.Alt4, _.Alt5, _.Alt6, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.Equal(45678, argValue)),
                _.AltMinus, _.Alt1, _.CtrlZ, CheckThat(() => Assert.Equal(-1, argValue)),
                _.AltMinus, _.Alt2, _.CtrlZ, CheckThat(() => Assert.Equal(-2, argValue)),
                _.AltMinus, _.Backspace, // Negative backspace should do nothing
                "a", _.Home, _.AltMinus, _.Delete, // Negative delete should do nothing
                _.Delete, // Delete the 'a' we added above
                _.Alt1, _.Alt2, _.CtrlZ, CheckThat(() => Assert.Equal(12, argValue)),
                _.Alt1, _.AltMinus, _.CtrlZ, CheckThat(() => Assert.Equal(-1, argValue)),
                _.Alt1, _.Alt2, _.AltMinus, _.CtrlZ, CheckThat(() => Assert.Equal(-12, argValue)),
                _.Alt2, _.AltMinus, _.Alt3, _.Alt4, _.CtrlZ, CheckThat(() => Assert.Equal(-234, argValue)),
                _.Alt3, _.Alt4, _.AltMinus, _.Alt5, _.Alt6, _.CtrlZ, CheckThat(() => Assert.Equal(-3456, argValue)),
                _.Alt4, _.Alt5, _.Alt6, _.AltMinus, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.Equal(-45678, argValue)),
                _.Alt9, _.AltMinus, _.AltMinus, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.Equal(978, argValue)),
                _.AltMinus, _.AltMinus, _.Alt7, _.Alt8, _.CtrlZ, CheckThat(() => Assert.Equal(78, argValue))
                ));
        }

        [Fact]
        public void DigitArgumentPrompt()
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

        [Fact]
        public void DigitArgumentWithSelfInsert()
        {
            TestSetup(KeyMode.Emacs);

            for (int i = 0; i < 9; i++)
            {
                var line = new string('a', i);
                var digitArgKey = new PSKeyInfo(
                    (char)('0' + i), ConsoleKey.D0 + i, false, true, false);
                Test(line, Keys(digitArgKey, 'a'));

                line = new string('z', i * 10);
                Test(line, Keys(digitArgKey, _.Alt0, 'z'));
            }
        }
    }
}
