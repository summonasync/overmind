namespace Overmind.Infrastructure.Wave;

public static class WaveMentions
{
    public const string Escape = "\\";

    public static string LikePattern(string alias) => $"% {alias.Replace("_", "\\_")} %";
}
