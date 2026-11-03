using System.Threading.RateLimiting;
using DgiiHelper.Api;
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

// Sin clave los endpoints que entregan números quedarían cerrados para todos, y eso en producción
// casi seguro es un olvido: mejor no arrancar.
if (string.IsNullOrEmpty(builder.Configuration["Auth:ApiKey"]) && !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("Falta Auth:ApiKey.");

// Por IP. Los validadores no cuestan nada, pero la API es pública y los de emisión tocan la base.
var perMinute = builder.Configuration.GetValue("RateLimits:PerMinute", 120);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("api", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Un servicio de una sola instancia: migrar al arrancar es lo más simple. Con varias réplicas
// habría que sacarlo a un paso del despliegue.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapDgii();

app.Run();

public partial class Program;
