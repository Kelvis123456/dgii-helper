Español: [README.es.md](README.es.md)

# dgii-helper

An ASP.NET Core API for two things any Dominican invoicing system ends up needing: checking that an RNC, a cédula, an NCF or an e-CF has a valid shape, and handing out NCF numbers without ever repeating one, even when many invoices arrive at once.

For readers outside the Dominican Republic: the DGII is the country's tax authority, an RNC is a business tax ID, a cédula is the national ID number, and an NCF (or its electronic version, the e-CF) is the sequential number the DGII authorizes for each invoice.

The second part is what interested me. A repeated NCF is not just an app bug, it is a problem with the DGII, so I wanted to see how to actually guarantee uniqueness with SQL Server and not just rely on "it almost never happens".

**Stack:** .NET 10 · ASP.NET Core minimal APIs · EF Core 10 · SQL Server · xUnit + Testcontainers · Angular 21 · GitHub Actions · Docker

## What it does

| Endpoint | Purpose | Key |
|---|---|---|
| `GET /api/rnc/{valor}` | Validates a 9-digit RNC and returns it with dashes | no |
| `GET /api/cedula/{valor}` | Validates an 11-digit cédula | no |
| `GET /api/ncf/{valor}` | Reads an NCF (series B) or an e-CF (series E): type and sequence | no |
| `POST /api/ncf/ranges` | Registers a range authorized by the DGII | yes |
| `GET /api/ncf/ranges/{id}` | How many numbers are left in the range | yes |
| `POST /api/ncf/ranges/{id}/issue` | Hands out the next number | yes |

The key goes in the `X-Api-Key` header. Issuing returns 201 with the NCF, 409 if the range is exhausted, 410 if it has expired and 404 if it does not exist.

```
POST /api/ncf/ranges
{ "kind": "traditional", "type": "01", "first": 1, "last": 500, "expiresOn": "2027-12-31" }

POST /api/ncf/ranges/1/issue
201 { "ncf": "B0100000001", "sequence": 1 }
```

## What it does not do

- It does not query the DGII registry. Validating an RNC means checking the check digit, not knowing whether the company exists.
- It does not validate an e-CF's XML against the DGII schemas, sign it or send it.
- It does not know whether an NCF was actually authorized, only whether it looks like an NCF.
- It does not keep a record of the numbers it issued and does not accept an idempotency key. If the client drops the connection right after SQL Server ran the issue, the number is consumed and nobody sees it: the gap cannot be reconciled from this API. A real invoicing system would have to record every issue and retry with the same key.
- The voucher types come from the DGII's public documentation as I read it. Before using this seriously they need to be checked against the current regulation.

## How to run it

You need .NET 10 and Docker.

```bash
docker compose up -d sql
dotnet run --project src/DgiiHelper.Api
```

In development the key is `dev-key` and the connection string is in `appsettings.Development.json`. Outside development the app does not start if `Auth:ApiKey` is missing. If it runs behind a proxy, set `Proxy:Trusted=true` so the per-IP limit uses the client's real IP; turn it on only if the container cannot be reached without going through the proxy.

```bash
dotnet test
```

The Angular UI lives in `web/`; how to run it is in [web/README.md](web/README.md).

The tests start a real SQL Server with Testcontainers. If you already have one installed, point `DGII_TEST_SQL` at its connection string.

## Decisions

They are in [docs/decisions.md](docs/decisions.md). The two that cost me the most:

- **Issuing with a single `UPDATE ... OUTPUT`** instead of a serializable transaction. With 40 simultaneous requests you get 40 distinct, consecutive numbers.
- **Serializable only for registering ranges**, where you do need to check that they do not overlap. That is where I found that my deadlock retry never fired, because EF wraps the exception in two layers.
