# Deployment guide

This document covers two recommended cloud topologies. Both expect that you have built and pushed the API + frontend Docker images to a registry (ACR / ECR / Docker Hub).

```bash
# Build and tag locally
docker build -t propertymanagement/api:1.0.0  ./backend
docker build -t propertymanagement/web:1.0.0  ./frontend

# Push to your registry
docker push <registry>/propertymanagement/api:1.0.0
docker push <registry>/propertymanagement/web:1.0.0
```

---

## Azure (recommended)

### Topology
```
┌──────────────────────┐     ┌──────────────────────┐
│  Azure Front Door /  │◄───►│  Azure App Service   │  api  (container)
│  Application Gateway │     │  (Linux, .NET 9)     │
└──────────┬───────────┘     └──────────┬───────────┘
           │                            │
           ▼                            ▼
┌──────────────────────┐     ┌──────────────────────┐
│  Azure App Service   │     │  Azure SQL Database  │
│  Static Web Apps     │     │  (Hyperscale or GP)  │
│  (frontend SPA)      │     └──────────────────────┘
└──────────────────────┘                │
                                        ▼
                              ┌──────────────────────┐
                              │  Azure Key Vault     │
                              │  (Jwt key, SQL pwd)  │
                              └──────────────────────┘
```

### Steps

1. **Provision SQL** — create an Azure SQL Database (Single Database, GeneralPurpose Gen5 2 vCore is fine for staging). Allow the App Service outbound subnet on the SQL firewall.
2. **Provision Key Vault** — store `Jwt:SigningKey`, the SQL connection string, and any future provider API keys.
3. **Provision App Service** — Linux, container deployment, image `<registry>/propertymanagement/api:<tag>`. Enable "Always On" and "Managed identity (system-assigned)". Grant the identity `Get` + `List` on the Key Vault secrets.
4. **App Service application settings** (env vars):
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `Database__UseInMemory=false`
   - `Hangfire__UseInMemory=false`
   - `ConnectionStrings__Default=@Microsoft.KeyVault(SecretUri=https://<kv>.vault.azure.net/secrets/SqlConn/)`
   - `Jwt__Issuer=<your-issuer>`
   - `Jwt__Audience=<your-audience>`
   - `Jwt__SigningKey=@Microsoft.KeyVault(SecretUri=https://<kv>.vault.azure.net/secrets/JwtSigningKey/)`
   - `Storage__LocalRoot=/home/site/wwwroot/App_Data/documents` (or use a Blob storage adapter — see below)
5. **Migrations on first deploy** — exec into the container and run `dotnet PropertyManagement.Api.dll --migrate-only` (or use a startup gate). Until that flag is implemented, set `Database__UseInMemory=false` and let `EnsureCreatedAsync` run the first time, then switch to migrations for subsequent schema changes.
6. **Hangfire dashboard** — gated to FirmAdmin via `HangfireAuthorizationFilter`; restrict by IP at the Front Door / App Gateway layer if you want extra hardening.
7. **Frontend** — build and deploy `frontend/` to Azure Static Web Apps OR deploy the nginx container to a second App Service. Set `VITE_API_BASE_URL` at build time to the public API URL.
8. **Secrets rotation** — rotate `Jwt:SigningKey` quarterly. App Service will pick up Key Vault references on the next request after a Key Vault rotation.

### Document storage

For production, swap `LocalFileDocumentStorage` for an Azure Blob implementation:

```csharp
public class AzureBlobDocumentStorage : IDocumentStorage
{
    private readonly BlobContainerClient _container;
    public AzureBlobDocumentStorage(BlobContainerClient container) => _container = container;
    // SaveAsync → UploadAsync, OpenAsync → DownloadStreamingAsync, etc.
}
```

Register it in `DependencyInjection.cs` instead of the local-file impl.

---

## AWS

### Topology
```
┌──────────────────┐     ┌──────────────────────┐
│  CloudFront /    │◄───►│  ECS Fargate         │  api  (task)
│  ALB             │     │  (.NET 9 container)  │
└────────┬─────────┘     └──────────┬───────────┘
         │                          │
         ▼                          ▼
┌──────────────────┐     ┌──────────────────────┐
│  S3 (frontend +  │     │  RDS for SQL Server  │
│  generated PDFs) │     │  (or Aurora MySQL    │
└──────────────────┘     │  with EF provider)   │
                         └──────────────────────┘
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │  AWS Secrets Manager │
                         └──────────────────────┘
```

