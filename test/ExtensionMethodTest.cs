using System;
using System.Linq;
using UnitTestPSReadLine;
using Xunit;

namespace Test;

public class ExtensionMethodTest
{
    [SkippableFact]
    public void TestShowContext()
    {
        string[] context = {"DSadf_L03ad2KF", @"[adj] 公众的，大众的"};
        foreach (var contextItem in context)
        {
            var arr = GetCHAR_INFOArray(contextItem);
            Assert.Equal(contextItem, arr.ShowContext());
        }
    }

    private CHAR_INFO[] GetCHAR_INFOArray(string context)
    {
        return context.Select(x => new CHAR_INFO(x, ConsoleColor.Black, ConsoleColor.White))
            .ToArray();
    }
}