using System.Data;
using DgiiHelper.Api.Data;
using DgiiHelper.Api.Identifiers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DgiiHelper.Api.Issuing;

public enum IssueStatus
{
    Issued,
    NotFound,
    Expired,
    Exhausted,
}

public readonly record struct IssueResult(IssueStatus Status, string? Ncf = null, long? Sequence = null);

public readonly record struct CreateRangeResult(NcfRange? Range, string? Error);

// Columnas de UPDATE ... OUTPUT. Las propiedades se llaman igual que las columnas del OUTPUT.
public class IssuedRow
{
    public long Sequence { get; set; }
    public int Kind { get; set; }
    public string TypeCode { get; set; } = "";
}

public class NcfIssuer(AppDbContext db, TimeProvider clock)
{
    private const int DeadlockVictim = 1205;
    private const int MaxAttempts = 8;

    // La DGII trabaja con fecha dominicana (UTC-4 todo el año, sin horario de verano).
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(-4)).DateTime);

    /// <summary>
    /// Entrega el próximo número del rango. Es una sola sentencia UPDATE ... OUTPUT: SQL Server bloquea
    /// la fila, suma uno y devuelve el valor anterior en el mismo paso, así que dos llamadas simultáneas
    /// nunca reciben el mismo número ni se salen del rango, sin transacción explícita ni reintentos.
    /// </summary>
    public async Task<IssueResult> IssueAsync(int rangeId, CancellationToken ct = default)
    {
        var today = Today;
        var rows = await db.Database.SqlQuery<IssuedRow>($"""
            UPDATE NcfRanges SET [Next] = [Next] + 1
            OUTPUT inserted.[Next] - 1 AS Sequence, inserted.Kind AS Kind, inserted.TypeCode AS TypeCode
            WHERE Id = {rangeId} AND [Next] <= [Last] AND ExpiresOn >= {today}
            """).ToListAsync(ct);

        if (rows.Count == 1)
        {
            var row = rows[0];
            return new(IssueStatus.Issued, Ncf.Format((NcfKind)row.Kind, row.TypeCode, row.Sequence), row.Sequence);
        }

        // El UPDATE no tocó nada: se mira por qué solo para dar el motivo correcto.
        var range = await db.NcfRanges.AsNoTracking().SingleOrDefaultAsync(r => r.Id == rangeId, ct);
        if (range is null) return new(IssueStatus.NotFound);
        return new(range.ExpiresOn < today ? IssueStatus.Expired : IssueStatus.Exhausted);
    }

    /// <summary>
    /// Registra un rango autorizado. Dos rangos del mismo tipo no pueden solaparse: la DGII no
    /// entrega el mismo número dos veces. La comprobación y el insert van en una transacción
    /// serializable para que dos altas simultáneas no se cuelen entre sí.
    /// </summary>
    public async Task<CreateRangeResult> CreateRangeAsync(
        NcfKind kind, string typeCode, long first, long last, DateOnly expiresOn, CancellationToken ct = default)
    {
        if (!Ncf.Types(kind).ContainsKey(typeCode)) return new(null, "type");
        if (first < 1 || last < first || last > Ncf.MaxSequence(kind)) return new(null, "bounds");
        if (expiresOn < Today) return new(null, "expired");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var overlaps = await db.NcfRanges.AnyAsync(
                    r => r.Kind == kind && r.TypeCode == typeCode && r.First <= last && r.Last >= first, ct);
                if (overlaps) return new(null, "overlap");

                var range = new NcfRange
                {
                    Kind = kind, TypeCode = typeCode, First = first, Last = last, Next = first, ExpiresOn = expiresOn,
                };
                db.NcfRanges.Add(range);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(range, null);
            }
            catch (Exception e) when (IsDeadlock(e) && attempt < MaxAttempts)
            {
                // Bajo Serializable dos altas simultáneas pueden bloquearse entre sí; SQL Server aborta
                // una y la otra pasa. Se reintenta la abortada, que ahora sí verá el rango de la primera.
                db.ChangeTracker.Clear();
                await Task.Delay(Random.Shared.Next(5, 25) * attempt, ct);
            }
        }
    }

    // EF envuelve la SqlException según de dónde venga (SaveChanges, consulta, reintento), así que se
    // revisa toda la cadena de excepciones internas.
    private static bool IsDeadlock(Exception? e)
    {
        for (; e is not null; e = e.InnerException)
            if (e is SqlException { Number: DeadlockVictim }) return true;
        return false;
    }
}
