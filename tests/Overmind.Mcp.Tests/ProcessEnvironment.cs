using Xunit;

namespace Overmind.Mcp.Tests;

// Serializes every test class that touches process-wide mutable state: environment variables
// (OVERMIND_ALIAS / OVERMIND_VENDOR / OVERMIND_PROJECT_ID / wake-period knobs) and the current
// directory that ProjectResolver and the git discovery helpers read. A separate cwd collection
// would let an env write race a cwd-dependent expectation, so both families share this one.
[CollectionDefinition(nameof(ProcessEnvironment))]
public sealed class ProcessEnvironment
{
}
