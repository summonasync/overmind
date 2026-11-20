using System.Text.Json;

namespace Overmind.Host.Kestrel.Tests;

public sealed class CdpChatHarness(CdpPage page)
{
    public const string ComposerSelector = "textarea[aria-label=\"Message the agent\"]";

    private readonly Dictionary<string, string> _seeded = new(StringComparer.Ordinal);
    private string? _seedScriptId;

    public async Task SeedStateAsync(string key, string value, CancellationToken ct)
    {
        _seeded[key] = value;
        if (_seedScriptId is not null)
        {
            await page.SendAsync(
                "Page.removeScriptToEvaluateOnNewDocument",
                new { identifier = _seedScriptId },
                ct);
        }

        var seeds = JsonSerializer.Serialize(_seeded);
        var source =
            "(() => { const seeds = " + seeds + "; "
            + "for (const [key, value] of Object.entries(seeds)) { window.localStorage.setItem(key, value); } })();";
        var result = await page.SendAsync("Page.addScriptToEvaluateOnNewDocument", new { source }, ct);
        _seedScriptId = result.GetProperty("identifier").GetString();
    }

    public async Task<string> SnapshotDomAsync(CancellationToken ct) =>
        (await page.EvaluateAsync("document.documentElement.outerHTML", ct)).GetString()
        ?? throw new InvalidOperationException("DOM snapshot returned no value");

    public async Task<string> TypeIntoComposerAsync(string text, CancellationToken ct)
    {
        var script =
            "(async () => {"
            + " const composer = document.querySelector('" + ComposerSelector + "');"
            + " if (composer === null) { return null; }"
            + " const setter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value').set;"
            + " setter.call(composer, " + JsonSerializer.Serialize(text) + ");"
            + " composer.dispatchEvent(new Event('input', { bubbles: true }));"
            + " await new Promise((resolve) => setTimeout(resolve, 0));"
            + " return composer.value; })()";
        var value = await page.EvaluateAsync(script, ct);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidOperationException("agent composer not found in the mounted page");
    }

    public async Task ClickSendAsync(CancellationToken ct)
    {
        var script =
            "(async () => {"
            + " for (let attempt = 0; attempt < 100; attempt++) {"
            + "  const button = Array.from(document.querySelectorAll('form button[type=\"submit\"]'))"
            + "   .find((candidate) => candidate.textContent.trim() === 'Send');"
            + "  if (button && !button.disabled) { button.click(); return true; }"
            + "  await new Promise((resolve) => setTimeout(resolve, 50)); }"
            + " return false; })()";
        var clicked = await page.EvaluateAsync(script, ct);
        if (clicked.ValueKind != JsonValueKind.True)
        {
            throw new InvalidOperationException("the Send button never became clickable");
        }
    }
}
