using Xunit;

namespace Overmind.Api.Tests;

// Serializes the test classes that mutate process-wide environment state — the wake-drop
// directory variable and TMPDIR. Those writes are visible to every collection in the
// assembly, so the classes that make them must not overlap.
[CollectionDefinition(nameof(ProcessEnvironment))]
public sealed class ProcessEnvironment
{
}
