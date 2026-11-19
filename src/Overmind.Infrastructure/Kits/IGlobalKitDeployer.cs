using Overmind.Core;

namespace Overmind.Infrastructure.Kits;

// The seam feature 251 owns: the mandatory kit's content belongs in detected agents' home skill
// directories, never in a project. 250 ships the call site and refuses per-project activation of
// the mandatory kit; the door that actually writes homes is 251's.
public interface IGlobalKitDeployer
{
    Task DeployAsync(SkillKit kit, CancellationToken ct);
}
