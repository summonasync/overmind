using Microsoft.Extensions.Configuration;

namespace Overmind.Host.Api;

public static class WaveOwnerDefaults
{
    public const string DefaultAlias = "owner";

    public static string ResolveAlias(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetValue<string?>("Wave:OwnerAlias") is { } alias && alias.Trim().Length > 0
            ? alias.Trim()
            : DefaultAlias;
    }
}
