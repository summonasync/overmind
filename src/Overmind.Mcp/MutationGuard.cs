using System.Text.Json;
using ModelContextProtocol;

namespace Overmind.Mcp;

public sealed class MutationGuard(OvermindApiClient api)
{
    public async Task<JsonElement> SendAsync(
        Func<Task<JsonElement>> mutation,
        MutationReadback? readback,
        CancellationToken ct) =>
        await SendAsync(mutation, _ => Task.FromResult(readback), ct);

    public async Task<JsonElement> SendAsync(
        Func<Task<JsonElement>> mutation,
        Func<CancellationToken, Task<MutationReadback?>> resolveReadback,
        CancellationToken ct)
    {
        try
        {
            return await mutation();
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(StopDetailOf(ex) ?? ex.Message);
        }
        catch (ApiNotFoundException ex)
        {
            throw new McpException(ex.Message);
        }
        catch (ApiUnreachableException)
        {
            throw new McpException($"{OvermindApiClient.RetryableMarker} {await SettleAsync(() => resolveReadback(ct), ct)}");
        }
    }

    private async Task<string> SettleAsync(Func<Task<MutationReadback?>> resolveReadback, CancellationToken ct)
    {
        MutationReadback? readback;
        try
        {
            readback = await resolveReadback();
        }
        catch (Exception resolveFailure) when (!ct.IsCancellationRequested)
        {
            return UnknownOutcome(
                $"the verify read-back address could not be resolved ({ProbeFailureReason(resolveFailure)})");
        }

        if (readback is null)
        {
            return UnknownOutcome(
                "the adapter has no verify probe for this mutation — no addressable artifact identity " +
                "or logNote marker to verify against");
        }

        try
        {
            var payload = await api.GetAsync(readback.Path, ct);
            var logContent = LogSectionReader.SectionContentText(ReadBody(payload));
            if (logContent is null)
            {
                // A body whose log lives in an inline wrapper renders as no section through the
                // anchored-pair read path; declaring "no trace" there would clear a resend that
                // may have committed — unknown keeps the agent on the safe side.
                return UnknownOutcome($"the read-back of {readback.Target} carries no readable log section");
            }

            if (!logContent.TrimEnd().EndsWith(readback.Marker.Trim(), StringComparison.Ordinal))
            {
                return $"the API connection died mid-mutation, but no trace — resend safe. " +
                    $"Read-back of {readback.Target}: the log does not end with this mutation's logNote.";
            }

            return $"the API connection died mid-mutation, but the write committed at v{ReadVersion(payload)} — do not resend. " +
                $"Read-back of {readback.Target}: the log ends with this mutation's logNote.";
        }
        catch (Exception probeFailure) when (!ct.IsCancellationRequested)
        {
            return UnknownOutcome(
                $"the verify read-back of {readback.Target} failed ({ProbeFailureReason(probeFailure)})");
        }
    }

    private static string UnknownOutcome(string reason) =>
        $"the API connection died mid-mutation and the outcome is unknown — read before resending ({reason}).";

    private static string ReadBody(JsonElement payload) =>
        payload.GetProperty("body").GetString() ?? string.Empty;

    private static int ReadVersion(JsonElement payload) =>
        payload.GetProperty("version").GetInt32();

    private static string ProbeFailureReason(Exception failure) => failure switch
    {
        // The probe rides the same budget as the mutation: a timeout here means the Host is
        // slow-and-alive committing the write, not down — labeling it unreachable would repeat
        // the exact misclassification the timeout text exists to remove.
        OperationCanceledException => "the read-back request timed out — the Host is slow and may still commit",
        ApiUnreachableException => "host unreachable",
        ApiNotFoundException => "the read-back address no longer exists",
        ApiStatusException ex => $"host returned status {(int)ex.StatusCode}",
        _ => "read-back payload was unusable"
    };

    // OvermindApiClient packs the ProblemDetails detail behind "): "; the gate's STOP text must lead the agent-visible error.
    internal static string? StopDetailOf(ApiStatusException ex)
    {
        var detail = DetailAfterOperationMark(ex.Message);
        return detail is not null && detail.StartsWith("STOP:", StringComparison.Ordinal) ? detail : null;
    }

    internal static string? DetailAfterOperationMark(string message)
    {
        var boundary = message.IndexOf("): ", StringComparison.Ordinal);
        return boundary < 0 ? null : message[(boundary + 3)..];
    }
}
