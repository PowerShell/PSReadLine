using System.Linq;
using System;
using System.Collections.Generic;


namespace Microsoft.PowerShell.PSReadLine;


public record struct EmphasisRange
{
    private static readonly int MinimumValue = -1;
    private readonly int _start;
    private readonly int _end;
    private bool _empty = false;
    public int Start => _start;
    public int End => _end;

    public static EmphasisRange Empty = new(0, 0) { _empty = true };
    public bool IsEmpty => _empty;

    public int Length { get; }

    public EmphasisRange(int start, int length)
    {
        _start = start;
        Length = length;
        _end = start + length;
        IsValid();
    }

    private void IsValid()
    {
        var state = $"\nstart is {_start}, end is {_end}.";
        if (_start > _end) throw new ArgumentException("The start must be less than the end." + state);

        if (_start < MinimumValue || _end < MinimumValue)
            throw new ArgumentException($"Index must be greater than or equal to {MinimumValue}" + state);
    }

    public bool IsIn(int index) => _start <= index && index < _end;
}

public static class Emphasis
{
    private static IEnumerable<EmphasisRange> _ranges = Array.Empty<EmphasisRange>();


    public static bool ToEmphasize(int index)
    {
        foreach (var r in _ranges)
        {
            if (r.IsIn(index))
            {
                return true;
            }
        }

        return false;
    }


    internal static void EmphasisInit()
    {
        _ranges = Array.Empty<EmphasisRange>();
    }

    public static bool IsNotEmphasisEmpty() => _ranges.Any();

    public static void SetEmphasisData(IEnumerable<EmphasisRange> ranges)
    {
        _ranges = ranges.ToArray();
    }

}