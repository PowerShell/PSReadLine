using Microsoft.PowerShell;
using System.Text;
using Xunit;

namespace Test
{
    public sealed class StringBuilderCharacterExtensionsTests
    {
        [Fact]
        public void StringBuilderCharacterExtensions_IsVisibleBlank()
        {
            var buffer = new StringBuilder(" \tn");

            // system under test

            Assert.True(buffer.IsVisibleBlank(0));
            Assert.True(buffer.IsVisibleBlank(1));
            Assert.False(buffer.IsVisibleBlank(2));
        }

        [Fact]
        public void StringBuilderCharacterExtensions_InWord()
        {
            var buffer = new StringBuilder("hello, world!");
            const string wordDelimiters = " ";

            // system under test

            Assert.True(buffer.InWord(2, wordDelimiters));
            Assert.True(buffer.InWord(5, wordDelimiters));
        }

        [Fact]
        public void StringBuilderCharacterExtensions_IsWhiteSpace()
        {
            var buffer = new StringBuilder("a c");


            // system under test

            Assert.False(buffer.IsWhiteSpace(0));
            Assert.True(buffer.IsWhiteSpace(1));
            Assert.False(buffer.IsWhiteSpace(2));
        }
    }
}
