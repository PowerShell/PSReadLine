using System;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void DigitArgumentValues()
        {
            int argValue = 0;
            TestSetup(KeyMode.Emacs,
                new KeyHandler("Ctrl+z", (key, arg) => argValue = (int)arg));

            Test("", Keys(
                _.Alt_1, _.Ctrl_z, CheckThat(() => Assert.Equal(1, argValue)),
                _.Alt_2, _.Ctrl_z, CheckThat(() => Assert.Equal(2, argValue)),
                _.Alt_3, _.Ctrl_z, CheckThat(() => Assert.Equal(3, argValue)),
                _.Alt_4, _.Ctrl_z, CheckThat(() => Assert.Equal(4, argValue)),
                _.Alt_5, _.Ctrl_z, CheckThat(() => Assert.Equal(5, argValue)),
                _.Alt_6, _.Ctrl_z, CheckThat(() => Assert.Equal(6, argValue)),
                _.Alt_7, _.Ctrl_z, CheckThat(() => Assert.Equal(7, argValue)),
                _.Alt_8, _.Ctrl_z, CheckThat(() => Assert.Equal(8, argValue)),
                _.Alt_9, _.Ctrl_z, CheckThat(() => Assert.Equal(9, argValue)),
                _.Alt_0, _.Ctrl_z, CheckThat(() => Assert.Equal(0, argValue)),
                _.Alt_1, _.Alt_2, _.Ctrl_z, CheckThat(() => Assert.Equal(12, argValue)),
                _.Alt_2, _.Alt_3, _.Alt_4, _.Ctrl_z, CheckThat(() => Assert.Equal(234, argValue)),
                _.Alt_3, _.Alt_4, _.Alt_5, _.Alt_6, _.Ctrl_z, CheckThat(() => Assert.Equal(3456, argValue)),
                _.Alt_4, _.Alt_5, _.Alt_6, _.Alt_7, _.Alt_8, _.Ctrl_z, CheckThat(() => Assert.Equal(45678, argValue)),
                _.Alt_Minus, _.Alt_1, _.Ctrl_z, CheckThat(() => Assert.Equal(-1, argValue)),
                _.Alt_Minus, _.Alt_2, _.Ctrl_z, CheckThat(() => Assert.Equal(-2, argValue)),
                _.Alt_Minus, _.Backspace, // Negative backspace should do nothing
                "a", _.Home, _.Alt_Minus, _.Delete, // Negative delete should do nothing
                _.Delete, // Delete the 'a' we added above
                _.Alt_1, _.Alt_2, _.Ctrl_z, CheckThat(() => Assert.Equal(12, argValue)),
                _.Alt_1, _.Alt_Minus, _.Ctrl_z, CheckThat(() => Assert.Equal(-1, argValue)),
                _.Alt_1, _.Alt_2, _.Alt_Minus, _.Ctrl_z, CheckThat(() => Assert.Equal(-12, argValue)),
                _.Alt_2, _.Alt_Minus, _.Alt_3, _.Alt_4, _.Ctrl_z, CheckThat(() => Assert.Equal(-234, argValue)),
                _.Alt_3, _.Alt_4, _.Alt_Minus, _.Alt_5, _.Alt_6, _.Ctrl_z, CheckThat(() => Assert.Equal(-3456, argValue)),
                _.Alt_4, _.Alt_5, _.Alt_6, _.Alt_Minus, _.Alt_7, _.Alt_8, _.Ctrl_z, CheckThat(() => Assert.Equal(-45678, argValue)),
                _.Alt_9, _.Alt_Minus, _.Alt_Minus, _.Alt_7, _.Alt_8, _.Ctrl_z, CheckThat(() => Assert.Equal(978, argValue)),
                _.Alt_Minus, _.Alt_Minus, _.Alt_7, _.Alt_8, _.Ctrl_z, CheckThat(() => Assert.Equal(78, argValue))
                ));
        }

        [SkippableFact]
        public void DigitArgumentPrompt()
        {
            TestSetup(KeyMode.Emacs);

            Test("", Keys(
                _.Alt_1, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 1")),
                _.Ctrl_g,
                _.Alt_Minus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: -1")),
                _.Ctrl_g,
                _.Alt_Minus, _.Alt_Minus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 1")),
                _.Ctrl_g,
                _.Alt_Minus, _.Alt_3, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: -3")),
                _.Ctrl_g,
                _.Alt_Minus, _.Alt_Minus, _.Alt_3, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 3")),
                _.Ctrl_g,
                _.Alt_2, _.Alt_Minus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: -2")),
                _.Ctrl_g,
                _.Alt_2, _.Alt_Minus, _.Alt_Minus, CheckThat(() => AssertScreenIs(2, NextLine, "digit-argument: 2"))
                ));
        }

        [SkippableFact]
        public void DigitArgumentWithSelfInsert()
        {
            TestSetup(KeyMode.Emacs);

            ConsoleKeyInfo GetDigitArgKey(int i)
            {
                switch (i) {
                    case 0: return _.Alt_0;
                    case 1: return _.Alt_1;
                    case 2: return _.Alt_2;
                    case 3: return _.Alt_3;
                    case 4: return _.Alt_4;
                    case 5: return _.Alt_5;
                    case 6: return _.Alt_6;
                    case 7: return _.Alt_7;
                    case 8: return _.Alt_8;
                    case 9: return _.Alt_9;
                }
                throw new Exception("Invalid digit argument key");
            }

            for (int i = 0; i < 9; i++)
            {
                var line = new string('a', i);
                var digitArgKey = GetDigitArgKey(i);
                Test(line, Keys(digitArgKey, 'a'));

                line = new string('z', i * 10);
                Test(line, Keys(digitArgKey, _.Alt_0, 'z'));
            }
        }
    }
}
