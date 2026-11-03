using DgiiHelper.Api.Identifiers;

namespace DgiiHelper.Tests.Identifiers;

public class CedulaTests
{
    [Theory]
    [InlineData("00112345673", "001-1234567-3")]
    [InlineData("00100000009", "001-0000000-9")]
    [InlineData("40200000004", "402-0000000-4")]
    [InlineData("00101234565", "001-0123456-5")]
    public void Acepta_una_cedula_con_su_verificador_correcto(string input, string formatted)
    {
        var r = Cedula.Validate(input);
        Assert.True(r.Valid);
        Assert.Equal(formatted, r.Formatted);
        Assert.Equal(input, r.Digits);
    }

    [Theory]
    [InlineData("001-1234567-3")]
    [InlineData("001 1234567 3")]
    public void Ignora_guiones_y_espacios(string input) => Assert.True(Cedula.Validate(input).Valid);

    [Theory]
    [InlineData("00112345670")]
    [InlineData("00112345674")]
    [InlineData("00100000000")]
    public void Rechaza_un_verificador_equivocado(string input)
    {
        var r = Cedula.Validate(input);
        Assert.False(r.Valid);
        Assert.Equal(IdProblem.CheckDigit, r.Problem);
    }

    [Theory]
    [InlineData("", IdProblem.Length)]
    [InlineData(null, IdProblem.Length)]
    [InlineData("0011234567", IdProblem.Length)]
    [InlineData("001123456733", IdProblem.Length)]
    [InlineData("0011234567X", IdProblem.NotDigits)]
    [InlineData("00000000000", IdProblem.Zeros)] // pasa Luhn, pero 000 no es un municipio
    [InlineData("00012345678", IdProblem.Zeros)]
    public void Rechaza_lo_que_no_tiene_forma_de_cedula(string? input, IdProblem problem)
    {
        var r = Cedula.Validate(input);
        Assert.False(r.Valid);
        Assert.Equal(problem, r.Problem);
    }

    [Fact]
    public void Coincide_con_una_implementacion_independiente_de_Luhn_en_20000_numeros()
    {
        var random = new Random(99);
        for (var n = 0; n < 20_000; n++)
        {
            // La mitad de los números con el verificador bueno, la otra mitad al azar.
            var body = random.Next(1, 1000).ToString("D3") + random.Next(0, 10_000_000).ToString("D7");
            var digit = n % 2 == 0 ? LuhnCheckDigit(body) : random.Next(0, 10);
            var number = body + digit;

            Assert.Equal(StandardLuhnValid(number), Cedula.Validate(number).Valid);
        }
    }

    // Luhn tal como se enseña: de derecha a izquierda, se duplica uno sí y uno no empezando por el
    // segundo, y la suma de todo debe ser múltiplo de 10. No comparte código con Cedula.
    private static bool StandardLuhnValid(string number)
    {
        var sum = 0;
        for (var i = 0; i < number.Length; i++)
        {
            var d = number[number.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }

    private static int LuhnCheckDigit(string body) =>
        Enumerable.Range(0, 10).Single(d => StandardLuhnValid(body + d));
}
