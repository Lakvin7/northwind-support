# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents, and move them through their lifecycle with strict SLA enforcement and escalation auditing.

Built with **ASP.NET Core (.NET 10) Web API** + **React 19 / TypeScript** + **SQL Server 2022**, in a clean-architecture monorepo.

---

## Technical Assessment Overview

This repository contains the completed technical assessment for **Verdentra's Software Engineering Internship**. All four assessment tasks have been implemented, verified with automated tests, and containerized.

### Assessment Progress Summary

| Task | Title | Time Spent | Status | What Was Implemented & Verified |
|---|---|---|---|---|
| **1** | **Defect Fix & Server-Side Filtering** | 2h 30m | **Completed & Verified** | Fixed DEFECT-117 in `useTicketList.ts`; implemented database-level filtering in `TicketQueries.cs` before counting and paging; added regression test suites. |
| **2** | **Ticket Escalation & Assignment** | 9h | **Completed & Verified** | Automatic triage on creation; configurable SLA policies; SLA status calculation; least-loaded specialist agent assignment; one-step escalation with immutable audit history; frontend escalation panel & SLA badges. |
| **3** | **Login & Authentication** | 2h | **Completed & Verified** | PBKDF2 password hashing on `Agent`; JWT bearer authentication; centralized fallback authorization policy; development credential seeding; protected frontend routes and tab-scoped `sessionStorage`. |
| **4** | **Dockerise the API** | 1h 30m | **Completed & Verified** | Multi-stage .NET 10 Dockerfile with restore layer caching; non-root runtime (`app`, UID 1654); Docker Compose integration; database-backed `/health` endpoint; Vite proxy compatibility. |
| **Total** | **All Tasks 1 — 4** | **15 hours** | **Fully Tested** | **133 backend tests, 15 frontend tests, multi-stage Docker verified (455 MB image, non-root user, /health 200)** |

---

## Task 1 — Defect Fix and Server-Side Filtering

