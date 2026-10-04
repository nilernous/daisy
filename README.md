# Daisy

Fashion e-commerce web application on ASP.NET Core 8, organised with Clean Architecture.

| Project | Role |
|---|---|
| `Daisy.Domain` | Entities, enums, domain rules |
| `Daisy.Application` | Use cases (services, DTOs, validators per feature) |
| `Daisy.Infrastructure` | EF Core / SQL Server, JWT, VietQR (SePay or Mock), storage, email, background job |
| `Daisy.Api` | REST (`/api`) and OData (`/odata`) API |
| `Daisy.Mvc` | Web client in Vietnamese: storefront and admin panel (`/admin`) |
| `Daisy.Tests` | Integration tests against a throw-away LocalDB database |

Design documents: [`docs/md/ARCHITECTURE.md`](docs/md/ARCHITECTURE.md), [`docs/md/API_DESIGN.md`](docs/md/API_DESIGN.md), business rules, use cases and sequence diagrams under [`.claude/`](.claude/).

## Prerequisites

- .NET SDK 8 (a newer SDK also works; `Daisy.slnx` needs SDK 9.0.200 or later to open from the CLI)
- SQL Server 2019+ or LocalDB
- `dotnet-ef` tool for migrations (optional): `dotnet tool install -g dotnet-ef --version 8.*`
- A trusted development certificate: `dotnet dev-certs https --trust`

## 1. Create the database

Either run the SQL scripts (they create a database named `Daisy`; **DaisyDB.sql drops an existing `Daisy` database**):

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -I -i infra/database/DaisyDB.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -I -i infra/database/DaisySeed.sql
```

or apply the EF Core migration, then load the seed:

```bash
dotnet ef database update --project Daisy.Infrastructure
sqlcmd -S "(localdb)\MSSQLLocalDB" -I -i infra/database/DaisySeed.sql
```

`-I` is required (QUOTED_IDENTIFIER ON for filtered indexes). Both ways create the same schema.

The default connection string is `Server=(localdb)\MSSQLLocalDB;Database=Daisy;...` in `Daisy.Api/appsettings.json`. To use another server, override it without editing the file:

```bash
dotnet user-secrets set "ConnectionStrings:Daisy" "Server=.\SQLEXPRESS;Database=Daisy;Trusted_Connection=True;TrustServerCertificate=True" --project Daisy.Api
```

## 2. Set the secrets (once per machine)

```bash
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project Daisy.Api
```

In Development, `VietQr:Provider` is `Mock` with a demo bank account (`appsettings.Development.json`). For real payments, set `VietQr:Provider = SePay`, the receiving account and `VietQr:WebhookApiKey`, and point the SePay webhook to `https://<api-host>/api/payments/webhook`.

Without `Smtp:Host`, password reset links are written to the API log instead of being emailed.

## 3. Run

```bash
dotnet run --project Daisy.Api --launch-profile https   # https://localhost:7001 (Swagger at /swagger)
dotnet run --project Daisy.Mvc --launch-profile https   # https://localhost:7002
```

Seed accounts (development only):

| Role | Email | Password |
|---|---|---|
| Admin | admin@daisy.local | Admin@123 |
| Staff | staff@daisy.local | Staff@123 |
| Customer | customer@daisy.local | Customer@123 |

Vouchers: `DAISY10`, `FREESHIP30`, `WELCOME50K`. With the Mock provider, the VietQR payment page has a "Giả lập đã chuyển khoản (dev)" button that sends a simulated bank transfer through the real webhook flow.

Uploaded images are stored in `storage/` at the repository root (ignored by git) and served by the API under `/uploads`.

## 4. Test

```bash
dotnet test Daisy.Tests
```

The tests create a LocalDB database `DaisyTests_<guid>` from the migrations, load `DaisySeed.sql`, and drop the database afterwards.

## Docs

Keep the docs in sync when requirements or technology change: see the `sync-docs` skill in `.claude/skills/sync-docs/` and run

```bash
python .claude/skills/sync-docs/scripts/check_docs.py
```
