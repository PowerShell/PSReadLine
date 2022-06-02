using System.Globalization;

namespace Microsoft.PowerShell;

internal struct Point
{
    public int X;
    public int Y;

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0},{1}", X, Y);
    }
}