using Test;
using Xunit.Abstractions;

namespace UnitTestPSReadLine;

public abstract class MyReadLine : ReadLineBase
{
    protected MyReadLine(ConsoleFixture fixture, ITestOutputHelper output, string lang, string os) : base(fixture,
        output,
        lang, os)
    {
    }
}