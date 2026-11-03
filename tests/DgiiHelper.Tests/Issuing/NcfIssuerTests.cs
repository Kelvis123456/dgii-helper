using DgiiHelper.Api.Data;
using DgiiHelper.Api.Identifiers;
using DgiiHelper.Api.Issuing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DgiiHelper.Tests.Issuing;

[Collection("api")]
public class NcfIssuerTests(ApiFactory api) : IAsyncLifetime
{
    private static readonly DateOnly Future = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);

    public Task InitializeAsync() => api.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<NcfRange> NewRange(
        long first, long last, string type = "01", NcfKind kind = NcfKind.Traditional, DateOnly? expires = null)
    {
        await using var scope = api.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<NcfIssuer>()
            .CreateRangeAsync(kind, type, first, last, expires ?? Future);
        Assert.Null(result.Error);
        return result.Range!;
    }

    private async Task<IssueResult> Issue(int rangeId)
    {
        await using var scope = api.NewScope();
        return await scope.ServiceProvider.GetRequiredService<NcfIssuer>().IssueAsync(rangeId);
    }

    [Fact]
    public async Task Entrega_los_numeros_en_orden_con_el_formato_del_tipo()
    {
        var range = await NewRange(1, 3);
        Assert.Equal("B0100000001", (await Issue(range.Id)).Ncf);
        Assert.Equal("B0100000002", (await Issue(range.Id)).Ncf);
        Assert.Equal("B0100000003", (await Issue(range.Id)).Ncf);
    }

    [Fact]
    public async Task Un_rango_electronico_entrega_ecf_de_13_caracteres()
    {
        var range = await NewRange(150, 160, "31", NcfKind.Electronic);
        var issued = await Issue(range.Id);
        Assert.Equal("E310000000150", issued.Ncf);
        Assert.Equal(150, issued.Sequence);
    }

    [Fact]
    public async Task Empieza_en_el_primer_numero_autorizado_aunque_no_sea_el_uno()
    {
        var range = await NewRange(5000, 5001);
        Assert.Equal("B0100005000", (await Issue(range.Id)).Ncf);
    }

    [Fact]
    public async Task Al_agotar_el_rango_deja_de_entregar_y_no_se_pasa_del_ultimo()
    {
        var range = await NewRange(1, 2);
        await Issue(range.Id);
        await Issue(range.Id);

        for (var i = 0; i < 3; i++) Assert.Equal(IssueStatus.Exhausted, (await Issue(range.Id)).Status);

        await using var scope = api.NewScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().NcfRanges.SingleAsync();
        Assert.Equal(3, stored.Next); // Last + 1: el UPDATE ya no tocó la fila
    }

    [Fact]
    public async Task Un_rango_de_un_solo_numero_se_entrega_una_vez()
    {
        var range = await NewRange(7, 7);
        Assert.Equal(IssueStatus.Issued, (await Issue(range.Id)).Status);
        Assert.Equal(IssueStatus.Exhausted, (await Issue(range.Id)).Status);
    }

    [Fact]
    public async Task Un_rango_vencido_no_entrega_nada()
    {
        var range = await NewRange(1, 10);
        await using (var scope = api.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.NcfRanges.ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresOn, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)));
        }

        Assert.Equal(IssueStatus.Expired, (await Issue(range.Id)).Status);
    }

    [Fact]
    public async Task El_dia_del_vencimiento_todavia_se_puede_emitir()
    {
        var dominicanToday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));
        var range = await NewRange(1, 10, expires: dominicanToday);
        Assert.Equal(IssueStatus.Issued, (await Issue(range.Id)).Status);
    }

    [Fact]
    public async Task Un_rango_que_no_existe_da_not_found()
    {
        Assert.Equal(IssueStatus.NotFound, (await Issue(123456)).Status);
    }

    [Fact]
    public async Task Cada_rango_lleva_su_propia_cuenta()
    {
        var consumo = await NewRange(1, 5, "02");
        var credito = await NewRange(1, 5, "01");
        Assert.Equal("B0200000001", (await Issue(consumo.Id)).Ncf);
        Assert.Equal("B0200000002", (await Issue(consumo.Id)).Ncf);
        Assert.Equal("B0100000001", (await Issue(credito.Id)).Ncf);
    }

    [Fact]
    public async Task Cuarenta_emisiones_simultaneas_reciben_cuarenta_numeros_distintos_y_seguidos()
    {
        var range = await NewRange(1, 40);

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() => Issue(range.Id))));

        Assert.All(results, r => Assert.Equal(IssueStatus.Issued, r.Status));
        Assert.Equal(Enumerable.Range(1, 40).Select(n => (long)n), results.Select(r => r.Sequence!.Value).Order());
    }

    [Fact]
    public async Task Con_mas_peticiones_que_numeros_sobran_las_ultimas_y_nadie_repite()
    {
        var range = await NewRange(1, 25);

        var results = await Task.WhenAll(Enumerable.Range(0, 60).Select(_ => Task.Run(() => Issue(range.Id))));

        var issued = results.Where(r => r.Status == IssueStatus.Issued).Select(r => r.Sequence!.Value).ToList();
        Assert.Equal(25, issued.Count);
        Assert.Equal(25, issued.Distinct().Count());
        Assert.Equal(35, results.Count(r => r.Status == IssueStatus.Exhausted));
        Assert.Equal(Enumerable.Range(1, 25).Select(n => (long)n), issued.Order());
    }

    [Theory]
    [InlineData("99", 1, 5, "type")]
    [InlineData("01", 0, 5, "bounds")]
    [InlineData("01", 5, 4, "bounds")]
    [InlineData("01", 1, 100_000_000, "bounds")] // el NCF tradicional solo tiene 8 dígitos de secuencia
    public async Task Rechaza_rangos_mal_formados(string type, long first, long last, string error)
    {
        await using var scope = api.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<NcfIssuer>()
            .CreateRangeAsync(NcfKind.Traditional, type, first, last, Future);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Range);
    }

    [Fact]
    public async Task Rechaza_un_rango_que_ya_vencio()
    {
        await using var scope = api.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<NcfIssuer>()
            .CreateRangeAsync(NcfKind.Traditional, "01", 1, 10, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        Assert.Equal("expired", result.Error);
    }

    [Theory]
    [InlineData(1, 10)] // idéntico
    [InlineData(5, 15)] // pisa el final
    [InlineData(10, 12)] // solo comparte el último número
    [InlineData(8, 8)] // dentro
    [InlineData(1, 99)] // lo envuelve
    public async Task No_deja_solapar_rangos_del_mismo_tipo(long first, long last)
    {
        await NewRange(1, 10);

        await using var scope = api.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<NcfIssuer>()
            .CreateRangeAsync(NcfKind.Traditional, "01", first, last, Future);

        Assert.Equal("overlap", result.Error);
    }

    [Fact]
    public async Task Un_rango_contiguo_o_de_otro_tipo_no_cuenta_como_solape()
    {
        await NewRange(1, 10);
        await NewRange(11, 20); // contiguo
        await NewRange(1, 10, "02"); // otro tipo, mismos números
        await NewRange(1, 10, "31", NcfKind.Electronic); // otra serie

        await using var scope = api.NewScope();
        Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<AppDbContext>().NcfRanges.CountAsync());
    }

    [Fact]
    public async Task Veinte_altas_simultaneas_del_mismo_rango_dejan_solo_una()
    {
        await using var warm = api.NewScope(); // calienta el contexto para que la carrera sea en la base
        await warm.ServiceProvider.GetRequiredService<AppDbContext>().NcfRanges.CountAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var scope = api.NewScope();
            return await scope.ServiceProvider.GetRequiredService<NcfIssuer>()
                .CreateRangeAsync(NcfKind.Traditional, "01", 1, 100, Future);
        })));

        Assert.Equal(1, results.Count(r => r.Error is null));
        Assert.Equal(19, results.Count(r => r.Error == "overlap"));
        await using var scope = api.NewScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().NcfRanges.CountAsync());
    }
}
