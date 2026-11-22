namespace Overmind.Host.Api;

// Routing unescapes every bound route value except %2F (measured on the deployed v1.9.1 Host,
// 2026-11-16: `rooms/zz.Test%2FSweep/messages` reached the handler as `zz.Test%2FSweep` while
// %25 and friends arrived decoded). Stored lane and room names keep the real slash, so only that
// escape is restored — %2F is a spelling of "/", never an escape to decode again. A full
// UnescapeDataString would double-decode names that merely spell like escapes: a wire request
// addressed to the literal room `A%20B` (escaped `A%2520B`) landed in `A B` — silent cross-room
// delivery (measured red on this suite). Invalid escapes pass through unchanged on .NET 10
// (`release 50%` stays `release 50%`), so nothing throws here — the defect was never a crash,
// it was a second decode. Because both spellings collapse in the bound value, a stored name
// containing the literal sequence `%2F` is shadowed by its slash twin — the transport cannot
// distinguish them.
internal static class RouteNameDecoder
{
    internal static string Decode(string name) => name.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
}
