using ModelContextProtocol;

namespace Overmind.Mcp;

internal static class TextCapLint
{
    internal static void Cap(string text, int cap)
    {
        if (text.Length <= cap)
        {
            return;
        }

        throw new McpException($"text is {text.Length} chars; cap is {cap}; cut {text.Length - cap}");
    }
}
