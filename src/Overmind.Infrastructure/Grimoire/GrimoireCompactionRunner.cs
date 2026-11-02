using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Llm;

namespace Overmind.Infrastructure.Grimoire;

public sealed class GrimoireCompactionRunner(
    IGrimoireMemoryClient memory,
    ILlmChatClientFactory chatFactory,
    ILogger<GrimoireCompactionRunner> logger)
{
    public const int MaxGroupsPerPass = 5;

    public async Task<GrimoireCompactionPassResult> RunAsync(CancellationToken ct)
    {
        GrimoireMemoryEndpoint endpoint;
        try
        {
            endpoint = await memory.ResolveEndpointAsync(ct);
        }
        catch (GrimoireMemoryException ex)
        {
            return GrimoireCompactionPassResult.Failed(GrimoireCompactionOutcome.GrimoireUnavailable, ex.Message);
        }

        var client = await chatFactory.CreateAsync(ct);
        if (client is null)
        {
            return GrimoireCompactionPassResult.Failed(
                GrimoireCompactionOutcome.ProviderMissing,
                "no LLM provider is configured (agent.apiKey is empty); distillation skipped");
        }

        IReadOnlyList<GrimoireCompactionGroup> groups;
        try
        {
            groups = await memory.GetCompactionCandidatesAsync(endpoint, ct);
        }
        catch (GrimoireMemoryException ex)
        {
            return GrimoireCompactionPassResult.Failed(GrimoireCompactionOutcome.CandidateSourceFailed, ex.Message);
        }

        var facts = 0;
        var forgotten = 0;
        var skipped = 0;
        foreach (var group in groups.Take(MaxGroupsPerPass))
        {
            if (group.Entries.Count == 0)
            {
                skipped++;
                continue;
            }

            try
            {
                var factId = await DistillAndStoreAsync(memory, endpoint, client, group, ct);
                foreach (var entry in group.MemoryIds)
                {
                    await memory.ForgetAsync(entry, $"compacted into {factId}", endpoint, ct);
                    forgotten++;
                }

                facts++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                skipped++;
                logger.LogWarning(
                    ex,
                    "Compaction group with {EntryCount} entries was skipped; its entries stay active",
                    group.MemoryIds.Count);
            }
        }

        return new GrimoireCompactionPassResult(
            GrimoireCompactionOutcome.Completed, null, groups.Count, facts, forgotten, skipped);
    }

    private static async Task<Guid> DistillAndStoreAsync(
        IGrimoireMemoryClient memory,
        GrimoireMemoryEndpoint endpoint,
        IChatClient client,
        GrimoireCompactionGroup group,
        CancellationToken ct)
    {
        var factText = (await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, GrimoireCompactionPrompts.Distill(group.Entries))], cancellationToken: ct))
            .Text.Trim();

        if (factText.Length == 0)
        {
            throw new InvalidOperationException("The provider returned an empty fact.");
        }

        var write = await memory.RememberAsync(
            endpoint,
            new GrimoireMemoryWriteRequest(factText, "global", "fact", [], 0.7, null),
            ct);
        return write.Id;
    }
}
