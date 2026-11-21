namespace Overmind.Host.Kestrel.Tests;

public static class NodeLocator
{
    private static readonly string[] ExtraDirectories = ["/usr/bin", "/usr/local/bin"];

    public static NodeToolchain? Find()
    {
        var node = ExecutableSearch.Find(
            Environment.GetEnvironmentVariable("OVERMIND_TEST_NODE_BIN"),
            ["node"],
            ExtraDirectories);
        if (node is null)
        {
            return null;
        }

        var npm = ExecutableSearch.Find(null, ["npm"], ExtraDirectories);
        return npm is null ? null : new NodeToolchain(node, npm);
    }
}
