using System.Text.RegularExpressions;

namespace Overmind.Infrastructure.Telegram;

public static partial class TelegramMarkdownStripper
{
    private static readonly Regex FencedCode = FencedCodePattern();
    private static readonly Regex Links = LinkPattern();
    private static readonly Regex Bold = BoldPattern();
    private static readonly Regex Italic = ItalicPattern();
    private static readonly Regex Strikethrough = StrikethroughPattern();
    private static readonly Regex InlineCode = InlineCodePattern();

    public static string Strip(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var text = FencedCode.Replace(markdown, static match => match.Groups[1].Value.TrimEnd());
        text = Links.Replace(text, static match => match.Groups[1].Value.Length == 0
            ? match.Groups[2].Value
            : $"{match.Groups[1].Value} ({match.Groups[2].Value})");
        text = Bold.Replace(text, "$1$2");
        text = Italic.Replace(text, "$1");
        text = Strikethrough.Replace(text, "$1");
        text = InlineCode.Replace(text, "$1");
        return text.Trim();
    }

    [GeneratedRegex("```\\w*\\r?\\n?(.*?)```", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex FencedCodePattern();

    [GeneratedRegex("\\[(.*?)\\]\\((.+?)\\)", RegexOptions.Compiled)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("\\*\\*(.+?)\\*\\*|__(.+?)__", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex BoldPattern();

    [GeneratedRegex("(?<!\\*)\\*([^*\\n]+?)\\*(?!\\*)", RegexOptions.Compiled)]
    private static partial Regex ItalicPattern();

    [GeneratedRegex("~~(.+?)~~", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex StrikethroughPattern();

    [GeneratedRegex("`([^`]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodePattern();
}
