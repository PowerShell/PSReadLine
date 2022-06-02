using System.Text;
using Microsoft.PowerShell;
using Xunit;

namespace Test;

public sealed class StringBuilderLinewiseExtensionsTests
{
    [Fact]
    public void StringBuilderLinewiseExtensions_LinewiseYank_Fragment()
    {
        var buffer = new StringBuilder("line1\nline2");

        // system under test

        var range = buffer.GetRange(1, 42);

        // assert

        Assert.Equal(5, range.Offset);
        Assert.Equal(6, range.Count);
    }

    [Fact]
    public void StringBuilderLinewiseExtensions_LinewiseYank_Line()
    {
        var buffer = new StringBuilder("line1\nline2\n");

        // system under test

        var range = buffer.GetRange(1, 42);

        // assert

        Assert.Equal(5, range.Offset);
        Assert.Equal(7, range.Count);
    }

    [Fact]
    public void StringBuilderLinewiseExtensions_LinewiseYank_Lines()
    {
        var buffer = new StringBuilder("line1\nline2\nline3\nline4");

        // system under test

        var range = buffer.GetRange(1, 2);

        // assert

        Assert.Equal(5, range.Offset);
        Assert.Equal(12, range.Count);
    }
}