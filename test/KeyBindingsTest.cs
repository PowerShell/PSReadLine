﻿using System.Linq;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class ReadLine
    {
        [Fact]
        public void WhatIsKey()
        {
            TestSetup(KeyMode.Cmd);

            Test("", Keys(
                _.AltQuestion,
                CheckThat(() => AssertScreenIs(2, NextLine, "what-is-key:")),
                'a',
                CheckThat(() => AssertScreenIs(2, NextLine, "a: SelfInsert - Insert the key typed"))));

            Test("", Keys(
                _.AltQuestion, _.F24,
                CheckThat(() => AssertScreenIs(2, NextLine, "F24: Key is unbound"))));

            Test("", Keys(
                _.AltQuestion, _.LeftArrow,
                CheckThat(() => AssertScreenIs(2, NextLine, "LeftArrow: BackwardChar - Move the cursor back one character"))
                ));

            TestSetup(KeyMode.Emacs);
            Test("", Keys(
                _.AltQuestion, _.CtrlX, _.CtrlU,
                CheckThat(() => AssertScreenIs(2, NextLine, "Ctrl+x,Ctrl+u: Undo - Undo a previous edit"))));
        }

        [Fact]
        public void ShowKeyBindings()
        {
            // I'm too lazy to validate the output as there's a lot of output.  So
            // just run it a few times to make sure nothing crashes.

            TestSetup(KeyMode.Cmd);
            Test("", Keys(Enumerable.Repeat(_.CtrlAltQuestion, 3)));

            TestSetup(KeyMode.Emacs);
            Test("", Keys(Enumerable.Repeat(_.CtrlAltQuestion, 3)));

            TestSetup(KeyMode.Vi);
            Test("", Keys(Enumerable.Repeat(_.CtrlAltQuestion, 3)));
        }
    }
}
