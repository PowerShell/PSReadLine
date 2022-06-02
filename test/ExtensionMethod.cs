using System.Collections.Generic;
using System.Linq;
using System.Text;
using Test;

namespace UnitTestPSReadLine;

public static class ExtensionMethod
{
    public static string ShowContext(this IEnumerable<CHAR_INFO> arr)
    {
        var sb = new StringBuilder();
        sb.Append(arr.Select(x => (char) x.UnicodeChar).ToArray());
        return sb.ToString();
    }
}