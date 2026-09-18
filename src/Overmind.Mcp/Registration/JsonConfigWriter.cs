using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Overmind.Mcp.Registration;

internal static class JsonConfigWriter
{
    public static string Write(JsonNode node, int indentSize, string newline)
    {
        ArgumentNullException.ThrowIfNull(node);

        var builder = new StringBuilder();
        WriteNode(node, depth: 0, indentSize, newline, builder);
        builder.Append(newline);
        return builder.ToString();
    }

    private static void WriteNode(JsonNode? node, int depth, int indentSize, string newline, StringBuilder builder)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject jsonObject:
                WriteObject(jsonObject, depth, indentSize, newline, builder);
                break;
            case JsonArray jsonArray:
                WriteArray(jsonArray, depth, indentSize, newline, builder);
                break;
            default:
                builder.Append(node.ToJsonString());
                break;
        }
    }

    private static void WriteObject(JsonObject jsonObject, int depth, int indentSize, string newline, StringBuilder builder)
    {
        if (jsonObject.Count == 0)
        {
            builder.Append("{}");
            return;
        }

        builder.Append('{');
        var first = true;
        foreach (var property in jsonObject)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(newline);
            AppendIndent(builder, depth + 1, indentSize);
            builder.Append(JsonSerializer.Serialize(property.Key));
            builder.Append(": ");
            WriteNode(property.Value, depth + 1, indentSize, newline, builder);
        }

        builder.Append(newline);
        AppendIndent(builder, depth, indentSize);
        builder.Append('}');
    }

    private static void WriteArray(JsonArray jsonArray, int depth, int indentSize, string newline, StringBuilder builder)
    {
        if (jsonArray.Count == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append('[');
        var first = true;
        foreach (var item in jsonArray)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(newline);
            AppendIndent(builder, depth + 1, indentSize);
            WriteNode(item, depth + 1, indentSize, newline, builder);
        }

        builder.Append(newline);
        AppendIndent(builder, depth, indentSize);
        builder.Append(']');
    }

    private static void AppendIndent(StringBuilder builder, int depth, int indentSize)
    {
        builder.Append(' ', depth * indentSize);
    }
}
