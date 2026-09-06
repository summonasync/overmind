using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Overmind.Host;

public static class PortFile
{
    public static void Write(WebApplication app, string portFilePath)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var port = addresses?.Addresses
                       .Select(ParsePort)
                       .FirstOrDefault(p => p > 0) ?? 0;

        if (port == 0)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(portFilePath)!);
        File.WriteAllText(portFilePath, port.ToString());
    }

    private static int ParsePort(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && !uri.IsDefaultPort ? uri.Port : 0;
}