### Steps

1. **Provision RDS for SQL Server** — Standard Edition Single-AZ for staging, Multi-AZ for production. Create the `PropertyManagementDb` database.
2. **Provision Secrets Manager secrets** — `pm/jwt-signing-key`, `pm/sql-conn-string`. Tag them so the ECS task IAM role can read them.
3. **Build the API image and push to ECR**:
   ```bash
   aws ecr get-login-password | docker login --username AWS --password-stdin <id>.dkr.ecr.<region>.amazonaws.com
   docker tag propertymanagement/api:1.0.0 <id>.dkr.ecr.<region>.amazonaws.com/propertymanagement/api:1.0.0
   docker push <id>.dkr.ecr.<region>.amazonaws.com/propertymanagement/api:1.0.0
   ```
4. **Create the ECS task definition** with these env vars and `secrets`:
   ```jsonc
   {
     "containerDefinitions": [{
       "image": "<id>.dkr.ecr…/propertymanagement/api:1.0.0",
       "portMappings": [{ "containerPort": 8080 }],
       "environment": [
         { "name": "ASPNETCORE_ENVIRONMENT", "value": "Production" },
         { "name": "Database__UseInMemory",   "value": "false" },
         { "name": "Hangfire__UseInMemory",   "value": "false" },
         { "name": "Jwt__Issuer",             "value": "PropertyManagement" },
         { "name": "Jwt__Audience",           "value": "PropertyManagement.Clients" }
       ],
       "secrets": [
         { "name": "ConnectionStrings__Default", "valueFrom": "arn:aws:secretsmanager:<region>:<id>:secret:pm/sql-conn-string" },
         { "name": "Jwt__SigningKey",            "valueFrom": "arn:aws:secretsmanager:<region>:<id>:secret:pm/jwt-signing-key" }
       ],
       "healthCheck": {
         "command": ["CMD-SHELL", "wget -q -O - http://localhost:8080/api/health || exit 1"],
         "interval": 30, "retries": 3, "startPeriod": 30
       }
     }]
   }
   ```
5. **ALB / target group** on port 8080, health check at `/api/health`.
6. **Frontend** — `npm run build` produces `dist/`. Upload to an S3 bucket configured for static website hosting and front it with CloudFront. Set the build-time `VITE_API_BASE_URL` to the API's public DNS name (or use a CloudFront behavior to route `/api/*` → ALB).
7. **Document storage** — write an `S3DocumentStorage : IDocumentStorage` implementation; register it instead of the local-file one. Generated PDFs and uploaded documents will land in S3 for durability.
8. **Hangfire** — works as-is against RDS for SQL Server. You can run a separate ECS service dedicated to Hangfire if you want background-job isolation from API request handling; the current `AddHangfireServer()` runs in-process which is fine for most workloads.

### Logging / observability

- **Azure**: Application Insights via `services.AddApplicationInsightsTelemetry(...)`. Serilog will continue to write to console/file; Application Insights captures the same.
- **AWS**: CloudWatch Logs auto-captures stdout from ECS Fargate. For traces add OpenTelemetry → AWS Distro for OpenTelemetry collector → AWS X-Ray.

---

## Database migrations

Until you switch to a relational provider in production, migrations are not applied automatically. The recommended deployment flow:

```bash
# Generate a migration locally after model changes.
dotnet ef migrations add <Name> -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api

# Apply migrations to your production database from CI/CD using a one-shot job:
dotnet ef database update -p src/PropertyManagement.Infrastructure -s src/PropertyManagement.Api \
  --connection "Server=...;Database=PropertyManagementDb;User Id=...;Password=...;TrustServerCertificate=True"
```

For Azure SQL, run this from a deployment slot or GitHub Actions runner that has SQL firewall access.

## Smoke checks after deploy

```bash
curl https://<your-api>/api/health                        # → 200 {status:"ok"}
curl https://<your-api>/swagger/v1/swagger.json | head    # → OpenAPI document
```

Then sign in with the bootstrap admin (`admin@pm.local` / `Admin!2345`) and **change the password immediately** (or pre-create your own administrator user via SQL before first launch and the seeder will skip the bootstrap user). No demo data is created — you start with an empty firm and add your real clients, PMS integrations, and users from the UI.
