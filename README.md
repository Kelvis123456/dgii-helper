# dgii-helper

API en ASP.NET Core para las dos cosas de la facturación dominicana que se repiten en cualquier
sistema: validar identificadores (RNC, cédula, NCF y e-CF) y entregar secuencias de NCF sin repetir
números aunque lleguen muchas facturas a la vez.

**Estado:** recién creado. Aquí solo hay el esqueleto; cada parte entra en su propio commit.

**Stack:** .NET 10 · ASP.NET Core minimal APIs · EF Core 10 · SQL Server · xUnit + Testcontainers
