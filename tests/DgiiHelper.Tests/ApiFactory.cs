using DgiiHelper.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace DgiiHelper.Tests;

// SQL Server real: el comportamiento que hay que probar (bloqueos de fila, aislamiento serializable)
// no existe en InMemory ni en SQLite. En CI se levanta un contenedor; en local se puede apuntar a un
// SQL Server ya instalado con DGII_TEST_SQL
// (ej. "Server=localhost;Database=DgiiHelper_Tests;Integrated Security=True;TrustServerCertificate=True").
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string? LocalSql = Environment.GetEnvironmentVariable("DGII_TEST_SQL");

    private readonly MsSqlContainer? _sql = LocalSql is null
        ? new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build()
        : null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", LocalSql ?? _sql!.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        if (_sql is not null) await _sql.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_sql is not null) await _sql.DisposeAsync();
    }

    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();

    // Cada test arranca con la tabla vacía.
    public async Task ResetAsync()
    {
        await using var scope = NewScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.ExecuteSqlRawAsync("DELETE FROM NcfRanges");
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
