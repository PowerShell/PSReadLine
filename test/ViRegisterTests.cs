using System.Text;
using Microsoft.PowerShell;
using Xunit;

namespace Test;

public sealed class ViRegisterTests
{
    [Fact]
    public void ViRegister_Empty_LinewisePasteBefore()
    {
        const string yanked = "line1";

        var register = new PSConsoleReadLine.ViRegister(null);
        register.LinewiseRecord(yanked);

        // system under test

        var buffer = new StringBuilder();
        const int position = 2;

        var newPosition = register.PasteBefore(buffer, position);

        // assert expectations

        Assert.Equal("line1\n", buffer.ToString());
        Assert.Equal(0, newPosition);
    }

    [Fact]
    public void ViRegister_Fragment_LinewisePasteBefore()
    {
        const string yanked = "line1";

        var register = new PSConsoleReadLine.ViRegister(null);
        register.LinewiseRecord(yanked);

        // system under test

        var buffer = new StringBuilder("line2");
        const int position = 2;

        var newPosition = register.PasteBefore(buffer, position);

        // assert expectations

        Assert.Equal("line1\nline2", buffer.ToString());
        Assert.Equal(0, newPosition);
    }

    [Fact]
    public void ViRegister_Lines_LinewisePasteBefore()
    {
        const string yanked = "\nline1\nline2";

        var register = new PSConsoleReadLine.ViRegister(null);
        register.LinewiseRecord(yanked);

        // system under test

        var buffer = new StringBuilder("\nline3");
        const int position = 2;

        var newPosition = register.PasteBefore(buffer, position);

        // assert expectations

        Assert.Equal("\nline1\nline2\nline3", buffer.ToString());
        Assert.Equal(1, newPosition);
    }

    [Fact]
    public void ViRegister_Fragment_LinewisePasteAfter_Fragment()
    {
        const string yanked = "line2";

        var register = new PSConsoleReadLine.ViRegister(null);
        register.LinewiseRecord(yanked);

        // system under test

        var buffer = new StringBuilder("line1");
        const int position = 2;

        var newPosition = register.PasteAfter(buffer, position);

        // assert expectations

        Assert.Equal("line1\nline2", buffer.ToString());
        Assert.Equal(6, newPosition);
    }

    [Fact]
    public void ViRegister_Fragment_LinewisePasteAfter_Lines()
    {
        const string yanked = "line2";

        var register = new PSConsoleReadLine.ViRegister(null);
        register.LinewiseRecord(yanked);

        // system under test

        var buffer = new StringBuilder("line1\n");
        const int position = 2;

        var newPosition = register.PasteAfter(buffer, position);

        // assert expectations

        Assert.Equal("line1\nline2\n", buffer.ToString());
        Assert.Equal(6, newPosition);
    }

    [Fact]
    public void ViRegister_Lines_LinewisePasteAfter_Fragment()
    {
        const string yanked = "\nline2\nline3";

        var register = new PSConsoleReadLine.ViRegister(null);
        register.LinewiseRecord(yanked);

        // system under test

        var buffer = new StringBuilder("line1");
        const int position = 2;

        var newPosition = register.PasteAfter(buffer, position);

        // assert expectations

        Assert.Equal("line1\nline2\nline3", buffer.ToString());
        Assert.Equal(6, newPosition);
    }

    [Fact]
    public void ViRegister_Lines_LinewisePasteAfter_Lines()
    {
        const string yanked = "\nline2\nline3";

        var register = new PSConsoleReadLine.ViRegister(null);
        register.LinewiseRecord(yanked);

        // system under test

        var buffer = new StringBuilder("line1\n");
        const int position = 2;

        var newPosition = register.PasteAfter(buffer, position);

        // assert expectations

        Assert.Equal("line1\nline2\nline3\n", buffer.ToString());
        Assert.Equal(6, newPosition);
    }
}