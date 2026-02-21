# BPO Platform – Azure Services Catalogue

> **Purpose:** Complete reference of every Azure service and API required to run the BPO AI Process Discovery Platform. Each entry includes the Azure portal name, Bicep resource type, the `appsettings.json` key used in code, the local-development fallback, and the Managed Identity role assignment required.

---

## Table of Contents

1. [Identity & Access Management](#1-identity--access-management)
2. [Compute & Hosting](#2-compute--hosting)
3. [Data & Storage](#3-data--storage)
4. [AI & Cognitive Services](#4-ai--cognitive-services)
5. [Integration & Messaging](#5-integration--messaging)
6. [DevOps & Deployment](#6-devops--deployment)
7. [Networking & Security](#7-networking--security)
8. [Monitoring & Observability](#8-monitoring--observability)
9. [Quick-Provision Checklist](#9-quick-provision-checklist)
10. [Managed Identity Role Assignments](#10-managed-identity-role-assignments)
11. [Cost Optimisation Tips](#11-cost-optimisation-tips)
12. [Local Development Fallbacks](#12-local-development-fallbacks)

---

## 1. Identity & Access Management

### 1.1 Microsoft Entra ID (Azure Active Directory)

| Field | Value |
|-------|-------|
| **Portal name** | Microsoft Entra ID |
| **Bicep resource** | *(tenant-level, not deployed via Bicep)* |
| **SDK / package** | `Microsoft.Identity.Web` 3.8.2, `MSAL.js` v3 |
| **`appsettings` key** | `AzureAd:TenantId`, `AzureAd:ClientId`, `AzureAd:Domain` |
| **Used for** | JWT bearer authentication on all API endpoints; MSAL login/logout in frontend |
| **Dev fallback** | `DevPermissivePolicyProvider` bypasses auth entirely in Development environment |
| **Setup steps** | 1. Register an App Registration (API + SPA). 2. Expose `BPOPlatform.Read` / `BPOPlatform.Write` API scopes. 3. Grant client application permission to those scopes. |

**Portal path:** `portal.azure.com → Microsoft Entra ID → App registrations`

---

### 1.2 Azure Key Vault

| Field | Value |
|-------|-------|
| **Portal name** | Key Vault |
| **Bicep resource** | `Microsoft.KeyVault/vaults@2023-07-01` |
| **SKU** | Standard (dev/prod) |
| **`appsettings` key** | *(secrets are loaded at startup via `AddAzureKeyVault()`; no static key)* |
| **Used for** | Storing SQL admin password, Speech Services key, any secret that must not appear in source code |
| **Managed Identity role** | `Key Vault Secrets User` on the vault → API App + Functions App managed identities |
| **Dev fallback** | `appsettings.Development.json` values (never commit real secrets) |

---

## 2. Compute & Hosting

### 2.1 Azure App Service (Linux)

| Field | Value |
|-------|-------|
| **Portal name** | App Service |
| **Bicep resource** | `Microsoft.Web/serverfarms@2023-12-01` (plan) + `Microsoft.Web/sites@2023-12-01` (app) |
| **SKU** | B1 (dev/test) → P2v3 (production) |
| **Runtime** | `DOTNETCORE\|8.0` (Linux) |
| **Used for** | Hosts `BPOPlatform.Api` — all REST endpoints, SignalR hub, Swagger UI |
| **Managed Identity** | System-assigned; grants access to Blob, OpenAI, SQL, Key Vault |
| **`appsettings` key** | `APPLICATIONINSIGHTS_CONNECTION_STRING`, `AzureStorage__ServiceUri`, `AzureOpenAI__Endpoint` (set as App Settings in portal / Bicep) |
| **Dev fallback** | `dotnet run` on `BPOPlatform.Api` locally |

---

### 2.2 Azure App Service – Web Frontend

| Field | Value |
|-------|-------|
| **Portal name** | Azure Static Web Apps *(recommended)* **or** App Service (Linux) serving static files |
| **Bicep resource** | `Microsoft.Web/staticSites@2023-12-01` (Static Web Apps) |
| **SKU** | Free / Standard |
| **Used for** | Serves `BPOPlatform.Web` – HTML/CSS/JS frontend pages |
| **Dev fallback** | `dotnet run` on `BPOPlatform.Web` at `http://localhost:5500` |

> **Recommendation:** Use **Azure Static Web Apps** for zero-infrastructure frontend hosting with built-in GitHub Actions deployment. Costs ~$0/month on Free tier.

---

### 2.3 Azure Functions (v4 Isolated Worker)

| Field | Value |
|-------|-------|
| **Portal name** | Function App |
| **Bicep resource** | `Microsoft.Web/sites@2023-12-01` (kind: `functionapp,linux`) |
| **SKU** | Consumption plan (dev) → Premium EP1 (production for Durable) |
| **Runtime** | `DOTNET-ISOLATED\|8.0` |
| **Used for** | `ArtifactAnalysisTrigger` (Blob trigger → AI pipeline), `ProcessOrchestrationTrigger` (HTTP → Durable), `ProcessOrchestrator` (activity sequencing) |
| **Extensions needed** | `AzureWebJobsStorage`, Durable Task extension (`Microsoft.Azure.Functions.Worker.Extensions.DurableTask`) |
| **`appsettings` key** | `AzureWebJobsStorage__accountName`, `AzureStorage__ServiceUri`, `AzureOpenAI__Endpoint` |
| **Dev fallback** | `func start` with local `local.settings.json` |

> **Note:** Durable Functions require Azure Storage (below) for its orchestration state store. Use `UseDevelopmentStorageEmulator=true` locally (Azurite).

---

## 3. Data & Storage

### 3.1 Azure SQL Database

| Field | Value |
|-------|-------|
| **Portal name** | Azure SQL Database |
| **Bicep resource** | `Microsoft.Sql/servers@2023-08-01-preview` + `Microsoft.Sql/servers/databases@2023-08-01-preview` |
| **SKU** | S1 Standard (dev/test) → General Purpose 4 vCores (production) |
| **Database name** | `BPOPlatformDB` |
| **Authentication** | Managed Identity (`Authentication=Active Directory Managed Identity`) — no password in connection string |
| **`appsettings` key** | `ConnectionStrings:DefaultConnection` |
| **Tables** | `Processes`, `ProcessArtifacts`, `WorkflowSteps`, `KanbanCards` (auto-created by EF Core migrations) |
| **Dev fallback** | SQLite (`DataSource=bpo-platform-dev.db`) via `appsettings.Development.json` |

**Alternative:** Replace `UseSqlServer()` with `UseNpgsql()` to use **Azure Database for PostgreSQL Flexible Server** — no domain code changes required.

---

### 3.2 Azure Blob Storage

| Field | Value |
|-------|-------|
| **Portal name** | Storage account (Blob Service) |
| **Bicep resource** | `Microsoft.Storage/storageAccounts@2023-05-01` |
| **SKU** | Standard LRS |
| **Container** | `process-artifacts` (private access) |
| **`appsettings` key** | `AzureStorage:ServiceUri`, `AzureStorage:ArtifactsContainerName` |
| **Used for** | Storing uploaded process documents (PDF, DOCX, MP3, WAV, M4A, images); trigger source for Azure Functions |
| **Managed Identity role** | `Storage Blob Data Contributor` → API App + Functions App |
| **Dev fallback** | **Azurite** emulator (`docker run mcr.microsoft.com/azure-storage/azurite`) or `LocalBlobStorageService` (saves to OS temp dir) |

> **Also required by:** Azure Functions (`AzureWebJobsStorage`) and Durable Functions state store.

---

## 4. AI & Cognitive Services

### 4.1 Azure OpenAI Service

| Field | Value |
|-------|-------|
| **Portal name** | Azure OpenAI |
| **Bicep resource** | `Microsoft.CognitiveServices/accounts@2024-10-01` (kind: `OpenAI`) |
| **Model deployed** | `gpt-4o` (`2024-08-06`), Standard SKU, 10K TPM capacity |
| **SDK / package** | `Azure.AI.OpenAI` 2.1 |
| **`appsettings` key** | `AzureOpenAI:Endpoint`, `AzureOpenAI:DeploymentName` |
| **Managed Identity role** | `Cognitive Services OpenAI User` → API App + Functions App |
| **Dev fallback** | `MockAiAnalysisService` (hardcoded scores) + `MarkdownDocumentGenerationService` (template-based) + `MockIntakeChatService` (guided chat) |
| **Regions** | Not available in all regions — check [availability](https://learn.microsoft.com/azure/ai-services/openai/concepts/models#standard-models-by-endpoint) |

#### Azure OpenAI API calls made by this platform

The following table lists **every place** the platform calls Azure OpenAI, the system prompt type, and the estimated token budget per call:

| Feature | File | API endpoint | System prompt type | Est. tokens / call |
|---------|------|-------------|-------------------|-------------------|
| **Process AI Analysis** (automation + compliance scoring) | `AzureOpenAiAnalysisService.cs` | `chat/completions` | JSON structured output — scores 0–100 + insights array | ~1 000 in / 500 out |
| **Process Document Generation** (Markdown / HTML / DOCX report) | `OpenAiDocumentGenerationService.cs` | `chat/completions` | Long-form report generation from process metadata | ~800 in / 2 000 out |
| **Intake Guided Chat** (meta-field collection) | `AzureIntakeChatService.SendMessageAsync` | `chat/completions` | Step-by-step field extraction; returns JSON + assistant message | ~600 in / 300 out per turn |
| **Intake AI Analysis** (brief + checkpoints + actionables) | `AzureIntakeChatService.AnalyseIntakeAsync` | `chat/completions` | Structured JSON: brief string + checkpoints array + actionables array | ~1 200 in / 800 out |

> **One deployment is sufficient** — all four features share the single `gpt-4o` deployment configured in `AzureOpenAI:DeploymentName`. No separate deployments are needed per feature.

#### Minimum required Azure OpenAI setup

```
1. Create Azure OpenAI resource  (portal → AI Services → Azure OpenAI)
2. Deploy model:  gpt-4o  (2024-08-06),  Standard,  ≥ 10K TPM
3. Copy Endpoint URL  →  appsettings.json  AzureOpenAI:Endpoint
4. Assign role:  Cognitive Services OpenAI User  →  API App managed identity
```

No Azure OpenAI API key is needed in production — Managed Identity handles authentication. In local development, the platform automatically falls back to mock services so **no Azure OpenAI account is required to run locally**.

---

### 4.2 Azure AI Document Intelligence

| Field | Value |
|-------|-------|
| **Portal name** | AI services → Document Intelligence |
| **Bicep resource** | `Microsoft.CognitiveServices/accounts` (kind: `FormRecognizer`) |
| **API** | `prebuilt-read` model |
| **SDK / package** | `Azure.AI.DocumentIntelligence` 1.0.0 |
| **`appsettings` key** | `DocumentIntelligence:Endpoint` |
| **Used for** | Extracting text from uploaded PDF and image process artifacts (invoices, forms, SOPs) |
| **Managed Identity role** | `Cognitive Services User` → API App + Functions App |
| **Dev fallback** | `MockDocumentIntelligenceService` (returns placeholder extracted text) |

---

### 4.3 Azure AI Speech Services

| Field | Value |
|-------|-------|
| **Portal name** | AI services → Speech |
| **Bicep resource** | `Microsoft.CognitiveServices/accounts` (kind: `SpeechServices`) |
| **API** | Speech-to-Text REST API v1 (`/speech/recognition/conversation/cognitiveservices/v1`) |
| **`appsettings` key** | `SpeechServices:Endpoint`, `SpeechServices:Region`, `SpeechServices:SubscriptionKey` |
| **Used for** | Transcribing uploaded audio recordings (MP3, WAV, M4A, OGG) of process walkthroughs and interviews |
| **Secret handling** | `SubscriptionKey` stored in **Azure Key Vault** in production; injected at startup |
| **Dev fallback** | `MockSpeechTranscriptionService` (returns realistic interview-style transcript) |
| **Supported formats** | `audio/mpeg` (MP3), `audio/wav` (WAV), `audio/mp4` (M4A), `audio/ogg` (OGG) |

---

## 5. Integration & Messaging

### 5.1 Azure SignalR Service *(optional, recommended for production)*

| Field | Value |
|-------|-------|
| **Portal name** | SignalR Service |
| **Bicep resource** | `Microsoft.SignalRService/signalR@2023-08-01-preview` |
| **SKU** | Free (dev) → Standard S1 (production) |
| **`appsettings` key** | `Azure:SignalR:ConnectionString` (injected via `builder.Services.AddSignalR().AddAzureSignalR()`) |
| **Used for** | Scaling `NotificationHub` beyond a single App Service instance; real-time toast notifications (`ProcessStatusChanged`, `ArtifactUploaded`, `AiAnalysisComplete`) |
| **Dev fallback** | Local SignalR hub running inside `BPOPlatform.Api` (no Azure SignalR needed for single-instance dev) |

> **Note:** Without Azure SignalR Service the hub works fine on a single App Service instance. Add it when you need multiple instances or slot-based deployments.

---

### 5.2 Power Automate (HTTP Trigger Flow)

| Field | Value |
|-------|-------|
| **Portal name** | Power Automate (`make.powerautomate.com`) |
| **Type** | Cloud Flow with HTTP Request trigger |
| **`appsettings` key** | `PowerAutomate:FlowUrl` |
| **Used for** | Creating external tickets in ServiceNow, Jira, Azure DevOps, or any system reachable by Power Automate connectors |
| **Dev fallback** | `NoOpTicketingService` (returns a GUID ticket ID without calling any external system) |
| **Setup steps** | 1. Create a new Cloud Flow. 2. Add "When an HTTP request is received" trigger. 3. Copy the generated URL into `appsettings.json` / Key Vault. |

---

## 6. DevOps & Deployment

### 6.1 Azure DevOps / GitHub Actions

| Field | Value |
|-------|-------|
| **Portal name** | GitHub Actions (`.github/workflows/ci-cd.yml`) |
| **Authentication** | OIDC Workload Identity Federation — no stored client secrets |
| **Used for** | Build → Test → Security scan → Bicep infra deploy → API deploy → Functions deploy |
| **Required GitHub secrets** | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_API_APP_NAME`, `AZURE_FUNCTIONS_APP_NAME`, `SQL_ADMIN_PASSWORD` |

---

### 6.2 Azure Container Registry *(optional)*

| Field | Value |
|-------|-------|
| **Portal name** | Container Registry |
| **Bicep resource** | `Microsoft.ContainerRegistry/registries` |
| **SKU** | Basic (dev) → Standard (prod) |
| **Used for** | Storing Docker images if moving from App Service (code deploy) to container-based deployment (AKS, Container Apps) |
| **Dev fallback** | Direct `dotnet publish` code deploy (current default) |

---

## 7. Networking & Security

### 7.1 Azure Front Door + WAF *(Phase 5 / Production)*

| Field | Value |
|-------|-------|
| **Portal name** | Front Door and CDN profiles |
| **Bicep resource** | `Microsoft.Cdn/profiles@2023-05-01` (kind: `Premium_AzureFrontDoor`) |
| **Used for** | Global load balancing, DDoS protection, WAF rules (OWASP CRS 3.2), geo-filtering, CDN caching for static assets |
| **Dev fallback** | Not needed locally |

---

### 7.2 Azure Virtual Network + Private Endpoints *(Phase 5 / Production)*

| Field | Value |
|-------|-------|
| **Portal name** | Virtual Networks, Private Endpoints |
| **Bicep resources** | `Microsoft.Network/virtualNetworks`, `Microsoft.Network/privateEndpoints` |
| **Used for** | Private network connectivity to Azure SQL and Blob Storage; removes public internet exposure of data services |
| **Dev fallback** | Public endpoints with IP firewall rules (current dev configuration) |

---

## 8. Monitoring & Observability

### 8.1 Azure Application Insights

| Field | Value |
|-------|-------|
| **Portal name** | Application Insights |
| **Bicep resource** | `Microsoft.Insights/components@2020-02-02` |
| **`appsettings` key** | `ApplicationInsights:ConnectionString` (also `APPLICATIONINSIGHTS_CONNECTION_STRING` App Setting) |
| **Used for** | Distributed tracing, request/dependency telemetry, exception tracking, custom KPI events (`TelemetryClient`) |
| **Serilog sink** | `Serilog.Sinks.ApplicationInsights` — structured logs forwarded automatically |
| **Dev fallback** | Console/file Serilog sinks (no Application Insights connection string needed locally) |

---

### 8.2 Azure Log Analytics Workspace

| Field | Value |
|-------|-------|
| **Portal name** | Log Analytics workspaces |
| **Bicep resource** | `Microsoft.OperationalInsights/workspaces@2023-09-01` |
| **SKU** | PerGB2018 |
| **Used for** | Backing store for Application Insights; Azure Monitor alerts; cross-service log queries (KQL) |
| **Retention** | 30 days (configurable) |

---

## 9. Quick-Provision Checklist

Use this checklist when setting up a new environment (dev / test / prod):

### Identity
- [ ] Create **App Registration** in Entra ID; note `TenantId`, `ClientId`
- [ ] Expose API scopes (`BPOPlatform.Read`, `BPOPlatform.Write`)
- [ ] Grant SPA client permission to those scopes
- [ ] (Optional) Create **App Role** definitions (`Admin`, `Manager`, `Analyst`, `Viewer`)

### Core Infrastructure (Bicep)
```bash
az group create --name bpo-<env>-rg --location eastus
az deployment group create \
  --resource-group bpo-<env>-rg \
  --template-file infra/main.bicep \
  --parameters @infra/main.parameters.json \
  --parameters environment=<env> sqlAdminPassword="<secret>"
```
This single command provisions:
- [ ] **Log Analytics Workspace** (`bpo-<env>-logs`)
- [ ] **Application Insights** (`bpo-<env>-ai`)
- [ ] **Storage Account** + `process-artifacts` container
- [ ] **Azure SQL Server** + `BPOPlatformDB` database
- [ ] **Azure OpenAI** account + `gpt-4o` deployment
- [ ] **Key Vault** (RBAC-enabled, soft-delete 7 days)
- [ ] **App Service Plan** (B1 dev / P2v3 prod, Linux)
- [ ] **API App Service** with System-assigned Managed Identity
- [ ] **Function App** with System-assigned Managed Identity
- [ ] **RBAC role assignments** for both managed identities → Storage Blob Data Contributor

### Services to Provision Manually (after Bicep)
- [ ] **Document Intelligence** — create via portal (`AI services → Document Intelligence`); copy endpoint to Key Vault / appsettings
- [ ] **Speech Services** — create via portal (`AI services → Speech`); store `SubscriptionKey` in Key Vault
- [ ] **Power Automate Flow** — create at `make.powerautomate.com`; copy HTTP trigger URL to Key Vault
- [ ] **Azure SignalR Service** (production only) — create via portal; add connection string to App Service config
- [ ] **Static Web Apps** (frontend) — link to GitHub repo branch for automatic deploys

### CI/CD
- [ ] Create **Federated Identity Credential** (OIDC) on the App Registration used by GitHub Actions
- [ ] Set all **GitHub Secrets** listed in [Section 6.1](#61-azure-devops--github-actions)
- [ ] Run pipeline to validate end-to-end deployment

---

## 10. Managed Identity Role Assignments

All production secrets are accessed via **Managed Identity** — no passwords or keys in `appsettings.json`.

| Resource | Role | Assigned to |
|----------|------|-------------|
| Storage Account | `Storage Blob Data Contributor` | API App + Functions App |
| Azure OpenAI | `Cognitive Services OpenAI User` | API App + Functions App |
| Azure SQL | `db_datareader` + `db_datawriter` (SQL RBAC) | API App managed identity (via `CREATE USER ... FROM EXTERNAL PROVIDER` in SQL) |
| Key Vault | `Key Vault Secrets User` | API App + Functions App |
| Document Intelligence | `Cognitive Services User` | API App + Functions App |
| Application Insights | *(no role needed — connection string only)* | — |
| Azure SignalR | `SignalR App Server` | API App |

> **Bicep tip:** Add `Microsoft.Authorization/roleAssignments` resources for each row above. See `infra/main.bicep` for the Storage example pattern.

---

## 11. Cost Optimisation Tips

| Service | Dev/Test saving | Production consideration |
|---------|----------------|--------------------------|
| **App Service** | Use **Free (F1)** or **Basic (B1)** — no always-on | Scale to P2v3 only when needed; use autoscale |
| **Azure SQL** | Use **Serverless** tier (auto-pause after 1h idle) | Move to General Purpose DTU/vCore when load is predictable |
| **Azure OpenAI** | Use `gpt-4o-mini` deployment for dev (lower TPM cost) | Monitor token usage via Application Insights |
| **Functions** | **Consumption plan** has 1M free executions/month | Switch to **Flex Consumption** or **Premium** only if cold start latency matters |
| **Blob Storage** | **LRS** redundancy is sufficient for dev artifacts | Use **ZRS** or **GRS** for production business data |
| **Document Intelligence** | Free tier: 500 pages/month | Standard S0: pay-per-page; use page-range extraction to reduce costs |
| **Speech Services** | Free tier: 5h audio/month | Standard: $1/hour; batch transcription is cheaper than real-time for long audio |
| **Azure SignalR** | **Free tier**: 20 connections, 20K messages/day | Standard S1: $49/month per 1K connections |
| **Log Analytics** | Set **30-day retention** and daily **data cap** | Move cold logs to Storage Account with archival tier |

---

## 12. Local Development Fallbacks

No Azure subscription is required to run the platform locally. All Azure services have a built-in fallback:

| Azure Service | Local Fallback | How to activate |
|---------------|---------------|-----------------|
| Azure SQL | **SQLite** (`bpo-platform-dev.db`) | Set `ConnectionStrings:DefaultConnection` to `DataSource=bpo-platform-dev.db` in `appsettings.Development.json` |
| Azure Blob Storage | **LocalBlobStorageService** (OS temp dir) or **Azurite** | Leave `AzureStorage:ServiceUri` blank, or set `ConnectionStrings:BlobStorage` to Azurite connection string |
| Azure OpenAI | **MockAiAnalysisService** (hardcoded scores) + **MockIntakeChatService** (guided chat) + **MarkdownDocumentGenerationService** (template doc) | Leave `AzureOpenAI:Endpoint` blank or set to placeholder |
| Document Intelligence | **MockDocumentIntelligenceService** | Leave `DocumentIntelligence:Endpoint` blank |
| Speech Services | **MockSpeechTranscriptionService** | Leave `SpeechServices:SubscriptionKey` blank |
| Document Generation | **MarkdownDocumentGenerationService** (template) | Automatic when OpenAI not configured |
| External Ticketing | **NoOpTicketingService** (returns GUID) | Leave `PowerAutomate:FlowUrl` blank |
| Authentication | **DevPermissivePolicyProvider** (all requests allowed) | Automatic in `Development` environment |
| SignalR | In-process hub (single instance) | No config needed — Azure SignalR only used in production |
| Application Insights | Console + file Serilog sinks | Leave `ApplicationInsights:ConnectionString` blank |

```bash
# Minimal local run (no Azure needed)
cd src/BPOPlatform.Api
dotnet run
# → http://localhost:5232/swagger  (all endpoints work with mock services)

cd src/BPOPlatform.Web
dotnet run
# → http://localhost:5500          (serves frontend, redirects to login.html)
```

---

*Last updated: 2026-02-20 | Covers Phases 1–4*
