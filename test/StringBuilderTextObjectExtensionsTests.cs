using Microsoft.PowerShell;
using System.Text;
using Xunit;

namespace Test
{
    public sealed class StringBuilderTextObjectExtensionsTests
    {
        [Fact]
        public void StringBuilderTextObjectExtensions_ViFindBeginningOfWordObjectBoundary()
        {
            const string wordDelimiters = PSConsoleReadLineOptions.DefaultWordDelimiters;

            var buffer = new StringBuilder("Hello, world!\ncruel   world.\none\n\n\n\n\ntwo\n three four.");
            Assert.Equal(0, buffer.ViFindBeginningOfWordObjectBoundary(1, wordDelimiters));
        }

        [Fact]
        public void StringBuilderTextObjectExtensions_ViFindBeginningOfWordObjectBoundary_whitespace()
        {
            const string wordDelimiters = PSConsoleReadLineOptions.DefaultWordDelimiters;

            var buffer = new StringBuilder("Hello,   world!");
            Assert.Equal(6, buffer.ViFindBeginningOfWordObjectBoundary(7, wordDelimiters));
        }

        [Fact]
        public void StringBuilderTextObjectExtensions_ViFindBeginningOfWordObjectBoundary_backwards()
        {
            const string wordDelimiters = PSConsoleReadLineOptions.DefaultWordDelimiters;

            var buffer = new StringBuilder("Hello!\nworld!");
            Assert.Equal(5, buffer.ViFindBeginningOfWordObjectBoundary(6, wordDelimiters));
        }

        [Fact]
        public void StringBuilderTextObjectExtensions_ViFindBeginningOfWordObjectBoundary_end_of_buffer()
        {
            const string wordDelimiters = PSConsoleReadLineOptions.DefaultWordDelimiters;

            var buffer = new StringBuilder("Hello, world!");
            Assert.Equal(12, buffer.ViFindBeginningOfWordObjectBoundary(buffer.Length, wordDelimiters));
        }

        [Fact]
        public void StringBuilderTextObjectExtensions_ViFindBeginningOfNextWordObjectBoundary()
        {
            const string wordDelimiters = PSConsoleReadLineOptions.DefaultWordDelimiters;

            var buffer = new StringBuilder("Hello, world!\ncruel world.\none\n\n\n\n\ntwo\n three four.");

            // Words |Hello|,| |world|!|\n|cruel |world|.|\n|one\n\n|\n\n|\n|two|\n |three| |four|.|
            // Pos    01234 5 6 78901 2 _3 456789 01234 5 _6 789_0_1 _2_3 _4 567 _89 01234 5 6789 0
            // Pos    0            1              2              3                   4            5

            // system under test

            Assert.Equal(5, buffer.ViFindBeginningOfNextWordObjectBoundary(0, wordDelimiters));
            Assert.Equal(6, buffer.ViFindBeginningOfNextWordObjectBoundary(5, wordDelimiters));
            Assert.Equal(7, buffer.ViFindBeginningOfNextWordObjectBoundary(6, wordDelimiters));
            Assert.Equal(12, buffer.ViFindBeginningOfNextWordObjectBoundary(7, wordDelimiters));
            Assert.Equal(13, buffer.ViFindBeginningOfNextWordObjectBoundary(12, wordDelimiters));
            Assert.Equal(19, buffer.ViFindBeginningOfNextWordObjectBoundary(13, wordDelimiters));
            Assert.Equal(20, buffer.ViFindBeginningOfNextWordObjectBoundary(19, wordDelimiters));
            Assert.Equal(25, buffer.ViFindBeginningOfNextWordObjectBoundary(20, wordDelimiters));
            Assert.Equal(26, buffer.ViFindBeginningOfNextWordObjectBoundary(25, wordDelimiters));
            Assert.Equal(30, buffer.ViFindBeginningOfNextWordObjectBoundary(26, wordDelimiters));
            Assert.Equal(32, buffer.ViFindBeginningOfNextWordObjectBoundary(30, wordDelimiters));
            Assert.Equal(34, buffer.ViFindBeginningOfNextWordObjectBoundary(32, wordDelimiters));
            Assert.Equal(38, buffer.ViFindBeginningOfNextWordObjectBoundary(34, wordDelimiters));
            Assert.Equal(40, buffer.ViFindBeginningOfNextWordObjectBoundary(38, wordDelimiters));
            Assert.Equal(45, buffer.ViFindBeginningOfNextWordObjectBoundary(40, wordDelimiters));
            Assert.Equal(46, buffer.ViFindBeginningOfNextWordObjectBoundary(45, wordDelimiters));
            Assert.Equal(50, buffer.ViFindBeginningOfNextWordObjectBoundary(46, wordDelimiters));
        }
    }
}
