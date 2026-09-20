namespace Overmind.Core;

public sealed class VectorDimensionMismatchException : Exception
{
    public int? PreviousDimensions { get; }
    public int NewDimensions { get; }

    public VectorDimensionMismatchException(int? previousDimensions, int newDimensions)
        : base(
            $"vector dimensions changed from {previousDimensions?.ToString() ?? "unknown"} to {newDimensions}")
    {
        PreviousDimensions = previousDimensions;
        NewDimensions = newDimensions;
    }
}
