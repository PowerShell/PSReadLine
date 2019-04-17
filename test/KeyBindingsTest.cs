using System.Linq;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void WhatIsKey()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                _.Alt_Question,
                CheckThat(() => AssertScreenIs(2, NextLine, "what-is-key:")),
                'a',
                CheckThat(() => AssertScreenIs(2, NextLine, "a: SelfInsert - Insert the key typed"))));

            Test("", Keys(
                _.Alt_Question, _.F9,
                CheckThat(() => AssertScreenIs(2, NextLine, "F9: Key is unbound"))));

            Test("", Keys(
                _.Alt_Question, _.LeftArrow,
                CheckThat(() => AssertScreenIs(2, NextLine, "LeftArrow: BackwardChar - Move the cursor back one character"))
                ));

            TestSetup(KeyMode.Emacs);
            Test("", Keys(
                _.Alt_Question, _.Ctrl_x, _.Ctrl_u,
                CheckThat(() => AssertScreenIs(2, NextLine, "Ctrl+x,Ctrl+u: Undo - Undo a previous edit"))));
        }

        [SkippableFact]
        public void ShowKeyBindings()
        {
            // I'm too lazy to validate the output as there's a lot of output.  So
            // just run it a few times to make sure nothing crashes.

            TestSetup(KeyMode.Cmd);
            Test("", Keys(Enumerable.Repeat(_.Ctrl_Alt_Question, 3)));

            TestSetup(KeyMode.Emacs);
            Test("", Keys(Enumerable.Repeat(_.Ctrl_Alt_Question, 3)));

            TestSetup(KeyMode.Vi);
            Test("", Keys(Enumerable.Repeat(_.Ctrl_Alt_Question, 3)));
        }

        [SkippableFact]
        public void ShiftBackspace()
        {
            TestSetup(KeyMode.Cmd);
            Test("aa", Keys("aaa", _.Shift_Backspace));
        }

        [SkippableFact]
        public void ShiftEscape()
        {
            TestSetup(KeyMode.Cmd);
            Test("", Keys("aaa", _.Shift_Escape));
        }
    }
}
