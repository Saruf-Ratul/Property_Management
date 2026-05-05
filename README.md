# Property Management Platform

Multi-tenant property management & New Jersey landlord-tenant case automation platform for law firms.

The platform pulls property, tenant, lease, unit, and ledger data from PMS systems (Rent Manager first, then Yardi / AppFolio / Buildium / PropertyFlow), lets law firms create LT cases from PMS data, auto-fills NJ court PDFs, manages full case lifecycle, exposes a client portal for property managers, and writes an immutable audit trail of every sensitive action.

---

## At a glance

| | |
|---|---|
| **Stack** | ASP.NET Core 9 · EF Core 9 · SQL Server · React 18 · TypeScript · Vite · Tailwind |
| **Modules** | Auth · Multi-tenancy · PMS Integrations · Properties · Cases · NJ LT Forms · Client Portal · Audit & Compliance · Dashboard |
| **API** | 71 endpoints across 9 modules · OpenAPI 3 · JWT bearer · role-based authorization |
| **Background jobs** | Hangfire (memory in dev / SQL in prod) — nightly PMS sync at 02:00 UTC |
| **Tests** | xUnit · 27 unit · 13 integration (WebApplicationFactory) |
| **Containers** | docker-compose: SQL Server 2022 + API + nginx-served SPA |

---

## Quick start

### Option A — bare metal (no DB, fastest)

```bash
# Backend (defaults to in-memory EF + Hangfire; seeds reference data + bootstrap admin only)
cd backend
dotnet run --project src/PropertyManagement.Api

# Frontend
cd frontend
npm install
npm run dev
```

The API listens on `http://localhost:5000` and `https://localhost:5001`. The UI runs on `http://localhost:5173` and proxies `/api/*` to the API.

### Option B — full docker-compose stack with SQL Server

```bash
# from the repo root
cp .env.example .env          # edit JWT_SIGNING_KEY before any non-toy deployment
docker compose up --build
```

| Service | URL |
|---|---|
| Frontend (SPA via nginx) | http://localhost:5081 |
| API + Swagger | http://localhost:5080/swagger |
| Hangfire dashboard (FirmAdmin only) | http://localhost:5080/hangfire |
| SQL Server | localhost:1433 (sa / `MSSQL_SA_PASSWORD`) |

The compose stack wires SQL Server health checks, creates a fresh database, and applies the schema via `EnsureCreatedAsync` on first boot. Subsequent runs reuse the `mssql-data` volume so seeded users persist.

### Option C — local SQL Server, native run

The default `appsettings.json` already targets LocalDB (`(localdb)\MSSQLLocalDB`). For dev mode (`appsettings.Development.json`) both `Database.UseInMemory` and `Hangfire.UseInMemory` are now `false`, so a fresh clone runs against SQL Server out of the box. To switch to your own SQL Server, set `ConnectionStrings:Default`.

```bash
cd backend

# (Optional) start LocalDB if it isn't already running
sqllocaldb start MSSQLLocalDB

# Apply the schema. The seeder runs Database.MigrateAsync() at startup, so this is only
# strictly required if you want to verify the SQL ahead of starting the API.
dotnet ef database update -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api

# Boot the API (will run any pending migrations, then seed roles + reference data + bootstrap admin)
dotnet run --project src/PropertyManagement.Api
```

> First run creates the database schema and seeds the bootstrap admin only. There is no demo business data — you start with a clean slate.

#### EF Core migration workflow

```bash
# Add a new migration after changing entity configurations
dotnet ef migrations add <Name> -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api -o Persistence/Migrations

# Apply pending migrations to the configured database
dotnet ef database update -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api

# Roll back to a specific migration
dotnet ef database update <PreviousMigrationName> -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api

# Drop the database (e.g. to re-seed from scratch)
dotnet ef database drop -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api -f

# Generate a deployable SQL script (idempotent — safe to run multiple times)
dotnet ef migrations script --idempotent -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api -o migrations.sql
```

The migration generator uses `AppDbContextDesignTimeFactory` which always targets SQL Server with the connection string from `appsettings.json` (or whatever you pass via `--connection`). Runtime DI still honours the `Database:UseInMemory` flag for tests.

---

## Bootstrap admin

The seeder creates exactly one user so an operator can sign in on first launch:

| Role | Email | Password |
|---|---|---|
| FirmAdmin | `admin@pm.local` | `Admin!2345` |

**Change this password immediately on first login** (or override the email/password by editing `BootstrapAdminEmail` / `BootstrapAdminPassword` in `DataSeeder.cs` before first run). The seeder also creates one empty law firm called "Your Law Firm" — rename it to your firm in **Clients** / firm settings, then add your real clients, PMS integrations, and users from the UI.

System reference data (roles, the 11 case stages, 4 case statuses) is seeded automatically and is required for the platform to function — that is **not** demo data.

