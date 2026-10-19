namespace Overmind.Infrastructure.Grimoire;

public interface IGrimoireBinaryLocator
{
    string? Locate(string? overridePath);
}
