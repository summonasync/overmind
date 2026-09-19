namespace Overmind.Infrastructure.Planning;

internal sealed class BacklogImportNotes
{
    private readonly HashSet<string> _newKeyNames = new(StringComparer.Ordinal);

    public List<string> Skipped { get; } = [];

    public List<string> Preserved { get; } = [];

    public List<string> CreatedFallbacks { get; } = [];

    public List<BacklogImportNewKey> NewKeys { get; } = [];

    public void PreservedKeys(string relative, IEnumerable<string> keys)
    {
        var ordered = keys.OrderBy(key => key, StringComparer.Ordinal).ToList();
        Preserved.Add($"preserved: {relative} ({string.Join(", ", ordered)})");
    }

    public void CreatedFallback(string relative, DateTime modifiedAt) =>
        CreatedFallbacks.Add(
            $"created: {relative} (no created token; fell back to file mtime {modifiedAt:yyyy-MM-dd})");

    public void NewKey(string name, string color)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (_newKeyNames.Add(normalized))
        {
            NewKeys.Add(new BacklogImportNewKey(normalized, color));
        }
    }
}
