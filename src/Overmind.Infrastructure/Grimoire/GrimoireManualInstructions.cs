namespace Overmind.Infrastructure.Grimoire;

public static class GrimoireManualInstructions
{
    public static IReadOnlyList<string> Steps { get; } =
    [
        "Install Grimoire using its own distribution channel (dotnet tool or the project's install script).",
        "Run 'grimoire init' so the runtime config exists.",
        "Verify 'grimoire --version' works in a terminal.",
        "Start 'grimoire serve' if you want the connected state, or keep it stopped — wiring works either way.",
        "Come back to this wizard and run Wire to register 'grimoire mcp' for your detected agents."
    ];
}
