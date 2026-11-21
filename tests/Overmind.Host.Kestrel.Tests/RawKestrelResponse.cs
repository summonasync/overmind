using System.Globalization;
using System.Net;
using System.Text;

namespace Overmind.Host.Kestrel.Tests;

public sealed record RawKestrelResponse(
    HttpStatusCode StatusCode,
    IReadOnlyDictionary<string, string> Headers,
    string Body)
{
    private const string HeaderSeparator = "\r\n\r\n";

    public static RawKestrelResponse Parse(byte[] wire)
    {
        var wireText = Encoding.Latin1.GetString(wire);
        var separatorAt = wireText.IndexOf(HeaderSeparator, StringComparison.Ordinal);
        var head = separatorAt < 0 ? wireText : wireText[..separatorAt];
        var body = separatorAt < 0 ? string.Empty : wireText[(separatorAt + HeaderSeparator.Length)..];

        var lines = head.Split("\r\n");
        var status = (HttpStatusCode)int.Parse(lines[0].Split(' ')[1], CultureInfo.InvariantCulture);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colonAt = line.IndexOf(':');
            if (colonAt > 0)
            {
                headers[line[..colonAt].Trim()] = line[(colonAt + 1)..].Trim();
            }
        }

        return new RawKestrelResponse(status, headers, body);
    }

    public string Header(string name) => Headers.GetValueOrDefault(name, string.Empty);
}
