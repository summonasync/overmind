using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Overmind.Infrastructure.Planning;

public static class FrontmatterKeyPalette
{
    public static readonly IReadOnlyList<string> Colors =
    [
        "#22c55e",
        "#f59e0b",
        "#60a5fa",
        "#38bdf8",
        "#a855f7",
        "#818cf8",
        "#2dd4bf",
        "#a3e635",
        "#fb923c",
        "#22d3ee",
        "#f472b6",
        "#e879f9",
        "#ef4444",
        "#fb7185"
    ];

    public static readonly IReadOnlyList<FrontmatterKeySeed> SeedKeys =
    [
        new FrontmatterKeySeed("status", "#22c55e"),
        new FrontmatterKeySeed("review", "#f59e0b"),
        new FrontmatterKeySeed("version", "#60a5fa"),
        new FrontmatterKeySeed("created", "#38bdf8"),
        new FrontmatterKeySeed("feature", "#a855f7"),
        new FrontmatterKeySeed("task", "#818cf8"),
        new FrontmatterKeySeed("slug", "#2dd4bf"),
        new FrontmatterKeySeed("validated", "#a3e635"),
        new FrontmatterKeySeed("depends_on", "#fb923c"),
        new FrontmatterKeySeed("phase", "#22d3ee"),
        new FrontmatterKeySeed("decision", "#f472b6"),
        new FrontmatterKeySeed("created_during", "#e879f9")
    ];

    public static string ColorFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant()));
        var index = (uint)BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(0, 4)) % (uint)Colors.Count;

        return Colors[(int)index];
    }
}
