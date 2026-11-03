using DgiiHelper.Api.Identifiers;

namespace DgiiHelper.Api.Data;

/// <summary>
/// Un rango de secuencias autorizado por la DGII para un tipo de comprobante: del First al Last,
/// con fecha de vencimiento. Next es el próximo número que se entrega.
/// </summary>
public class NcfRange
{
    public int Id { get; set; }
    public NcfKind Kind { get; set; }
    public string TypeCode { get; set; } = "";
    public long First { get; set; }
    public long Last { get; set; }
    public long Next { get; set; }
    public DateOnly ExpiresOn { get; set; }
}
