using System.Net.Http;
using System.Net.Sockets;

internal static class IdentityClusterHttpClientConfiguration
{
    public static Uri GetDestinationAddress(Uri internalAddress, string serverName)
    {
        if (!string.Equals(internalAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return internalAddress;
        }

        if (Uri.CheckHostName(serverName) != UriHostNameType.Dns)
        {
            throw new InvalidOperationException("IDENTITY_TLS_SERVER_NAME must be a DNS hostname for the Identity TLS certificate.");
        }

        var destination = new UriBuilder(internalAddress) { Host = serverName };
        return destination.Uri;
    }

    public static void Configure(
        string clusterId,
        SocketsHttpHandler handler,
        Uri internalAddress,
        string serverName)
    {
        if (!string.Equals(clusterId, "identity", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(internalAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = GetDestinationAddress(internalAddress, serverName);
        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            if (context.DnsEndPoint.Port != internalAddress.Port)
            {
                throw new InvalidOperationException("Identity proxy port does not match its internal HTTPS endpoint.");
            }

            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(internalAddress.DnsSafeHost, internalAddress.Port, cancellationToken);
                return client.GetStream();
            }
            catch
            {
                client.Dispose();
                throw;
            }
        };
    }
}
