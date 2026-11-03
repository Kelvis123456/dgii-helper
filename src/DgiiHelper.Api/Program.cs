using DgiiHelper.Api.Data;
using DgiiHelper.Api.Issuing;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connection = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Default.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<NcfIssuer>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

// Un servicio de una sola instancia: migrar al arrancar es lo más simple. Con varias réplicas
// habría que sacarlo a un paso del despliegue.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
