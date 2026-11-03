using System.Security.Cryptography;
using System.Text;
using DgiiHelper.Api.Data;
using DgiiHelper.Api.Identifiers;
using DgiiHelper.Api.Issuing;
using Microsoft.EntityFrameworkCore;

namespace DgiiHelper.Api;

public record CreateRangeRequest(string? Kind, string? Type, long First, long Last, DateOnly ExpiresOn);

public static class Endpoints
{
    public const string ApiKeyHeader = "X-Api-Key";

    public static void MapDgii(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireRateLimiting("api");

        // Validar solo mira la forma; no guarda nada ni toca la base.
        api.MapGet("/rnc/{value}", (string value) => Respond(Rnc.Validate(value)));
        api.MapGet("/cedula/{value}", (string value) => Respond(Cedula.Validate(value)));
        api.MapGet("/ncf/{value}", (string value) =>
        {
            var r = Ncf.Validate(value);
            return r.Valid
                ? Results.Ok(new
                {
                    valid = true,
                    kind = r.Info!.Value.Kind.ToString().ToLowerInvariant(),
                    type = r.Info.Value.TypeCode,
                    typeName = r.Info.Value.TypeName,
                    sequence = r.Info.Value.Sequence,
                })
                : Results.Ok(new { valid = false, problem = r.Problem });
        });

        // Todo lo que cambia o entrega números exige la clave.
        var admin = api.MapGroup("/ncf/ranges").AddEndpointFilter(RequireApiKey);

        admin.MapPost("/", async (CreateRangeRequest body, NcfIssuer issuer, CancellationToken ct) =>
        {
            if (!TryParseKind(body.Kind, out var kind)) return Results.BadRequest(new { error = "kind" });
            var result = await issuer.CreateRangeAsync(kind, body.Type ?? "", body.First, body.Last, body.ExpiresOn, ct);
            return result switch
            {
                { Range: { } range } => Results.Created($"/api/ncf/ranges/{range.Id}", Describe(range)),
                { Error: "overlap" } => Results.Conflict(new { error = "overlap" }),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        });

        admin.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await db.NcfRanges.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct) is { } range
                ? Results.Ok(Describe(range))
                : Results.NotFound());

        admin.MapPost("/{id:int}/issue", async (int id, NcfIssuer issuer, CancellationToken ct) =>
        {
            var issued = await issuer.IssueAsync(id, ct);
            return issued.Status switch
            {
                IssueStatus.Issued => Results.Created((string?)null, new { ncf = issued.Ncf, sequence = issued.Sequence }),
                IssueStatus.NotFound => Results.NotFound(),
                IssueStatus.Expired => Results.Json(new { error = "expired" }, statusCode: StatusCodes.Status410Gone),
                _ => Results.Conflict(new { error = "exhausted" }),
            };
        });
    }

    private static IResult Respond(IdResult r) => Results.Ok(new
    {
        valid = r.Valid,
        formatted = r.Valid ? r.Formatted : null,
        problem = r.Valid ? null : r.Problem.ToString().ToLowerInvariant(),
    });

    private static object Describe(NcfRange r) => new
    {
        id = r.Id,
        kind = r.Kind.ToString().ToLowerInvariant(),
        type = r.TypeCode,
        first = r.First,
        last = r.Last,
        next = r.Next,
        remaining = r.Last - r.Next + 1,
        expiresOn = r.ExpiresOn,
    };

    private static bool TryParseKind(string? text, out NcfKind kind)
    {
        kind = default;
        // Enum.TryParse también acepta "1" o "0" como números; aquí solo valen los nombres.
        var name = Enum.GetNames<NcfKind>().FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        return name is not null && Enum.TryParse(name, out kind);
    }

    private static async ValueTask<object?> RequireApiKey(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var expected = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Auth:ApiKey"];
        var sent = ctx.HttpContext.Request.Headers[ApiKeyHeader].ToString();

        // Comparación de tiempo constante: no se filtra cuántos caracteres de la clave son correctos.
        var ok = !string.IsNullOrEmpty(expected)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(expected));
        return ok ? await next(ctx) : Results.Unauthorized();
    }
}
