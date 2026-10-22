using System;

namespace Mexerica.Tracing;

internal enum TracingMode { Color, BlackAndWhite }

internal sealed record TraceSettings
{
    public TracingMode Mode { get; init; } = TracingMode.Color;
    public int Colors { get; init; } = 16;
    public double SpeckleArea { get; init; } = 6;
    public int MinRegion { get; init; } = 16;
    public bool MergeEdgeBands { get; init; } = true;
    public double CurveTolerance { get; init; } = 0.7;
    public double CornerAngle { get; init; } = 55;
    public int CornerRun { get; init; } = 3;
    public int SmoothingPasses { get; init; } = 2;
}
