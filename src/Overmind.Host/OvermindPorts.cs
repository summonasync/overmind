using System.Net;
using System.Net.Sockets;

namespace Overmind.Host;

public static class OvermindPorts
{
    public const int DefaultPort = 4761;

    public static int ResolvePreferredPort(int preferredPort) =>
        IsPortAvailable(preferredPort) ? preferredPort : 0;

    public static bool IsPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }
}
