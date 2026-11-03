using System.Net;
using System.Net.Sockets;

namespace DgiiHelper.Api;

public static class RateLimitKey
{
    /// <summary>
    /// Clave del límite por IP. En IPv6 un cliente suele tener un /64 entero, así que contar por dirección
    /// completa le daría un cupo nuevo en cada dirección que rote; se cuenta por el prefijo de 64 bits.
    /// </summary>
    public static string For(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();
        return Convert.ToHexString(address.GetAddressBytes().AsSpan(0, 8));
    }
}
