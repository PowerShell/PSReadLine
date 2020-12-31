using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void ViTextObject_diw()
        {
            TestSetup(KeyMode.Vi);

            Test("\"hello, \ncruel world!\"", Keys(
                _.DQuote, 
                "hello, world!", _.Enter,
                "cruel world!", _.DQuote,
                _.Escape,

                // move cursor to the 'o' in 'world'
                "gg9l",

                // delete text object
                "diw",
                CheckThat(() => AssertLineIs("\"hello, !\ncruel world!\"")),
                CheckThat(() => AssertCursorLeftIs(8)),

                // delete
                "diw",
                CheckThat(() => AssertLineIs("\"hello, \ncruel world!\"")),
                CheckThat(() => AssertCursorLeftIs(7))
            ));
        }

        [SkippableFact]
        public void ViTextObject_diw_digit_arguments()
        {
            TestSetup(KeyMode.Vi);

            Test("\"hello, world!\"", Keys(
                _.DQuote, 
                "hello, world!", _.Enter,
                "cruel world!", _.DQuote,
                _.Escape,

                // move cursor to the 'o' in 'world'
                "gg9l",

                // delete text object
                "diw",
                CheckThat(() => AssertLineIs("\"hello, !\ncruel world!\"")),
                CheckThat(() => AssertCursorLeftIs(8)),

                // delete multiple text objects (spans multiple lines)
                "3diw",
                CheckThat(() => AssertLineIs("\"hello, world!\"")),
                CheckThat(() => AssertCursorLeftIs(8))
            ));
        }


        [SkippableFact]
        public void ViTextObject_diw_noop()
        {
            TestSetup(KeyMode.Vi);

            TestMustDing("\"hello, world!\ncruel world!\"", Keys(
                _.DQuote, 
                "hello, world!", _.Enter,
                "cruel world!", _.DQuote,
                _.Escape,

                // move cursor to the 'o' in 'world'
                "gg9l",

                // attempting to delete too many words must ding
                "1274diw"
            ));
        }

        [SkippableFact]
        public void ViTextObject_diw_empty_line()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test("\"\nhello, world!\n\noh, bitter world!\n\"", Keys(
                _.DQuote, _.Enter, 
                "hello, world!", _.Enter,
                _.Enter,
                "oh, bitter world!", _.Enter,
                _.DQuote, _.Escape,

                // move cursor to the second line
                "ggjj",

                // deleting single word cannot move backwards to previous line (noop)
                "diw", 
                CheckThat(() => AssertLineIs("\"\nhello, world!\n\noh, bitter world!\n\""))
            ));
        }

        [SkippableFact]
        public void ViTextObject_diw_end_of_buffer()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test("", Keys(
                _.DQuote, 
                "hello, world!", _.Enter,
                "cruel world!", _.DQuote,
                _.Escape,

                // move to end of buffer
                "G$",

                // delete text object (deletes backwards)
                "diw", CheckThat(() => AssertLineIs("\"hello, world!\ncruel world")),
                "diw", CheckThat(() => AssertLineIs("\"hello, world!\ncruel ")),
                "diw", CheckThat(() => AssertLineIs("\"hello, world!\ncruel")),
                "diw", CheckThat(() => AssertLineIs("\"hello, world!\n")),
                "diw", CheckThat(() => AssertLineIs("\"hello, world")),
                "diw", CheckThat(() => AssertLineIs("\"hello, ")),
                "diw", CheckThat(() => AssertLineIs("\"hello,")),
                "diw", CheckThat(() => AssertLineIs("\"hello")),
                "diw", CheckThat(() => AssertLineIs("\"")),
                "diw", CheckThat(() => AssertLineIs(""))
            ));
        }

        [SkippableFact]
        public void ViTextObject_diw_empty_buffer()
        {
            TestSetup(KeyMode.Vi);
            Test("", Keys(_.Escape, "diw"));
            TestMustDing("", Keys(_.Escape, "d2iw"));
        }

        [SkippableFact]
        public void ViTextObject_diw_new_lines()
        {
            TestSetup(KeyMode.Vi);

            var continuationPrefixLength = PSConsoleReadLineOptions.DefaultContinuationPrompt.Length;

            Test("\"\ntwo\n\"", Keys(
                _.DQuote, _.Enter,
                "one", _.Enter,
                _.Enter, _.Enter,
                _.Enter, _.Enter,
                _.Enter,
                "two", _.Enter, _.DQuote,
                _.Escape,

                // move to the beginning of 'one'
                "gg0j",

                // delete text object
                "2diw",
                CheckThat(() => AssertLineIs("\"\n\n\n\n\ntwo\n\"")),

                "ugg0j", // currently undo does not move the cursor to the correct position
                // delete multiple text objects (spans multiple lines)
                "3diw",
                CheckThat(() => AssertLineIs("\"\n\n\ntwo\n\"")),

                "ugg0j", // currently undo does not move the cursor to the correct position
                // delete multiple text objects (spans multiple lines)
                "4diw",
                CheckThat(() => AssertLineIs("\"\ntwo\n\""))
            ));
        }
    }
}