English: [README.md](README.md)

# dgii-helper

API en ASP.NET Core para dos cosas que cualquier sistema de facturación dominicano termina necesitando: comprobar que un RNC, una cédula, un NCF o un e-CF tienen forma válida, y entregar números de NCF sin repetir ninguno aunque lleguen muchas facturas a la vez.

Lo segundo es la parte que me interesaba. Un NCF repetido no es solo un bug de la app, es un problema con la DGII, así que quise ver cómo se garantiza de verdad con SQL Server y no solo con "casi nunca pasa".

**Stack:** .NET 10 · ASP.NET Core minimal APIs · EF Core 10 · SQL Server · xUnit + Testcontainers · Angular 21 · GitHub Actions · Docker

## Qué hace

| Endpoint | Para qué | Clave |
|---|---|---|
| `GET /api/rnc/{valor}` | Valida un RNC de 9 dígitos y lo devuelve con guiones | no |
| `GET /api/cedula/{valor}` | Valida una cédula de 11 dígitos | no |
| `GET /api/ncf/{valor}` | Lee un NCF (serie B) o un e-CF (serie E): tipo y secuencia | no |
| `POST /api/ncf/ranges` | Registra un rango autorizado por la DGII | sí |
| `GET /api/ncf/ranges/{id}` | Cuántos números quedan en el rango | sí |
| `POST /api/ncf/ranges/{id}/issue` | Entrega el siguiente número | sí |

La clave va en el header `X-Api-Key`. Emitir responde 201 con el NCF, 409 si el rango se agotó, 410 si venció y 404 si no existe.

```
POST /api/ncf/ranges
{ "kind": "traditional", "type": "01", "first": 1, "last": 500, "expiresOn": "2027-12-31" }

POST /api/ncf/ranges/1/issue
201 { "ncf": "B0100000001", "sequence": 1 }
```

## Lo que no hace

- No consulta el padrón de la DGII. Validar un RNC es comprobar el dígito verificador, no saber si la empresa existe.
- No valida el XML de un e-CF contra los esquemas de la DGII ni lo firma ni lo envía.
- No sabe si un NCF fue realmente autorizado, solo si tiene forma de NCF.
- No guarda un registro de los números entregados ni acepta una clave de idempotencia. Si el cliente corta la conexión justo después de que SQL Server ejecutó la entrega, el número se consume y nadie lo ve: el hueco no se puede conciliar desde esta API. Un sistema de facturación real tendría que registrar cada emisión y reintentar con la misma clave.
- Los tipos de comprobante salen de la documentación pública de la DGII tal como la leí. Antes de usarlo en serio hay que contrastarlos con la norma vigente.

## Cómo correrlo

Necesitas .NET 10 y Docker.

```bash
docker compose up -d sql
dotnet run --project src/DgiiHelper.Api
```

En desarrollo la clave es `dev-key` y la conexión está en `appsettings.Development.json`. Fuera de desarrollo la app no arranca si falta `Auth:ApiKey`. Si corre detrás de un proxy, pon `Proxy:Trusted=true` para que el límite por IP use la IP real del cliente; actívalo solo si el contenedor no se alcanza sin pasar por el proxy.

```bash
dotnet test
```

La interfaz en Angular vive en `web/`; cómo correrla está en [web/README.md](web/README.md).

Los tests levantan un SQL Server real con Testcontainers. Si ya tienes uno instalado, apunta `DGII_TEST_SQL` a su cadena de conexión.

## Decisiones

Están en [docs/decisions.md](docs/decisions.md). Las dos que más me costaron:

- **Emitir con un solo `UPDATE ... OUTPUT`** en vez de una transacción serializable. Con 40 peticiones simultáneas salen 40 números distintos y seguidos.
- **Serializable solo para registrar rangos**, donde sí hay que comprobar que no se solapen. Ahí encontré que mi reintento por deadlock nunca se activaba porque EF envuelve la excepción en dos capas.
