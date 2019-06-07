using System;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void DeadKeyShouldBeIgnored()
        {
            Skip.If(this.Fixture.Lang != "fr-FR", "The dead key test requires Keyboard layout to be set to 'fr-FR'");
            TestSetup(KeyMode.Cmd);

            Test("aa", Keys("aa", _.DeadKey_Caret));
            Test("aab", Keys("aa", _.DeadKey_Caret, 'b'));
            Test("aaâ", Keys("aa", _.DeadKey_Caret_A));
        }
    }
}
