using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Trait("Surface", "Browser")]
[Collection("overmind kestrel browser")]
public sealed class CdpHarnessSelfTests(BrowserHostFixture fixture)
{
    private const string ConversationTitle = "harness smoke conversation";
    private const string SelectionStorageKey = "overmind.agent-chat.selected-conversation";

    [BrowserFact]
    public async Task SeededChatPage_MountsInChrome_AndSnapshotCarriesSeededContent()
    {
        fixture.EnsureUsable();
        var page = fixture.Page!;
        var harness = fixture.Harness!;

        await harness.SeedStateAsync(SelectionStorageKey, fixture.ConversationId.ToString(), CancellationToken.None);
        await page.NavigateAsync($"{fixture.BaseUrl}/agent-chat?project={fixture.ProjectId}", CancellationToken.None);

        var mount = await WaitUntilMountedAsync(page);
        mount.GetProperty("main").GetBoolean().ShouldBeTrue();
        mount.GetProperty("composer").GetBoolean().ShouldBeTrue();
        mount.GetProperty("heading").GetBoolean().ShouldBeTrue();

        var dom = await harness.SnapshotDomAsync(CancellationToken.None);
        dom.ShouldNotBeNullOrEmpty();
        dom.ShouldContain("Agent chat");
        dom.ShouldContain(ConversationTitle);
        dom.ShouldContain("aria-label=\"Message the agent\"");
        dom.ShouldContain("aria-current=\"true\"");

        var draft = "harness composer probe";
        var reflected = await harness.TypeIntoComposerAsync(draft, CancellationToken.None);
        reflected.ShouldBe(draft);
    }

    private static async Task<JsonElement> WaitUntilMountedAsync(CdpPage page)
    {
        var script =
            "(() => ({"
            + " main: Boolean(document.querySelector('main')), "
            + " composer: Boolean(document.querySelector('" + CdpChatHarness.ComposerSelector + "')), "
            + " heading: document.body.innerText.includes('Agent chat') }))()";
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            var state = await page.EvaluateAsync(script, CancellationToken.None);
            if (state.GetProperty("main").GetBoolean()
                && state.GetProperty("composer").GetBoolean()
                && state.GetProperty("heading").GetBoolean())
            {
                return state;
            }

            await Task.Delay(300);
        }

        throw new TimeoutException("agent chat page did not mount within 45s");
    }
}
