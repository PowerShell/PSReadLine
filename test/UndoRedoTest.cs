using Xunit;

namespace Test
{
    using PSConsoleReadLine = Microsoft.PowerShell.PSConsoleReadLine;

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

        [SkippableFact]
        public void SupportNestedEditGroups()
        {
            TestSetup(KeyMode.Vi);

            Test(" ", Keys(
                " ", _.Escape,
                CheckThat(() =>
                {
                    PSConsoleReadLine._singleton.StartEditGroup();
                    PSConsoleReadLine._singleton.EndEditGroup();
                })));

            Test("012 45", Keys(
                "012 45", _.Escape,
                "b", // back one word
                CheckThat(() =>
                {
                    PSConsoleReadLine._singleton.StartEditGroup();

                    // In an ideal situation, edit groups would be started and ended
                    // in balanced pairs of calls. However, we expose public methods
                    // that may start an edit group and rely on future actions to
                    // properly end the group.
                    //
                    // To improve robustness of the code, we allow starting "nested"
                    // edit groups and end the whole sequence of groups once. That is,
                    // a single call to _singleton.EndEditGroup() will properly record the
                    // changes made up to that point from calls to _singleton.StartEditGroup()
                    // that have been made at different points in the overall sequence of changes.

                    // Thus, the following, unneeded call, should not crash.

                    PSConsoleReadLine._singleton.StartEditGroup();

                    PSConsoleReadLine._singleton.EndEditGroup();

                    // Likewise, closing an edit group that is no longer open
                    // is now a no-op. That is, the following line should not crash either.

                    PSConsoleReadLine._singleton.EndEditGroup();
                })));
        }
    }
}
