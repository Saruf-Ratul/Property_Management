# Clarence Implementation Plan

This document describes the phased build-out of the Clarence Case Management Platform. The repository in its current state contains a **working MVP foundation** that compiles, runs, seeds data, exposes a Swagger-documented API, and renders a role-based React dashboard. Subsequent phases extend that foundation.

---

## Phase 0 — Foundation (DELIVERED in this commit)

- Clean architecture .NET 9 solution (Domain / Application / Infrastructure / Api).
- Full EF Core domain model: `LawFirm`, `User`, `Client`, `PmsIntegration`, `PmsProperty`, `PmsUnit`, `PmsTenant`, `PmsLease`, `PmsLedgerItem`, `Case`, `CaseStatus`, `CaseStage`, `CaseDocument`, `CaseComment`, `CasePayment`, `CaseActivity`, `LtCase`, `LtCaseFormData`, `LtCaseDocument`, `AttorneySettings`, `GeneratedDocument`, `AuditLog`, `SyncLog`.
- ASP.NET Identity + JWT (access + refresh tokens, role claims).
- Roles: `FirmAdmin`, `Lawyer`, `Paralegal`, `ClientAdmin`, `ClientUser`, `Auditor`.
- Multi-tenant data isolation enforced at the DbContext level via `LawFirmId` global query filter, with a per-request `ITenantContext`.
- Hangfire wired with in-memory storage for development, SQL Server storage for production. Two recurring jobs registered: nightly PMS sync, daily delinquency snapshot.
- Serilog (console + rolling file) configured.
- Swagger / OpenAPI with JWT auth integration.
- Global error-handling middleware that returns RFC-7807 problem details.
- Audit middleware that captures sensitive actions automatically.
- Initial DB seed (roles, case stages/statuses, one law firm shell, one bootstrap FirmAdmin) — no demo business data.
- React 18 + Vite + TypeScript + Tailwind + Router + TanStack Query + Axios.
- Auth context with token persistence, automatic refresh, and protected routes.
- Role-aware sidebar layout.
- Pages scaffolded: Login, Dashboard, Cases (list + detail + new), Properties (list + detail), Tenants, NJ Forms wizard skeleton, Clients, PMS Integrations, Audit Logs, Client Portal.
- Reusable UI primitives: Button, Card, Table, Modal, Input, Select, Badge, EmptyState, Spinner, Toast.

## Phase 1 — Real PMS sync & case-from-PMS

- Replace `RentManagerConnector` HTTP stubs with real Rent Manager API v12 calls (corporate and location tokens).
- Implement transactional sync that upserts on `(IntegrationId, ExternalId)`.
- Implement `SyncJob` Hangfire dispatcher that fans out per integration.
- Add UI flow: select tenant → preview ledger → confirm → create `Case` with PMS snapshot copied into a denormalized `CaseSnapshotJson`.
- Add ledger-driven delinquency report.

## Phase 2 — NJ LT forms engine

- Define each form template (`Verified Complaint`, `Summons`, `LT Certifications`, `LCIS`, `Warrant of Removal`) as JSON form schema with field metadata, source mapping (case / lease / firm / attorney), redaction flags, and signature blocks.
- Implement `IPdfFormFiller` using PdfSharpCore + AcroForm field discovery.
- Implement redaction validator that scans every generated PDF for SSN, DL, bank, credit card patterns and refuses publication if found in a public form.
- Implement merged packet builder (concat in defined order, page-numbered, with cover sheet).
- Add per-form version history (`GeneratedDocument.Version` increments on regeneration).
- Frontend: stepper wizard with auto-fill / manual override / preview modal / packet download.

## Phase 3 — Client portal hardening

- `ClientUser` JWT scoped strictly by `ClientId`; backend filters on `Case.ClientId == user.ClientId`.
- Public/upload signed URLs for `CaseDocument`s.
- Per-document permission flag `IsClientVisible`.
- Comment threads with `IsInternal` flag (paralegal/lawyer-only).

## Phase 4 — Audit, compliance, exports

- Append-only audit table with hash chain (each row's hash = SHA256(prev_hash + payload)) so tampering is detectable.
- Auditor-only views: search/filter, CSV export.
- SAR/data export for client.

## Phase 5 — Additional connectors

- Yardi Voyager 7S SOAP/REST.
- AppFolio Property Manager API.
- Buildium API.
- PropertyFlow API.
- Each connector implements `IPmsConnector` so swap-in is configuration-only.

## Phase 6 — Hardening for production

- Move secrets to Azure Key Vault / AWS Secrets Manager.
- Encrypt `PmsIntegration.CredentialsCipher` with `IDataProtectionProvider`.
- Add CSRF, rate limiting, output caching.
- Add health checks (`/health/live`, `/health/ready`).
- Add OpenTelemetry traces & metrics.
- CI/CD: GitHub Actions / Azure DevOps, infra-as-code (Bicep/Terraform).

---

## Architectural rules followed

- **Domain** has zero references to EF Core, ASP.NET, or HTTP — pure POCOs and enums.
- **Application** depends only on Domain. Defines DTOs and interfaces (`IPmsConnector`, `IPdfFormFiller`, `ICaseService`, `ICurrentUser`, `ITenantContext`).
- **Infrastructure** implements Application interfaces and references Domain + Application. Owns EF Core, Identity, JWT, Hangfire, PMS HTTP clients, PDF service, encryption.
- **Api** is the composition root. Wires DI, middleware, controllers, Swagger.
- **Frontend** never trusts the JWT for authorization decisions other than UI hints; every protected resource is re-checked server side.
- **PMS data is a snapshot at filing time.** A case never silently mutates because PMS data changed.
