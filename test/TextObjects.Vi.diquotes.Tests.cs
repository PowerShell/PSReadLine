using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableTheory]
        [InlineData("a 'quoted' text", "di'", "a '' text")]
        [InlineData("a \"quoted\" text", "di\"", "a \"\" text")]
        public void ViTextObject_diquotes(string input, string motion, string expected)
        {
            TestSetup(KeyMode.Vi);

            Test(expected, Keys(
                input, _.Escape,
                "Fo", // move backwards to the letter 'o' inside the quotes

                // delete text object (delete inner quotes)
                // this will find the surrounding quotes at the position

                motion,

                CheckThat(() => AssertCursorLeftIs(3))
            ));

            Test(expected, Keys(
                input, _.Escape,
                "0", // move to beginning of logical line

                // delete text object (delete inner quotes)
                // this will look forward and find the quoted text

                motion,

                CheckThat(() => AssertCursorLeftIs(3))
            ));
        }

        [SkippableTheory]
        [InlineData("a 'quoted' text", "42di'", "a  text")]
        [InlineData("a \"quoted\" text", "42di\"", "a  text")]
        public void ViTextObject_diquotes_argument(string input, string motion, string expected)
        {
            TestSetup(KeyMode.Vi);

            Test(expected, Keys(
                input, _.Escape,
                "Fo", // move backwards to the letter 'o' inside the quotes

                // delete text object (delete inner quotes)
                // no matter how many times, we only care about more than once or not

                motion,
                CheckThat(() => AssertCursorLeftIs(2))
            ));
        }

        [SkippableTheory]
        [InlineData("an 'incorrectly quoted text", "di'", "A'", "an 'incorrectly quoted text'")]
        [InlineData("an \"incorrectly quoted text", "di\"", "A\"", "an \"incorrectly quoted text\"")]
        public void ViTextObject_diquotes_noop(string input, string motion, string fix, string expected)
        {
            TestSetup(KeyMode.Vi);

            TestMustDing("a text", Keys(
                "a text", _.Escape,

                // attempt to delete non-existent text object must ding
                // no matter how many times, we only care about more than once or not

                motion
            ));

            TestMustDing(expected, Keys(
                input, _.Escape,

                // even though there is a starting delimiter, 
                // the motion cannot find an ending delimiter

                motion,

                // we must make the input a valid line
                // move to the end of the buffer and add the missing quote

                fix
            ));
        }
    }
}
