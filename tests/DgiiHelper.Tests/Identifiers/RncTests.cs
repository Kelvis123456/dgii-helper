using DgiiHelper.Api.Identifiers;

namespace DgiiHelper.Tests.Identifiers;

public class RncTests
{
    // Cada dígito verificador sale de la suma ponderada hecha a mano (pesos 7,9,8,6,5,4,3,2):
    //   10101010 → 7+0+8+0+5+0+3+0 = 23, 23 % 11 = 1  → 1
    //   13100001 → 7+27+8+0+0+0+0+2 = 44, 44 % 11 = 0 → 2
    //   12345678 → 7+18+24+24+25+24+21+16 = 159, 159 % 11 = 5 → 11-5 = 6
    //   10000000 → 7, 7 % 11 = 7                       → 11-7 = 4
    [Theory]
    [InlineData("101010101", "1-01-01010-1")]
    [InlineData("131000012", "1-31-00001-2")]
    [InlineData("123456786", "1-23-45678-6")]
    [InlineData("100000004", "1-00-00000-4")]
    public void Acepta_un_rnc_con_su_verificador_correcto(string input, string formatted)
    {
        var r = Rnc.Validate(input);
        Assert.True(r.Valid);
        Assert.Equal(IdProblem.None, r.Problem);
        Assert.Equal(input, r.Digits);
        Assert.Equal(formatted, r.Formatted);
    }

    [Theory]
    [InlineData("1-01-01010-1")]
    [InlineData("1 01 01010 1")]
    [InlineData(" 101010101 ")]
    public void Ignora_guiones_y_espacios(string input) => Assert.True(Rnc.Validate(input).Valid);

    [Theory]
    [InlineData("101010100")]
    [InlineData("101010102")]
    [InlineData("123456785")]
    [InlineData("123456787")]
    public void Rechaza_un_verificador_equivocado(string input)
    {
        var r = Rnc.Validate(input);
        Assert.False(r.Valid);
        Assert.Equal(IdProblem.CheckDigit, r.Problem);
        Assert.Equal("", r.Formatted);
    }

    [Theory]
    [InlineData("", IdProblem.Length)]
    [InlineData(null, IdProblem.Length)]
    [InlineData("---", IdProblem.Length)]
    [InlineData("10101010", IdProblem.Length)]
    [InlineData("1010101011", IdProblem.Length)]
    [InlineData("10101010A", IdProblem.NotDigits)]
    [InlineData("1010.1010.1", IdProblem.NotDigits)]
    [InlineData("１０１０１０１０１", IdProblem.NotDigits)] // dígitos de ancho completo: no son ASCII
    [InlineData("000000002", IdProblem.Zeros)] // el cuerpo en cero daría verificador 2, pero no existe
    public void Rechaza_lo_que_no_tiene_forma_de_rnc(string? input, IdProblem problem)
    {
        var r = Rnc.Validate(input);
        Assert.False(r.Valid);
        Assert.Equal(problem, r.Problem);
    }

    [Fact]
    public void Para_cada_cuerpo_solo_un_digito_verificador_es_valido_y_nunca_es_cero()
    {
        var random = new Random(1234);
        for (var n = 0; n < 5000; n++)
        {
            var body = random.Next(1, 100_000_000).ToString("D8");
            var valid = Enumerable.Range(0, 10).Where(d => Rnc.Validate(body + d).Valid).ToList();
            Assert.Single(valid);
            Assert.NotEqual(0, valid[0]);
        }
    }
}
