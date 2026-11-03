using System.Net;
using DgiiHelper.Api;

namespace DgiiHelper.Tests;

public class RateLimitKeyTests
{
    [Fact]
    public void Dos_direcciones_del_mismo_64_comparten_cupo()
    {
        var a = RateLimitKey.For(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd"));
        var b = RateLimitKey.For(IPAddress.Parse("2001:db8:1:2:1111:2222:3333:4444"));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Dos_64_distintos_no_comparten_cupo()
    {
        Assert.NotEqual(
            RateLimitKey.For(IPAddress.Parse("2001:db8:1:2::1")),
            RateLimitKey.For(IPAddress.Parse("2001:db8:1:3::1")));
    }

    [Fact]
    public void Una_IPv4_y_su_forma_mapeada_a_IPv6_son_la_misma_clave()
    {
        Assert.Equal("203.0.113.7", RateLimitKey.For(IPAddress.Parse("203.0.113.7")));
        Assert.Equal("203.0.113.7", RateLimitKey.For(IPAddress.Parse("::ffff:203.0.113.7")));
    }

    [Fact]
    public void Sin_direccion_hay_una_clave_fija()
    {
        Assert.Equal("unknown", RateLimitKey.For(null));
    }
}
