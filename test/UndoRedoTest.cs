using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void UndoneEditsShouldBeRemovedBeforeEndingEditGroup()
        {
            TestSetup(KeyMode.Vi);

            Test("yuiogh", Keys(
                "yuiogh", _.Escape, CheckThat(() => AssertLineIs("yuiogh")),
                _.C, CheckThat(() => AssertLineIs("yuiog")),
                "897", CheckThat(() => AssertLineIs("yuiog897")),
                _.Ctrl_z, CheckThat(() => AssertLineIs("yuiog89")),
                _.Escape, _.u, CheckThat(() => AssertLineIs("yuiogh"))
            ));
        }

        [SkippableFact]
        public void ProperlyUpdateEditGroupStartMarkWhenRemovingUndoneEdits()
        {
            TestSetup(KeyMode.Vi);

            Test("yuiogh", Keys(
                "yuiogh", _.Escape, CheckThat(() => AssertLineIs("yuiogh")),
                _.LeftArrow, _.C, CheckThat(() => AssertLineIs("yuio")),
                "789", CheckThat(() => AssertLineIs("yuio789")),
                _.Ctrl_z, CheckThat(() => AssertLineIs("yuio78")),
                '1', _.Escape, _.u, CheckThat(() => AssertLineIs("yuiogh"))
            ));
        }

        [SkippableFact]
        public void ProperlySetEditGroupStartMarkWhenLoopInHistory()
        {
            TestSetup(KeyMode.Vi);

            Test("ok", Keys("ok"));

            // The _editGroupStart mark for an history entry should always be -1, meaning no start mark.
            Test("ok", Keys(
                "yuiogh", _.Escape, CheckThat(() => AssertLineIs("yuiogh")),
                _.C, CheckThat(() => AssertLineIs("yuiog")),
                _.UpArrow, CheckThat(() => AssertLineIs("ok")),
                _.Escape));

            // The _editGroupStart mark should be restored when loop back to the current command line.
            Test("yuiogh", Keys(
                "yuiogh", _.Escape, CheckThat(() => AssertLineIs("yuiogh")),
                _.C, CheckThat(() => AssertLineIs("yuiog")),
                _.UpArrow, CheckThat(() => AssertLineIs("ok")),
                _.DownArrow, CheckThat(() => AssertLineIs("yuiog")),
                "123", CheckThat(() => AssertLineIs("yuiog123")),
                _.Escape, _.u, CheckThat(() => AssertLineIs("yuiogh"))
            ));
        }
    }
}
