using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DgiiHelper.Tests;

[Collection("api")]
public class EndpointTests(ApiFactory api) : IAsyncLifetime
{
    private static readonly string Future = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");

    public Task InitializeAsync() => api.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>());

    private static object Range(string kind = "traditional", string type = "01", long first = 1, long last = 3, string? expires = null) =>
        new { kind, type, first, last, expiresOn = expires ?? Future };

    [Fact]
    public async Task Health_responde_sin_clave()
    {
        var r = await api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Valida_un_rnc_y_lo_devuelve_con_guiones()
    {
        var body = await Json(await api.CreateClient().GetAsync("/api/rnc/101010101"));
        Assert.True(body.GetProperty("valid").GetBoolean());
        Assert.Equal("1-01-01010-1", body.GetProperty("formatted").GetString());
    }

    [Fact]
    public async Task Un_rnc_malo_dice_por_que()
    {
        var body = await Json(await api.CreateClient().GetAsync("/api/rnc/101010100"));
        Assert.False(body.GetProperty("valid").GetBoolean());
        Assert.Equal("checkdigit", body.GetProperty("problem").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("formatted").ValueKind);
    }

    [Fact]
    public async Task Valida_cedulas_y_ncf_por_http()
    {
        var http = api.CreateClient();
        Assert.True((await Json(await http.GetAsync("/api/cedula/001-1234567-3"))).GetProperty("valid").GetBoolean());

        var ncf = await Json(await http.GetAsync("/api/ncf/E310000000150"));
        Assert.Equal("electronic", ncf.GetProperty("kind").GetString());
        Assert.Equal("31", ncf.GetProperty("type").GetString());
        Assert.Equal(150, ncf.GetProperty("sequence").GetInt64());

        var bad = await Json(await http.GetAsync("/api/ncf/B0500000001"));
        Assert.False(bad.GetProperty("valid").GetBoolean());
        Assert.Equal("type", bad.GetProperty("problem").GetString());
    }

    [Theory]
    [InlineData("POST", "/api/ncf/ranges")]
    [InlineData("GET", "/api/ncf/ranges/1")]
    [InlineData("POST", "/api/ncf/ranges/1/issue")]
    public async Task Sin_clave_o_con_clave_equivocada_no_se_puede_emitir(string method, string path)
    {
        var http = api.CreateClient();
        var none = await http.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(Range()) });
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);

        var wrong = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(Range()) };
        wrong.Headers.Add("X-Api-Key", ApiFactory.ApiKey + "x");
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(wrong)).StatusCode);
    }

    [Fact]
    public async Task Crea_un_rango_y_emite_hasta_agotarlo()
    {
        var http = api.AdminClient();
        var created = await http.PostAsJsonAsync("/api/ncf/ranges", Range(last: 2));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Json(created)).GetProperty("id").GetInt32();

        var first = await http.PostAsync($"/api/ncf/ranges/{id}/issue", null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("B0100000001", (await Json(first)).GetProperty("ncf").GetString());

        var info = await Json(await http.GetAsync($"/api/ncf/ranges/{id}"));
        Assert.Equal(1, info.GetProperty("remaining").GetInt64());

        Assert.Equal(HttpStatusCode.Created, (await http.PostAsync($"/api/ncf/ranges/{id}/issue", null)).StatusCode);
        var spent = await http.PostAsync($"/api/ncf/ranges/{id}/issue", null);
        Assert.Equal(HttpStatusCode.Conflict, spent.StatusCode);
        Assert.Equal("exhausted", (await Json(spent)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Emitir_en_un_rango_que_no_existe_da_404()
    {
        var r = await api.AdminClient().PostAsync("/api/ncf/ranges/987654/issue", null);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.AdminClient().GetAsync("/api/ncf/ranges/987654")).StatusCode);
    }

    [Fact]
    public async Task Un_rango_vencido_da_410()
    {
        var http = api.AdminClient();
        var id = (await Json(await http.PostAsJsonAsync("/api/ncf/ranges", Range(expires: Future)))).GetProperty("id").GetInt32();
        await using (var scope = api.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DgiiHelper.Api.Data.AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("UPDATE NcfRanges SET ExpiresOn = DATEADD(day, -3, GETDATE())");
        }

        var r = await http.PostAsync($"/api/ncf/ranges/{id}/issue", null);
        Assert.Equal(HttpStatusCode.Gone, r.StatusCode);
    }

    [Theory]
    [InlineData("papel", "01", 1, 5, "kind")]
    [InlineData(null, "01", 1, 5, "kind")]
    [InlineData("traditional", "99", 1, 5, "type")]
    [InlineData("traditional", "01", 9, 5, "bounds")]
    [InlineData("electronic", "01", 1, 5, "type")] // 01 es de la serie B
    public async Task Rechaza_un_rango_mal_formado_con_400(string? kind, string type, long first, long last, string error)
    {
        var r = await api.AdminClient().PostAsJsonAsync("/api/ncf/ranges", new { kind, type, first, last, expiresOn = Future });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(error, (await Json(r)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Un_rango_solapado_da_409()
    {
        var http = api.AdminClient();
        await http.PostAsJsonAsync("/api/ncf/ranges", Range(first: 1, last: 10));
        var r = await http.PostAsJsonAsync("/api/ncf/ranges", Range(first: 10, last: 20));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("overlap", (await Json(r)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Pasado_el_limite_por_minuto_responde_429()
    {
        using var limited = api.WithWebHostBuilder(b => b.UseSetting("RateLimits:PerMinute", "3"));
        var http = limited.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++) codes.Add((await http.GetAsync("/api/rnc/101010101")).StatusCode);

        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.OK));
        Assert.Equal(2, codes.Count(c => c == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public void En_produccion_no_arranca_sin_clave()
    {
        using var prod = api.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Auth:ApiKey", "");
        });
        var e = Assert.Throws<InvalidOperationException>(() => prod.CreateClient());
        Assert.Contains("Auth:ApiKey", e.Message);
    }
}
