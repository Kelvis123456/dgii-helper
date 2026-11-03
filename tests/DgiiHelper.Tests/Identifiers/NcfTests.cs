using DgiiHelper.Api.Identifiers;

namespace DgiiHelper.Tests.Identifiers;

public class NcfTests
{
    [Fact]
    public void Lee_un_ncf_tradicional()
    {
        var r = Ncf.Validate("B0100000025");
        Assert.True(r.Valid);
        Assert.Equal(new NcfInfo(NcfKind.Traditional, "01", "Factura de crédito fiscal", 25), r.Info);
    }

    [Fact]
    public void Lee_un_ecf()
    {
        var r = Ncf.Validate("E310000000150");
        Assert.True(r.Valid);
        Assert.Equal(new NcfInfo(NcfKind.Electronic, "31", "Factura de crédito fiscal electrónica", 150), r.Info);
    }

    [Theory]
    [InlineData("b0200000001")]
    [InlineData("  B0200000001  ")]
    public void Acepta_minusculas_y_espacios_en_los_bordes(string input) => Assert.True(Ncf.Validate(input).Valid);

    [Theory]
    [InlineData(null, "length")]
    [InlineData("", "length")]
    [InlineData("   ", "length")]
    [InlineData("A0100000001", "series")]
    [InlineData("0100000001", "series")]
    [InlineData("B010000001", "length")] // 10 caracteres
    [InlineData("B01000000012", "length")] // 12 caracteres
    [InlineData("E31000000001", "length")] // un e-CF con la secuencia de un NCF tradicional más uno
    [InlineData("B010000000A", "not_digits")]
    [InlineData("B01-0000001", "not_digits")]
    [InlineData("B0500000001", "type")] // no existe B05
    [InlineData("E350000000001", "type")] // no existe E35
    [InlineData("E010000000001", "type")] // 01 es de la serie B
    [InlineData("B3100000001", "type")] // 31 es de la serie E
    [InlineData("B0100000000", "sequence")]
    [InlineData("E310000000000", "sequence")]
    public void Rechaza_lo_que_no_es_un_comprobante(string? input, string problem)
    {
        var r = Ncf.Validate(input);
        Assert.False(r.Valid);
        Assert.Equal(problem, r.Problem);
    }

    [Theory]
    [InlineData(NcfKind.Traditional, "01", 1L, "B0100000001")]
    [InlineData(NcfKind.Traditional, "02", 99_999_999L, "B0299999999")]
    [InlineData(NcfKind.Electronic, "31", 1L, "E310000000001")]
    [InlineData(NcfKind.Electronic, "47", 9_999_999_999L, "E479999999999")]
    public void Format_rellena_con_ceros_y_vuelve_a_validar(NcfKind kind, string type, long seq, string expected)
    {
        var text = Ncf.Format(kind, type, seq);
        Assert.Equal(expected, text);
        Assert.Equal(seq, Ncf.Validate(text).Info!.Value.Sequence);
    }

    [Fact]
    public void El_maximo_de_cada_serie_cabe_en_sus_digitos()
    {
        Assert.Equal(Ncf.TraditionalSequenceDigits, Ncf.MaxSequence(NcfKind.Traditional).ToString().Length);
        Assert.Equal(Ncf.ElectronicSequenceDigits, Ncf.MaxSequence(NcfKind.Electronic).ToString().Length);
    }
}
