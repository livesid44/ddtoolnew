# BPO Platform – End-to-End .NET + Azure Development Plan

> **Purpose:** This document is the single source of truth for developing the BPO AI Process Discovery Platform using .NET 8 and Azure services. It covers architecture, technology choices, implementation phases, customisation points, and operational practices.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Solution Structure](#2-solution-structure)
3. [Technology Stack](#3-technology-stack)
4. [Azure Services Map](#4-azure-services-map)
5. [Implementation Phases](#5-implementation-phases)
6. [Configuration & Customisation](#6-configuration--customisation)
7. [Security](#7-security)
8. [Infrastructure as Code](#8-infrastructure-as-code)
9. [CI/CD Pipeline](#9-cicd-pipeline)
10. [Local Development Setup](#10-local-development-setup)
11. [Testing Strategy](#11-testing-strategy)
12. [Monitoring & Observability](#12-monitoring--observability)
13. [Extending the Platform](#13-extending-the-platform)

---

## 1. Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        CLIENT LAYER                                      │
│  HTML/JS Wireframe (AURA theme)  ←→  Future Blazor / React SPA           │
└──────────────────────────┬───────────────────────────────────────────────┘
                           │  HTTPS / REST
┌──────────────────────────▼───────────────────────────────────────────────┐
│                        API LAYER                                         │
│  ASP.NET Core 8 Web API  ·  JWT (Azure AD / Entra ID)  ·  Swagger       │
└──────────┬─────────────────────────────────────────────┬─────────────────┘
           │  MediatR (CQRS)                             │  Events
┌──────────▼───────────────────┐          ┌─────────────▼─────────────────┐
│      APPLICATION LAYER        │          │     AZURE FUNCTIONS            │
│  Commands · Queries · DTOs    │          │  BlobTrigger · TimerTrigger    │
│  FluentValidation · Pipeline  │          │  (AI analysis, notifications)  │
└──────────┬───────────────────┘          └─────────────┬─────────────────┘
           │  Interfaces                                 │
┌──────────▼───────────────────────────────────────────▼──────────────────┐
│                      INFRASTRUCTURE LAYER                                │
│  EF Core → Azure SQL  ·  Azure Blob Storage  ·  Azure OpenAI             │
│  Azure Key Vault  ·  Managed Identity (no stored secrets)                │
└──────────────────────────────────────────────────────────────────────────┘
```

**Key design principles:**
- **Clean Architecture** – domain logic has zero Azure/infrastructure dependencies.
- **CQRS via MediatR** – reads (Queries) and writes (Commands) are separated, easily swapped.
- **Interface-driven** – every Azure service is hidden behind a domain interface; swap or mock at will.
- **No stored secrets** – Managed Identity everywhere in production; Azure Key Vault for bootstrapping.

---

## 2. Solution Structure

```
ddtoolnew/
├── src/
│   ├── BPOPlatform.Domain/          # Entities, domain events, interfaces (no dependencies)
│   │   ├── Common/                  #   BaseEntity, IDomainEvent
│   │   ├── Entities/                #   Process, ProcessArtifact, WorkflowStep
│   │   ├── Enums/                   #   ProcessStatus, ArtifactType, UserRole …
│   │   ├── Events/                  #   ProcessCreatedEvent, StatusChangedEvent …
│   │   └── Interfaces/              #   IRepository<T>, IBlobStorageService, IAiAnalysisService
│   │
│   ├── BPOPlatform.Application/     # CQRS handlers, DTOs, validators, behaviours
│   │   ├── Processes/Commands/      #   CreateProcessCommand, AdvanceStatusCommand
│   │   ├── Processes/Queries/       #   GetAllProcessesQuery, GetProcessByIdQuery
│   │   ├── Processes/DTOs/          #   ProcessDto, ProcessSummaryDto, ArtifactDto
│   │   ├── Common/Behaviours/       #   ValidationBehaviour (pipeline)
│   │   ├── Common/Mappings/         #   Entity → DTO extension methods
│   │   └── DependencyInjection/     #   AddApplicationServices()
│   │
│   ├── BPOPlatform.Infrastructure/  # Azure implementations of domain interfaces
│   │   ├── Persistence/             #   BPODbContext, EF Core repositories, UnitOfWork
│   │   ├── Services/                #   AzureBlobStorageService, AzureOpenAiAnalysisService
│   │   └── DependencyInjection/     #   AddInfrastructureServices(config)
│   │
│   ├── BPOPlatform.Api/             # ASP.NET Core 8 Web API (composition root)
│   │   ├── Controllers/             #   ProcessesController (REST endpoints)
│   │   ├── Program.cs               #   Startup, DI wiring, Serilog, auth
│   │   ├── appsettings.json         #   Production config template
│   │   └── appsettings.Development.json  # Local dev overrides (SQL + Azurite)
│   │
│   └── BPOPlatform.Functions/       # Azure Functions isolated worker
│       ├── Program.cs               #   Host builder
│       └── ArtifactAnalysisTrigger  #   BlobTrigger → AI analysis → DB update
│
├── infra/
│   ├── main.bicep                   # All Azure resources (parameterised per env)
│   └── main.parameters.json         # Environment parameter values
│
├── .github/workflows/
│   └── ci-cd.yml                    # Build → Test → Security → Infra → Deploy
│
└── docs/
    └── DOTNET-AZURE-PLAN.md         # This document
```

---

## 3. Technology Stack

| Layer | Technology | Reason |
|-------|-----------|--------|
| Runtime | .NET 8 LTS | Long-term support, performance, cross-platform |
| API | ASP.NET Core 8 Web API | Mature, controller or minimal-API style |
| CQRS | MediatR 12 | Decouples API from business logic |
| Validation | FluentValidation 11 | Expressive, pipeline-integrated |
| ORM | EF Core 8 + SQL Server provider | Type-safe queries, migrations |
| Auth | Microsoft.Identity.Web 3.8.2 | Azure AD / Entra ID with MSAL |
| Logging | Serilog + Application Insights sink | Structured logs, correlation |
| Blob | Azure.Storage.Blobs 12 | Official Azure SDK, Managed Identity |
| AI | Azure.AI.OpenAI 2.1 | Official SDK for Azure OpenAI |
| Functions | Azure Functions v4 isolated worker | Serverless, event-driven |
| IaC | Bicep | ARM-native, readable, modular |
| CI/CD | GitHub Actions | OIDC / Workload Identity, no long-lived secrets |

---

## 4. Azure Services Map

> **Full catalogue:** See [`docs/AZURE-SERVICES.md`](./AZURE-SERVICES.md) for the complete per-service reference including SKUs, Bicep resource types, `appsettings` keys, Managed Identity role assignments, cost tips, and local-development fallbacks.

| Service | Purpose | Flexible Alternative |
|---------|---------|---------------------|
| **Microsoft Entra ID** | Authentication & RBAC (JWT bearer + MSAL.js) | B2C for external users |
| **Azure Key Vault** | Secrets & certificates (no secrets in code) | Managed HSM for regulated environments |
| **Azure App Service (Linux)** | Host `BPOPlatform.Api` + SignalR hub | Azure Container Apps, AKS |
| **Azure Static Web Apps** | Host `BPOPlatform.Web` HTML/JS frontend | CDN + Blob, Azure Front Door |
| **Azure Functions v4** | Blob-triggered AI pipeline, Durable orchestration | Logic Apps |
| **Azure SQL Database** | Relational process data (EF Core + Managed Identity) | PostgreSQL Flexible Server, Cosmos DB |
| **Azure Blob Storage** | Process artifact files + Functions state store | ADLS Gen2 for analytics |
| **Azure OpenAI** | LLM analysis + document generation (GPT-4o) | Azure AI Foundry, custom models |
| **Azure AI Document Intelligence** | PDF / image text extraction (`prebuilt-read`) | Custom Form Recognizer model |
| **Azure AI Speech Services** | Audio transcription (MP3/WAV/M4A interviews) | Azure Video Indexer |
| **Azure SignalR Service** | Scale real-time notifications (production) | Self-hosted SignalR (single instance) |
| **Power Automate** | External ticketing (ServiceNow, Jira, ADO) | Azure Logic Apps, custom connector |
| **Application Insights** | APM, distributed tracing, structured logs | Azure Monitor, Grafana |
| **Log Analytics Workspace** | Backing store for Application Insights + KQL | — |
| **Azure Front Door + WAF** | Global LB, DDoS, WAF *(Phase 5)* | Application Gateway |
| **Azure Virtual Network** | Private endpoints for SQL + Blob *(Phase 5)* | — |

All Azure services are encapsulated behind interfaces — swap any service without touching domain logic.

---

## 5. Implementation Phases

### Phase 1 – Foundation (Weeks 1–2) ✅
- [x] Clean Architecture .NET solution scaffolded
- [x] Domain entities: `Process`, `ProcessArtifact`, `WorkflowStep`
- [x] Domain interfaces: `IProcessRepository`, `IBlobStorageService`, `IAiAnalysisService`
- [x] Application: CQRS commands/queries for Process lifecycle
- [x] Infrastructure: EF Core + Azure Blob + Azure OpenAI
- [x] API: JWT auth, Swagger, CORS, Serilog
- [x] Azure Functions: blob-triggered AI analysis
- [x] Bicep IaC: SQL, Blob, OpenAI, App Service, Key Vault, Managed Identity RBAC
- [x] GitHub Actions CI/CD pipeline (build → test → deploy)

### Phase 2 – Core Features (Weeks 3–6) ✅
- [x] EF Core migrations + seeding (default workflow steps)
- [x] Artifact upload endpoint (`POST /api/v1/processes/{id}/artifacts`)
- [x] AI analysis endpoint (`POST /api/v1/dashboard/processes/{id}/analyse`)
- [x] User management + RBAC claims (`Admin`, `Manager`, `Analyst`, `Viewer`)
- [x] Kanban task board entities + CQRS handlers
- [x] Dashboard KPI queries (processes by status, avg scores)
- [x] Pagination, sorting, filtering on all list endpoints
- [x] Integration tests (xUnit + WebApplicationFactory + EF Core InMemory)

### Phase 3 – Frontend Integration (Weeks 7–9) ✅
- [x] Wire existing HTML pages to REST API (fetch / Axios)
- [x] Azure AD MSAL.js v3 authentication in frontend (dev bypass for local development)
- [x] Real file upload to Blob via API (multipart with XHR progress bars)
- [x] Real-time notifications (SignalR `NotificationHub` on App Service; `notifications.js` toast client)
- [x] Replace chart placeholders with real data (Chart.js bar/doughnut/line from `/api/v1/dashboard/kpis`)
- [x] API client module (`js/api-client.js`) with Bearer token injection and loading indicator
- [x] All pages wired: dashboard, upload, analysis, kanban, workflow, login

### Phase 4 – AI & Automation (Weeks 10–12) ✅
- [x] Document intelligence (Azure AI Document Intelligence for PDF extraction)
- [x] Speech-to-text transcription (Azure AI Speech Services for MP3/WAV/M4A)
- [x] Automated workflow advancement (Durable Functions orchestration with 4 activities)
- [x] Power Automate connector for external ticketing (ServiceNow, Jira, Azure DevOps)
- [x] LLM-generated process documentation (Markdown / HTML / Word `.docx` export)

### Phase 5 – Production Hardening (Weeks 13–16)
- [ ] API versioning (`/api/v1/`, `/api/v2/`)
- [ ] Rate limiting (ASP.NET Core 8 built-in)
- [ ] Health checks (`/healthz`, `/readyz`) + Azure App Service health probe
- [ ] Azure Front Door + WAF (DDoS, geo-filtering)
- [ ] Private endpoints for SQL + Blob (VNet integration)
- [ ] Disaster recovery runbook + RTO/RPO targets
- [ ] Performance load testing (k6 / Azure Load Testing)
- [ ] GDPR data export & erasure endpoints

---

## 6. Configuration & Customisation

All settings are externalised to `appsettings.json` / environment variables. **No code change is needed to switch Azure regions, tiers, or service endpoints.**

### Key configuration sections

```json
// appsettings.json (template – values injected by Bicep / Key Vault)
{
  "AzureAd":        { "TenantId": "...", "ClientId": "..." },
  "ConnectionStrings": { "DefaultConnection": "...", "BlobStorage": "..." },
  "AzureStorage":   { "ServiceUri": "...", "ArtifactsContainerName": "process-artifacts" },
  "AzureOpenAI":    { "Endpoint": "...", "DeploymentName": "gpt-4o" },
  "ApplicationInsights": { "ConnectionString": "..." },
  "AllowedOrigins": ["https://your-frontend.azurestaticapps.net"]
}
```

### Swap any service without changing domain code

| Scenario | What to change |
|----------|---------------|
| Use PostgreSQL instead of SQL Server | Replace `UseSqlServer()` with `UseNpgsql()` in `AddInfrastructureServices()` |
| Use local filesystem instead of Blob | Implement `ILocalBlobStorageService : IBlobStorageService`, register in DI |
| Use a different LLM provider | Implement `IOpenAiAnalysisService : IAiAnalysisService`, register in DI |
| Add a new workflow step | Add enum value + seed new `WorkflowStep` entity |
| Add a new API controller | Create controller, add commands/queries in Application layer |

---

## 7. Security

| Control | Implementation |
|---------|---------------|
| Authentication | Azure AD / Entra ID (JwtBearer + Microsoft.Identity.Web) |
| Authorisation | Claims-based RBAC, `[Authorize(Roles = "Admin")]` |
| Secrets management | Azure Key Vault; Managed Identity access; **no connection strings in code** |
| TLS | HTTPS enforced on all App Services; TLS 1.2 minimum on SQL + Storage |
| Blob access | Private containers; SAS tokens with short expiry for downloads |
| SQL | Managed Identity auth (no password in connection string for production) |
| CI/CD | OIDC Workload Identity Federation (no stored client secrets in GitHub) |
| Dependency scanning | `dotnet list package --vulnerable` in every pipeline run |
| Input validation | FluentValidation on every command; EF parameterised queries (no raw SQL) |

---

## 8. Infrastructure as Code

All Azure resources are defined in `infra/main.bicep`. Deploy with:

```bash
# One-time: create resource group
az group create --name bpo-dev-rg --location eastus

# Deploy infrastructure
az deployment group create \
  --resource-group bpo-dev-rg \
  --template-file infra/main.bicep \
  --parameters @infra/main.parameters.json \
  --parameters environment=dev sqlAdminPassword="YourStrong!Passw0rd"
```

**Parameterised:** every resource name, SKU, and region is a parameter – promoting from dev → prod is a single parameter change.

---

## 9. CI/CD Pipeline

The `.github/workflows/ci-cd.yml` pipeline runs automatically:

| Job | Trigger | Action |
|-----|---------|--------|
| `build-and-test` | Every push / PR | `dotnet build` + `dotnet test` |
| `security-scan` | After build | `dotnet list package --vulnerable` |
| `publish-api` | `main` branch only | `dotnet publish` API → artifact |
| `publish-functions` | `main` branch only | `dotnet publish` Functions → artifact |
| `deploy-infra` | After publish | `az deployment group create` (Bicep) |
| `deploy-api` | After infra | `azure/webapps-deploy` |
| `deploy-functions` | After infra | `azure/functions-action` |

**Required GitHub secrets:**
```
AZURE_CLIENT_ID          # Service principal / managed identity client ID
AZURE_TENANT_ID          # Azure AD tenant
AZURE_SUBSCRIPTION_ID    # Azure subscription
AZURE_RESOURCE_GROUP     # Target resource group
AZURE_API_APP_NAME       # App Service name
AZURE_FUNCTIONS_APP_NAME # Function App name
SQL_ADMIN_PASSWORD       # Used only during infra deploy; stored in Key Vault afterwards
```

---

## 10. Local Development Setup

### Prerequisites
- .NET 8 SDK
- Docker Desktop (for SQL Server + Azurite via containers)
- Azure Functions Core Tools v4
- Visual Studio 2022 / VS Code + C# Dev Kit

### Start local dependencies

```bash
# SQL Server (Docker)
docker run -e ACCEPT_EULA=Y -e SA_PASSWORD="YourStrong!Passw0rd" \
  -p 1433:1433 --name bpo-sql -d mcr.microsoft.com/mssql/server:2022-latest

# Azurite (Azure Storage emulator)
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  --name bpo-azurite -d mcr.microsoft.com/azure-storage/azurite
```

### Apply EF Core migrations

```bash
cd src
dotnet ef database update --project BPOPlatform.Infrastructure --startup-project BPOPlatform.Api
```

### Run the API

```bash
cd src/BPOPlatform.Api
dotnet run
# Swagger UI: https://localhost:7xxx/swagger
```

### Run Azure Functions locally

```bash
cd src/BPOPlatform.Functions
func start
```

---

## 11. Testing Strategy

| Type | Framework | Location |
|------|-----------|---------|
| Unit tests (domain + application) | xUnit + Moq | `tests/BPOPlatform.UnitTests/` |
| Integration tests (API + DB) | xUnit + Testcontainers (SQL Server) | `tests/BPOPlatform.IntegrationTests/` |
| End-to-end tests | Playwright / Azure Load Testing | `tests/BPOPlatform.E2ETests/` |

**Add test projects:**
```bash
cd src
dotnet new xunit -n BPOPlatform.UnitTests
dotnet new xunit -n BPOPlatform.IntegrationTests
dotnet sln BPOPlatform.slnx add BPOPlatform.UnitTests BPOPlatform.IntegrationTests
```

---

## 12. Monitoring & Observability

| Concern | Tool |
|---------|------|
| Structured logging | Serilog → Application Insights |
| Distributed tracing | Application Insights + correlation IDs |
| Metrics | Azure Monitor, custom KPIs via `TelemetryClient` |
| Alerting | Azure Monitor alert rules → Action Groups (email, Teams) |
| Dashboards | Azure Workbooks / Power BI connected to Application Insights |
| Health checks | `/healthz` (liveness), `/readyz` (readiness) – App Service probe |

---

## 13. Extending the Platform

### Add a new domain entity
1. Create entity in `BPOPlatform.Domain/Entities/`
2. Add `DbSet<T>` to `BPODbContext`
3. Create repository interface in `Domain/Interfaces/`
4. Implement repository in `Infrastructure/Persistence/Repositories/`
5. Add commands/queries in `Application/`
6. Add controller in `Api/Controllers/`
7. Run `dotnet ef migrations add <Name>`

### Add a new Azure service
1. Define interface in `BPOPlatform.Domain/Interfaces/`
2. Implement in `BPOPlatform.Infrastructure/Services/`
3. Register in `AddInfrastructureServices()`
4. Add Bicep resource to `infra/main.bicep`
5. Add RBAC role assignment for Managed Identity

### Swap the database
1. Replace `UseSqlServer()` with the new provider in `AddInfrastructureServices()`
2. Update connection string format in `appsettings.json`
3. Re-generate migrations: `dotnet ef migrations add InitialCreate`

---

*Last updated: 2026-02-20 | Version: 1.0.0*
