using System.Threading.RateLimiting;
using DgiiHelper.Api;
using DgiiHelper.Api.Data;
using DgiiHelper.Api.Issuing;
using Microsoft.AspNetCore.HttpOverrides;
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

// Detrás de un proxy (Caddy, nginx, el balanceador de la nube) todas las peticiones llegan con la IP
// del proxy y el límite por IP sería uno solo para todos. Solo se activa a propósito: si el contenedor
// fuera alcanzable sin pasar por el proxy, cualquiera podría falsear X-Forwarded-For y evadir el límite.
var behindProxy = builder.Configuration.GetValue("Proxy:Trusted", false);
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

var app = builder.Build();
if (behindProxy) app.UseForwardedHeaders();

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