The integration test harness adds extra fixtures (a sample `client@acme.local` ClientAdmin and Lawyer/Paralegal/Auditor accounts) via `DataSeeder.SeedTestFixturesAsync`, which is invoked only by `PropertyManagementApiFactory`. Production startup never calls it.

---

## Repository layout

```
Property_Management/
├── backend/                                ASP.NET Core 9 clean-architecture solution
│   ├── PropertyManagement.sln
│   ├── Dockerfile                          .NET 9 multi-stage build
│   ├── src/
│   │   ├── PropertyManagement.Domain/        Entities, enums, value objects (no infra deps)
│   │   ├── PropertyManagement.Application/   DTOs, service interfaces, validators (no infra deps)
│   │   ├── PropertyManagement.Infrastructure/ EF Core, Identity, JWT, Hangfire, PMS, PDF
│   │   └── PropertyManagement.Api/           Controllers, middleware, Program.cs, Swagger
│   └── tests/
│       ├── PropertyManagement.UnitTests/        xUnit (validators, redaction, PDF mapping)
│       └── PropertyManagement.IntegrationTests/ WebApplicationFactory (auth, scoping, public)
├── frontend/                               React 18 + TypeScript + Vite + Tailwind
│   ├── Dockerfile                          nginx + multi-stage build
│   ├── nginx.conf                          /api proxy + SPA fallback
│   └── src/
├── docker-compose.yml                      SQL Server + API + frontend
├── .env.example                            docker-compose env vars
├── docs/
│   ├── IMPLEMENTATION_PLAN.md              Phased roadmap
│   ├── CONFIGURATION.md                    Environment variables reference
│   └── DEPLOYMENT.md                       Azure & AWS deployment guides
└── README.md
```

---

## Modules

1. **Authentication & Authorization** — JWT bearer, ASP.NET Identity, 6 roles (`FirmAdmin`, `Lawyer`, `Paralegal`, `ClientAdmin`, `ClientUser`, `Auditor`), global fallback policy denies anonymous by default.
2. **PMS Integrations** — connector abstraction with one marker interface per provider (Rent Manager, Yardi, AppFolio, Buildium, PropertyFlow). Rent Manager has a working live REST client (auth + paged Properties / Units / Tenants / Leases / Charges / Payments / RecurringCharges / PhoneNumbers); others are stubs that fail with friendly error messages.
3. **Property data** — synced properties, units, tenants, leases, ledger items. Filterable by client / provider / county / status.
4. **Case Management** — full LT case lifecycle: 11 stages × 4 statuses; PMS snapshot at intake; per-case timeline of every status change, comment, payment, document upload, and close event.
5. **NJ LT Forms** — 7 forms (Verified Complaint, Summons, Certifications, LCIS, Warrant of Removal). Per-form `IPdfFieldMappingService` mapper, redaction validator (SSN / DL / credit card / bank / military status), version history, attorney review gate before final packet generation.
6. **Client Portal** — separate UI/route tree at `/portal`. Strict scoping by `ClientId`. ClientAdmin can comment & upload; ClientUser is read-only.
7. **Audit & Compliance** — 24 audit actions covering every spec event. `OldValueJson` / `NewValueJson` columns on update events. CSV export. Available to FirmAdmin / Auditor only.
8. **Dashboard** — KPIs, cases-by-stage, cases-by-client, recent activity, PMS sync status. Auto-scoped for client users.

---

## Configuration & deployment

* Environment variables, secrets, and per-environment defaults live in `docs/CONFIGURATION.md`.
* Production deployment guides for Azure App Service / Azure SQL and AWS ECS Fargate / RDS live in `docs/DEPLOYMENT.md`.
* Phased build-out roadmap (current state → Phase 6 production hardening) lives in `docs/IMPLEMENTATION_PLAN.md`.

---

## Test, build, run

```bash
# Backend tests (unit + integration)
cd backend && dotnet test

# Frontend type check + production build
cd frontend && npm run build

# Full stack with SQL Server
docker compose up --build
```

---

## Key business rules (enforced)

- A case must belong to one law firm and one client (property manager).
- Cases can be created manually or **from a PMS lease**. The latter snapshots PMS data onto the case at intake.
- PMS data is **never automatically overwritten** after intake — re-snapshot is an explicit operator action.
- An attorney must mark the LT case as reviewed before the final filing packet can be generated.
- Public NJ court forms (Verified Complaint, Summons, LCIS, Warrant) are scanned for SSN / DL / credit card / bank account / military-status patterns; matches block PDF generation.
- Client portal users only see their own client's data — defense in depth at controller, service, query, and field-projection layers.
- PMS credentials are encrypted at rest with `IDataProtectionProvider` and never serialized in any API response.
- Every sensitive action is appended to an immutable `AuditLogs` table.