### DEFECT-117: Root Cause & Resolution
- **Root Cause**: In `apps/web/src/features/tickets/hooks/useTicketList.ts`, the `useEffect` hook that triggered the API call only included `[page, pageSize, debouncedSearch]` in its dependency array. State updates from dropdown filters (`status`, `priority`, `categoryId`, etc.) re-rendered the component but never triggered a fetch to `/api/tickets`.
- **Frontend Correction**:
  - Added all filter criteria (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`) to the effect's dependency array.
  - Retained the 300 ms debounce on free-text searches to prevent spamming the backend while ensuring filter dropdowns fire immediately.
  - Implemented request cancellation/race-condition mitigation using an internal sequence counter (`requestId`) so slower, superseded responses are discarded.
  - Verified in commit `ea81f23` and regression tests in `apps/web/src/features/tickets/hooks/useTicketList.test.ts`.

### Server-Side Filtering (`TicketQueries.cs`)
- **Query Architecture**: The API accepted query parameters, but the repository previously returned all tickets.
- **Implementation**:
  - Implemented composable `IQueryable<TicketWithLabels>` filtering in `SupportDesk.Infrastructure/Queries/TicketQueries.cs`.
  - Applied filters: exact match on `Status`, `Priority`, `CategoryId`, `CustomerId`, `AssignedAgentId`, boolean `UnassignedOnly`, and case-insensitive substring search across `Title`, `Reference`, and `Customer.Name`.
  - **Crucial Order of Operations**: All filters are applied **before** `CountAsync()` and pagination (`Skip/Take`). Consequently, `totalCount` and pagination links accurately reflect the filtered subset rather than the entire table.
  - Verified in commit `3523128` with 12 regression tests in `TicketQueriesTests.cs`.

---

## Task 2 — Ticket Escalation, Intelligent Assignment & SLA

### Priority Rules & Forced-Critical Categories
- Ticket creation checks the chosen category: if `Category.ForcesCriticalPriority == true`, the ticket is forced to `Critical` priority regardless of the requested priority.

### Configurable SLA Deadlines & Status
- **Configuration**: Managed via `SlaOptions` and `SlaPolicy` registered and validated at startup (`ValidateOnStart()`):
  - Base priority windows: `Low` (72h), `Medium` (24h), `High` (8h), `Critical` (4h).
  - Premium customer multiplier: `0.5` (halves response window), bounded by a minimum floor (`MinimumWindowHours = 1h`).
  - At-risk threshold: `25%` of total SLA window.
- **Dynamic SLA Status**:
  - `Breached`: Current time (`now`) >= `DueAtUtc` (or resolved after deadline).
  - `AtRisk`: Unresolved, before deadline, and remaining time <= 25% of the total SLA window.
  - `Met`: Ticket resolved or closed before `DueAtUtc`.
  - `WithinSla`: Unresolved, before deadline, and outside the at-risk threshold.

### Intelligent Automatic Assignment
- Handled by `AgentAssignmentService`:
  - **Specialist Check**: If `Category.RequiresSpecialist == true`, only specialist agents are eligible.
  - **Availability**: Agents must be active (`IsActive == true`) and have strictly fewer open tickets than `MaxOpenTickets`.
  - **Load Balancing**: Selects the eligible agent with the fewest open tickets.
  - **Deterministic Tie-Breaking**: If multiple agents tie for fewest open tickets, the agent with the lowest numerical `Id` is selected.
  - **Unassigned Fallback**: If no agent is eligible, the ticket remains unassigned and returns an explicit triage explanation in the API response.

### Escalation Workflow & Immutable History
- **State Machine**:
  - Priority steps up exactly one tier: `Low` -> `Medium` -> `High` -> `Critical`.
  - `Critical` tickets cannot be escalated (rejected with HTTP 409 Conflict).
  - Escalation reason is mandatory (minimum 10 characters).
  - Deadline is recalculated from `now` using the new priority window and customer tier.
  - Assignee is re-evaluated: current assignee is retained if still eligible and under capacity; reassigned to the least-loaded eligible agent otherwise.
- **Audit History**: Every escalation appends an immutable record to `TicketEscalations` capturing previous/new priority, previous/new assignee, previous/new due date, reason, escalated timestamp, and actor ID.
- **Frontend Panel**: `EscalationPanel` component on the ticket detail page provides real-time client validation, pending submission state, and server error handling (including 409 rejections).

---

## Task 3 — Login and Authentication

### Authentication Architecture
- **Credentials**: Added `PasswordHash` column to the `Agents` table via EF Core migration `20261021094627_AddAgentAuthentication`.
- **Password Hashing**: Built using ASP.NET Core's `PasswordHasher<Agent>` (`Microsoft.AspNetCore.Identity.Core`), implementing PBKDF2 with HMAC-SHA512 and cryptographic per-user salts. Plain text or reversible encryption is never used.
- **Central Fallback Authorization**:
  - Configured `AuthorizationOptions.FallbackPolicy = RequireAuthenticatedUser()`.
  - All endpoints are closed by default. Public endpoints (`POST /api/auth/login`, `GET /health`, Swagger) are explicitly permitted via `[AllowAnonymous]` or `.AllowAnonymous()`.
- **JWT Access Tokens**:
  - Issued by `JwtAccessTokenService` with HMAC-SHA256 signing (`Jwt:SigningKey`).
  - Payload claims: `sub` (Agent ID), `name` (Full Name), `email`, `jti`.
  - Lifetime: 60 minutes (`Jwt:LifetimeMinutes`).
- **Escalation Security**: The escalation command handler extracts the authenticated agent ID directly from the JWT `sub` claim via `ICurrentAgent` (`HttpCurrentAgent`), preventing client-side actor spoofing.

### Development Credentials
- **Email**: `max@northwind.local` (and legacy agent `alex.turner@northwind-support.example`)
- **Password**: Provided at runtime through the environment variable `DevelopmentCredentials__AgentPassword`.
- **Reviewer Note**: For security, no actual password is committed to this repository. Reviewers can find the development password supplied in the assessment submission email or set `DevelopmentCredentials__AgentPassword=<development-password>` in `.env`.

### Frontend Session Management
- Tokens are stored in `sessionStorage` (isolated per browser tab, automatically cleared on tab closure).
- Axios client interceptor attaches `Authorization: Bearer <token>` to all outgoing calls and intercepts HTTP 401 responses to purge session storage and redirect to `/login`.
- Protected routes redirect unauthenticated users to `/login` and return them to their destination upon successful sign-in.

---

## Task 4 — Dockerise the API

### Docker Container & Compose Architecture
- **Multi-Stage Build (`apps/api/Dockerfile`)**:
  - Build stage using official `mcr.microsoft.com/dotnet/sdk:10.0`. Copies `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and project `.csproj` files first, then executes `dotnet restore`. Application source files (`apps/api/src/`) are copied afterwards.
  - **Restore Caching**: C# source file changes do not invalidate the NuGet package restore layer, speeding up rebuilds from ~40s to ~2s.
  - Final runtime stage using `mcr.microsoft.com/dotnet/aspnet:10.0`.
- **Security & Privileges**:
  - Runs as the official non-root `app` user (`USER $APP_UID`, UID 1654).
  - Binds to HTTP port `8080` via `ASPNETCORE_HTTP_PORTS=8080`. No HTTPS certificates or private keys are baked into the image.
- **Docker Compose Integration (`docker-compose.yml`)**:
  - Defines `api` service with build context `.` and Dockerfile `apps/api/Dockerfile`.
  - Exposes container port `8080` on host `localhost:5080`.
  - Depends on `sqlserver` with `condition: service_healthy`.
  - Connects to SQL Server using internal service DNS `Server=sqlserver,1433`.
  - Configures runtime environment variables (`ConnectionStrings__Default`, `Jwt__SigningKey`, `DevelopmentCredentials__AgentPassword`).
  - Sets project name `name: northwind-support-main` to preserve existing database containers and data volumes.
- **Database Health Check (`GET /health`)**:
  - Implemented with `DatabaseHealthCheck` testing `SupportDbContext.Database.CanConnectAsync()`.
  - Open anonymously without authentication. Returns HTTP 200 `Healthy` when SQL Server is reachable, and HTTP 503 `Unhealthy` when disconnected.
- **Image Metrics**:
  - Uncompressed image size: **455 MB** (compressed download size: ~125 MB), compared to the ~3.5 GB SDK image.

---

## Quick Start & Running the Application

### Prerequisites
- **.NET SDK 10** (`global.json` specifies `10.0.100`)
- **Node.js 22** (`.nvmrc`; 20.19+ supported)
- **Docker & Docker Compose**

### Option A: Run Everything via Docker Compose (Recommended)

1. **Configure Environment** (Optional):
   ```bash
   cp .env.example .env
   # Edit DevelopmentCredentials__AgentPassword in .env if desired
   ```

2. **Build and Start Containers**:
   ```bash
   docker compose up --build -d
   ```
   This brings up SQL Server 2022 and the containerized .NET 10 API.

3. **Verify Health**:
   ```bash
   curl http://localhost:5080/health
   # Expected: Healthy
   ```

4. **Start the Frontend Web App**:
   ```powershell
   $env:VITE_API_PROXY_TARGET = "http://localhost:5080"
   npm install
   npm run web
   ```
   Open **http://localhost:5173** in your browser.

---

### Option B: Local Development (Bare-Metal .NET)

1. **Start SQL Server**:
   ```bash
   npm run db:up
   ```

2. **Run the API**:
   ```bash
   npm run api
   ```
   In Development, the API automatically runs migrations and seeds data.

3. **Run the React Frontend**:
   ```bash
   npm run web
   ```

| Service | Local URL | Container URL |
|---|---|---|
| **Web App** | http://localhost:5173 | N/A (runs on host) |
| **API** | https://localhost:7043 (HTTP: 5043 / 5081) | http://localhost:5080 |
| **Health Check** | https://localhost:7043/health | http://localhost:5080/health |
| **Swagger UI** | https://localhost:7043/swagger | http://localhost:5080/swagger |

---

## Automated Testing

```bash
# Run all tests (backend + frontend)
npm test

# Run backend tests only
dotnet test

# Run frontend tests only
npm run test --workspace apps/web
```

### Test Suite Summary
- **Backend Architecture Tests (4 passed)**: Enforce Clean Architecture layer isolation.
- **Backend Unit & Integration Tests (129 passed)**:
  - SLA calculation, multiplier, and minimum window floor tests.
  - Boundary tests for `WithinSla`, `AtRisk`, `Breached`, and `Met`.
  - Agent assignment, specialist filtering, capacity limits, and tie-breaking.
  - Escalation state transitions and history logging.
  - PBKDF2 password hashing verification.
  - JWT token generation, lifetime, and claim validation.
  - Health check endpoint routing, status codes, and DB reachability.
- **Frontend Vitest Tests (15 passed)**:
  - Ticket table rendering and SLA status badge rendering.
  - Escalation panel form validation, pending states, and 409 conflict handling.
  - Login page validation, credential rejection feedback, and session storage management.

---

## Design Decisions & Engineering Trade-Offs

1. **Layer Separation & Domain Integrity**:
   - Business invariants (e.g. priority escalation step rules, maximum open tickets, and SLA deadline calculations) reside in domain aggregates and application policies. Controllers and query classes remain lean orchestrators.
2. **Server-Side Filtering via `IQueryable`**:
   - Filtering is translated to SQL expressions by EF Core before executing `CountAsync()` and `Skip/Take`. In-memory post-filtering was rejected because it causes incorrect total page counts and fails to scale to tens of thousands of tickets.
3. **Database Date Math Compatibility (SQL Server vs. SQLite)**:
   - EF Core's `EF.Functions.DateDiffSecond` works seamlessly on SQL Server but is unsupported by SQLite. In `TicketQueries.cs`, provider inspection (`db.Database.IsSqlServer()`) dynamically switches between T-SQL `DateDiffSecond` and provider-compatible `Ticks` arithmetic. This allows SQL-level filtering in production while maintaining fast, dependency-free in-memory SQLite unit tests.
4. **Credential Storage on `Agent` Aggregate**:
   - Adding `PasswordHash` to `Agent` was chosen over full ASP.NET Core Identity. Full Identity introduces 7+ tables and heavy boilerplate, which is disproportionate for an internal support desk with an established agent aggregate.
5. **Centralized Authorization vs. Attributes**:
   - A global `FallbackPolicy` was chosen over per-controller `[Authorize]` attributes. This provides security-by-default: new endpoints are protected automatically unless explicitly granted `[AllowAnonymous]`.
6. **Token Storage in `sessionStorage`**:
   - `sessionStorage` was selected over `localStorage` because it automatically purges tokens on tab close and restricts token exposure to that specific tab session. While HttpOnly cookies provide higher XSS immunity, `sessionStorage` with bearer tokens fits SPA APIs cleanly without CSRF vulnerability.
7. **Multi-Stage Non-Root Docker Image**:
   - Compiling inside the SDK image and copying only published binaries to `mcr.microsoft.com/dotnet/aspnet:10.0` produced a lightweight (455 MB) image. Running as `app` (UID 1654) enforces least privilege and container security best practices.

---

## AI Collaboration & Methodology

AI assistance (ChatGPT and Google Antigravity) was used throughout the assessment under engineering supervision:
- **Planning & Architecture**: Clarified requirements, evaluated clean-architecture seams, and outlined atomic test cases.
- **Defect Investigation**: Assisted in diagnosing the missing dependency array values in `useTicketList.ts` (DEFECT-117).
- **Targeted Problem Solving**: When SQLite threw translation errors during query tests for SLA date math, AI helped devise the provider-switching approach in `TicketQueries.cs` using `db.Database.IsSqlServer()`.
- **Review & Verification**: All AI-suggested code was verified with unit tests, type checks, lint checks, Docker container executions, and manual API calls before merging. No generated code was committed unreviewed.

---

## Known Limitations

- **No Refresh Token Flow**: Access tokens expire after 60 minutes, requiring the agent to re-authenticate. A production system would implement sliding-window refresh tokens in HttpOnly cookies.
- **No Login Rate Limiting / Lockout**: The login endpoint currently does not enforce rate limiting or account lockout after repeated failed attempts.
- **Transitive Package Warnings**: The project reports known NuGet vulnerability warnings for `System.Security.Cryptography.Pkcs` (9.0.11), inherited transitively from framework dependencies. These were left untouched to preserve solution dependency constraints.
- **Production Migration Strategy**: In Development, the API runs `MigrateAndSeedAsync()` at startup. In enterprise production, schema migrations should be decoupled from application startup and executed via EF Core migration bundles or CI/CD deployment pipelines.
