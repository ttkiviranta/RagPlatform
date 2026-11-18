using System.Net;
using System.Net.Sockets;

namespace RagPlatform.Api.Services;

internal static class WebContentDestinationPolicy
{
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedIpv4Networks =
    [
        (IPAddress.Parse("0.0.0.0"), 8),
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("100.64.0.0"), 10),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.0.0.0"), 24),
        (IPAddress.Parse("192.0.2.0"), 24),
        (IPAddress.Parse("192.88.99.0"), 24),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("198.18.0.0"), 15),
        (IPAddress.Parse("198.51.100.0"), 24),
        (IPAddress.Parse("203.0.113.0"), 24),
        (IPAddress.Parse("224.0.0.0"), 4),
        (IPAddress.Parse("240.0.0.0"), 4)
    ];

    internal static void ValidateUri(Uri uri, string parameterName)
    {
        if (!uri.IsAbsoluteUri
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A valid HTTP or HTTPS URL is required.", parameterName);
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("URLs containing user information are not allowed.", parameterName);
        }

        var allowedPort = uri.Scheme == Uri.UriSchemeHttp ? 80 : 443;
        if (uri.Port != allowedPort)
        {
            throw new ArgumentException("Only HTTP port 80 and HTTPS port 443 are allowed.", parameterName);
        }

        if (IPAddress.TryParse(uri.DnsSafeHost, out var address) && !IsPublicAddress(address))
        {
            throw new ArgumentException("The URL destination must be a public IP address.", parameterName);
        }
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !BlockedIpv4Networks.Any(network => IsInNetwork(address, network.Network, network.PrefixLength));
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        var isGlobalUnicast = (bytes[0] & 0xE0) == 0x20;
        var isProtocolAssignment = IsInNetwork(address, IPAddress.Parse("2001::"), 23);
        var isDocumentation = IsInNetwork(address, IPAddress.Parse("2001:db8::"), 32);
        var isSixToFour = IsInNetwork(address, IPAddress.Parse("2002::"), 16);

        return isGlobalUnicast && !isProtocolAssignment && !isDocumentation && !isSixToFour;
    }

    internal static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        if (endpoint.Port is not (80 or 443))
        {
            throw new HttpRequestException("Only HTTP port 80 and HTTPS port 443 are allowed.");
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(endpoint.Host, out var literalAddress))
        {
            addresses = [literalAddress];
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new HttpRequestException("The URL destination must resolve only to public IP addresses.");
        }

        SocketException? lastSocketException = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                lastSocketException = exception;
                socket.Dispose();
            }
        }

        throw new HttpRequestException("Unable to connect to the validated public destination.", lastSocketException);
    }

    private static bool IsInNetwork(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var index = 0; index < fullBytes; index++)
        {
            if (addressBytes[index] != networkBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (addressBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }
}