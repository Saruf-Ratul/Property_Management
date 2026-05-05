# Configuration reference

All settings can be supplied via `appsettings.json`, `appsettings.{Environment}.json`, environment variables (using `Section__Key` double-underscore syntax), or .NET user-secrets in development.

## Backend

### Database
| Setting | Default | Notes |
|---|---|---|
| `ConnectionStrings:Default` | `Server=(localdb)\\MSSQLLocalDB;Database=PropertyManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True` | Standard SQL Server connection string. |
| `Database:UseInMemory` | `true` | When `true`, EF Core uses the in-memory provider — perfect for development and tests, **never for production**. |

### Hangfire (background jobs)
| Setting | Default | Notes |
|---|---|---|
| `Hangfire:UseInMemory` | `true` | When `false`, Hangfire uses SQL Server storage and the connection string above. |

### JWT
| Setting | Default | Notes |
|---|---|---|
| `Jwt:Issuer` | `PropertyManagement` | Token issuer (`iss`). |
| `Jwt:Audience` | `PropertyManagement.Clients` | Token audience (`aud`). |
| `Jwt:SigningKey` | `PropertyManagementDevelopmentSigningKey_ChangeMe_Min32Chars_!` | **Must be at least 32 characters.** Rotate regularly in production. Never commit your real key — use Azure Key Vault, AWS Secrets Manager, or .NET user-secrets. |
| `Jwt:AccessTokenMinutes` | `120` | Access-token lifetime. |
| `Jwt:RefreshTokenDays` | `14` | Refresh-token lifetime. |

### Storage
| Setting | Default | Notes |
|---|---|---|
| `Storage:LocalRoot` | `App_Data/documents` | Where uploaded + generated PDFs live. In containers this should be a mounted volume. In production prefer S3/Blob — implement `IDocumentStorage` accordingly. |

### Logging (Serilog)
Configured under `Serilog:MinimumLevel`. Console + rolling file (14-day retention) by default; logs at `logs/PropertyManagement-{date}.log`.

### CORS
Hard-coded in `Program.cs` to allow `http://localhost:5173`, `http://127.0.0.1:5173`, and `http://localhost:4173`. Adjust for your own frontend origin in production.

## Frontend

| Setting | Default | Notes |
|---|---|---|
| `VITE_API_BASE_URL` | `/api` | Path used by the axios client. In docker-compose the nginx config rewrites `/api/*` to the API container; in `npm run dev` Vite proxies `/api/*` to `http://localhost:5000`. |

## docker-compose env

`.env` (sibling of `docker-compose.yml`):

| Variable | Required | Notes |
|---|---|---|
| `MSSQL_SA_PASSWORD` | yes | SQL Server SA password — must satisfy SQL Server complexity rules (>= 8 chars, mix of upper/lower/digit/symbol). |
| `MSSQL_DATABASE` | no (default `PropertyManagementDb`) | Database name. |
| `JWT_ISSUER` | no | Override JWT issuer claim. |
| `JWT_AUDIENCE` | no | Override JWT audience claim. |
| `JWT_SIGNING_KEY` | yes (production) | At least 32 characters. **MUST be set to a real secret in any non-toy deployment.** |

## Secrets in production

- **Azure**: store secrets in Azure Key Vault and reference them from App Service application settings or Container Apps secrets. `Jwt:SigningKey`, the SQL admin password, and any future cloud-storage keys belong here.
- **AWS**: use AWS Secrets Manager + ECS task definition `secretsArn` references.
- **Local development**: `dotnet user-secrets set Jwt:SigningKey "..."` from the API project folder.

## Identity / role seeding

`PropertyManagement.Infrastructure.Persistence.DataSeeder.SeedAsync` runs at boot and writes only the bare minimum needed for the platform to function:
1. Creates the 6 roles if missing (`FirmAdmin`, `Lawyer`, `Paralegal`, `ClientAdmin`, `ClientUser`, `Auditor`).
2. Creates 11 case stages and 4 case statuses.
3. Creates one empty law firm ("Your Law Firm") and one bootstrap FirmAdmin user (`admin@pm.local` / `Admin!2345`) so an operator can sign in on first launch.

There is no demo client, demo PMS integration, demo PMS data, or demo cases. Change the bootstrap password on first login. To override the bootstrap email/password, edit the `BootstrapAdminEmail` / `BootstrapAdminPassword` constants in `DataSeeder.cs` before first run, or pre-create the user via SQL and the seeder will skip it.

The integration test harness loads additional demo fixtures (extra role accounts, a sample client) via `DataSeeder.SeedTestFixturesAsync`. That method is only invoked from `PropertyManagementApiFactory` and never runs in production.
