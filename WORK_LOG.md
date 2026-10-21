
# Verdentra Assessment - Work Log

## Step 0 - Setup & Exploration
**Status:** Completed
**Date:** 20 October 2026  
**Time spent:** 1 hour

- Set up GitHub repository, VS Code, Docker, SQL Server, .NET API and React frontend.
- Verified the application and Swagger API.
- Ran baseline tests: **32 passed, 0 failed**.
- Explored ticket domain, application handlers and database queries.
- Identified existing frontend filtering defect and missing backend filtering.
- Observed existing NuGet dependency security warnings.

## Task 1 — Bug Fix & Filtering

**Time spent:** 2 hours 30 minutes (Task 1.1: ~1h 15m; Task 1.2: 2h)  

## Task 1.1 — Frontend Filtering Defect (DEFECT-117)
**Time spent:** ~ 1hour 15minutes
**Branch:** `task-1.1-frontend-defect`

### Problem identified
The ticket list did not automatically reload when filters such as status or priority changed. The data-fetching effect only tracked search, page, and page size.

### Changes implemented
- Updated `useTicketList.ts` so changes to the supported ticket filters trigger a new API request.
- Preserved the 300 ms search debounce.
- Added request ID tracking to prevent older API responses from overwriting newer results.
- Added regression tests covering status changes and stale API responses.
- Updated the existing search test to account for asynchronous request scheduling.

### Verification
- Frontend tests: **4 passed**
- Frontend lint: **Passed**
- Frontend production build: **Passed**
- Manual browser check: Changing the status filter triggered a new API request with the selected status and returned HTTP 200.

### Important observation
The backend does not yet apply the requested ticket filters. This is expected to be addressed in Task 1.2.


### Notes
AI assistance was used to help investigate the defect, implement changes, and develop tests. The implementation and test results were reviewed during development.


### Task 1.2 — Server-Side Ticket Filtering
 
**Time spent:** 2hours
**Branch:** `task-1.2-backend-filtering`  

#### Problem identified

The API accepted ticket filtering parameters but did not apply them to its database query. As a result, the returned tickets and pagination counts did not reflect the requested filters.

#### Changes implemented

- Implemented server-side filtering in `TicketQueries.cs`.
- Added exact-match filters for:
  - Status
  - Priority
  - Category ID
  - Customer ID
  - Assigned agent ID
- Implemented `unassignedOnly` filtering.
- Implemented case-insensitive search across ticket title, ticket reference and customer name.
- Trimmed search input and ignored blank searches.
- Combined supplied filters using AND conditions.
- Applied filters before database counting, sorting and pagination.
- Preserved the existing sorting and paging behaviour.
- Kept the filtering logic modular to support adding the Task 2 `slaStatus` filter.

#### Regression tests

Added 12 test cases in `TicketQueriesTests.cs`, covering:

- Exact-match filters
- Assigned and unassigned tickets
- Case-insensitive search
- Whitespace trimming
- Combined filters
- Filtered pagination and total counts

Tests use an in-memory SQLite database to exercise EF Core query execution.

#### Verification

- Infrastructure project build: **Passed**
- New filtering tests: **12 passed**
- Complete backend unit test suite: **38 passed, 0 failed, 0 skipped**
- Reviewed the staged changes before committing.
- Published the feature branch and merged it into `main`.
- Pushed the updated `main` branch to GitHub.

#### Design decision

Filtering is implemented through composable `IQueryable` expressions before counting and paging, allowing EF Core to translate the filtering into SQL instead of filtering loaded results in application memory.

#### Note

AI assistance was used to plan the query changes, prepare tests and review the implementation. The changes were checked through builds, automated tests and Git diff inspection.

## Work Summary & Time Spent

### Overall Task Summary

| Task | Scope | Time spent | Verification | Status |
|---|---|---|---|---|
| **Step 0** | Setup & Exploration | 1 hour | Baseline tests (32 passed), Swagger, Docker | Completed |
| **Task 1** | Defect Fix (DEFECT-117) & Server-Side Filtering | 2 hours 30 minutes | 4 Vitest tests, 12 query tests, 38 backend tests | Completed & Verified |
| **Task 2** | Triage, Escalation & SLA Engine | 9 hours | 124 unit tests, 4 arch tests, 15 frontend tests | Completed & Verified |
| **Task 3** | Authentication & Central Authorization | 2 hours | 128 backend tests, 5 auth flow Vitest tests | Completed & Verified |
| **Task 4** | Dockerise the API & Health Checks | 1 hour 30 minutes | Multi-stage Docker build, non-root app user, /health HTTP 200 | Completed & Verified |
| **Total** | **All Tasks 1 — 4** | **15 hours** (16h with Step 0) | **133 backend tests, 15 frontend tests, Docker verified** | **All Complete** |

### Detailed Implementation Breakdown

| Task | Work completed | Time spent | Verification | Status |
|---|---|---|---|---|
| **Task 1.1** | DEFECT-117 frontend filter dependency array fix, request ID cancellation | ~ 1hour 15minutes | 4 Vitest tests passed, manual browser check | Completed & Verified |
| **Task 1.2** | Server-side ticket filtering, case-insensitive search, paged total counts | 2hours | 12 new query tests, 38 backend tests passed | Completed & Verified |
| **Task 2.1** | Configurable SLA policy, deadline calculation, floor & multiplier rules | Task 2 (9h total) | Unit tests with FixedClock, boundary checks | Completed & Verified |
| **Task 2.2** | SLA status evaluation (`WithinSla`, `AtRisk`, `Breached`, `Met`) | Task 2 (9h total) | Boundary tests at exact thresholds | Completed & Verified |
| **Task 2.3** | Intelligent agent assignment (specialists, capacity limits, least-loaded) | Task 2 (9h total) | Unit tests for tie-breaking and eligibility | Completed & Verified |
| **Task 2.4** | Ticket escalation, one-step priority increase, assignee re-evaluation | Task 2 (9h total) | Domain & command handler unit tests | Completed & Verified |
| **Task 2.5** | Immutable escalation history (`TicketEscalations`), EF Core migration | Task 2 (9h total) | EF Core migration applied, query tests | Completed & Verified |
| **Task 2.6** | Server-side SLA filtering & SQL Server/SQLite compatibility fix | Task 2 (9h total) | SQL Server DateDiff & SQLite Ticks tests | Completed & Verified |
| **Task 2.7** | Frontend escalation panel, SLA indicators, triage result feedback | Task 2 (9h total) | Vitest component tests, UI manual checks | Completed & Verified |
| **Task 3.1** | Agent authentication, PBKDF2 password hashing, credential migration | Task 3 (2h total) | Password hash service tests, seeder tests | Completed & Verified |
| **Task 3.2** | JWT token generation, lifetime, and validation claims | Task 3 (2h total) | JWT options and token service tests | Completed & Verified |
| **Task 3.3** | Central fallback authorization policy & anonymous login endpoint | Task 3 (2h total) | Integration tests with WebApplicationFactory | Completed & Verified |
| **Task 3.4** | Escalation actor resolution from authenticated JWT claims (`sub`) | Task 3 (2h total) | Command handler tests with HttpCurrentAgent | Completed & Verified |
| **Task 3.5** | Frontend login page, session storage, 401 interceptor, protected routes | Task 3 (2h total) | 5 Vitest auth flow tests passed | Completed & Verified |
| **Task 4.1** | Multi-stage Dockerfile (.NET 10 SDK & runtime) with restore caching | Task 4 (1h 30m total) | Docker build succeeded, restore cached | Completed & Verified |
| **Task 4.2** | Root `.dockerignore` for minimal build context | Task 4 (1h 30m total) | Verified context transfer (15.3 kB) | Completed & Verified |
| **Task 4.3** | Non-root container (`app`, UID 1654) on HTTP port 8080 | Task 4 (1h 30m total) | `docker compose exec api whoami` = `app` | Completed & Verified |
| **Task 4.4** | Docker Compose integration with SQL Server health dependency | Task 4 (1h 30m total) | Compose startup, healthy dependency | Completed & Verified |
| **Task 4.5** | Database health check `GET /health` with anonymous access | Task 4 (1h 30m total) | HTTP 200 `Healthy` via curl, unit tests | Completed & Verified |
| **Task 4.6** | Frontend proxy compatibility with containerized API on port 5080 | Task 4 (1h 30m total) | Verified login & ticket fetch via Vite proxy | Completed & Verified |

---

## Task 2 — Triage, Escalation & SLA

**Time spent:** 9 hours  
**Branch:** `task-2-escalation` (merged into `main`)

### Problems identified
- Newly created tickets did not automatically determine priority, compute due dates, or assign an appropriate agent.
- There was no mechanism to escalate tickets or record an immutable audit trail of priority/assignee transitions.
- SLA status was not calculated dynamically or filterable in SQL.

### Changes implemented
- **SLA Policy & Calculation**:
  - Implemented `SlaOptions` and `SlaPolicy` with configurable priority base windows (Low: 72h, Medium: 24h, High: 8h, Critical: 4h), Premium customer multiplier (0.5), minimum window floor (1h), and at-risk threshold (25%).
  - Added dynamic SLA status calculation (`WithinSla`, `AtRisk`, `Breached`, `Met`) in `SlaEvaluator`.
  - Added forced-Critical rule for categories requiring immediate critical handling.
- **Intelligent Automatic Assignment**:
  - Implemented `AgentAssignmentService` ensuring only active agents under their `MaxOpenTickets` limit are considered.
  - Enforced specialist qualification when `Category.RequiresSpecialist` is true.
  - Balanced load by selecting the eligible agent with the fewest open tickets, resolving ties deterministically with the lowest `Agent.Id`.
  - Returned triage explanations when tickets remain unassigned.
- **Ticket Escalation & History**:
  - Implemented `Ticket.Escalate` enforcing one-step priority increase and preventing escalation of Critical tickets.
  - Recalculated due dates from `now` with the new priority window.
  - Re-evaluated agent assignment upon escalation: retained current agent if still eligible and under capacity; reassigned to least-loaded eligible agent otherwise.
  - Created `TicketEscalations` entity, EF Core mapping, and database migration with index on `TicketId`.
- **SQL Server / SQLite Compatibility**:
  - In `TicketQueries.cs`, used `EF.Functions.DateDiffSecond` when connected to SQL Server, and provider-compatible `Ticks` calculation when testing with SQLite to prevent unsupported provider translation exceptions.
- **Frontend Integration**:
  - Added `EscalationPanel` component with validation (minimum reason length), pending state, and server error handling (409 Conflict).
  - Added SLA status indicators and triage assignment feedback.

### Verification
- Architecture tests: **4 passed**
- Unit tests: **124 passed** (SLA windows, forced categories, agent assignment, tie breaking, escalation rules, history recording).
- Frontend tests: **15 passed** across 6 test suites.

---

## Task 3 — Authentication

**Time spent:** 2 hours  
**Branch:** `task-3-authentication` (merged into `main`)

### Problems identified
- The application was entirely open: any client could access all endpoints, mutate tickets, and spoof escalation actor names.

### Changes implemented
- **Credentials & Hashing**:
  - Added `PasswordHash` column to `Agents` table via committed EF Core migration `20261021094627_AddAgentAuthentication`.
  - Used ASP.NET Core `PasswordHasher<Agent>` implementing PBKDF2 with HMAC-SHA512 and cryptographic salts.
  - Configured development credentials for `max@northwind.local` (and legacy agent) seeded at startup via `SupportDbSeeder`.
- **JWT Authentication & Central Authorization**:
  - Added `JwtAccessTokenService` issuing signed tokens with claims (`sub`, `name`, `email`, `jti`).
  - Enforced a centralized fallback authorization policy requiring authenticated users across all endpoints by default.
  - Explicitly allowed anonymous access on `POST /api/auth/login` and Swagger UI.
  - Implemented `ICurrentAgent` / `HttpCurrentAgent` extracting the authenticated agent ID from token claims, preventing escalation actor spoofing.
- **Frontend Authentication Flow**:
  - Built accessible `/login` page with field validation, pending submission state, and error handling for invalid credentials.
  - Stored tokens in `sessionStorage` and attached `Authorization: Bearer <token>` on all requests.
  - Added 401 interceptor that automatically purges storage and redirects unauthenticated users to `/login`.
  - Added `ProtectedRoute` component to guard ticket views.

### Verification
- Integration tests in `AuthenticationEndpointTests.cs` verifying 401 responses for invalid credentials, generic error messages, and fallback policy enforcement.
- Frontend tests in `AuthenticationFlow.test.tsx` verifying validation, login, redirection, and session clearing.
- All 128 backend tests and 15 frontend tests passed.

---

## Task 4 — Docker

**Time spent:** 1 hour 30 minutes  
**Branch:** `task-4-docker` (merged into `main`)

### Problems identified
- The API was not containerized. Running the solution required manual local .NET SDK installation and could cause environment discrepancies.
- Database connectivity was not verifiable through a standardized health endpoint.

### Changes implemented
- **Multi-Stage Dockerfile (`apps/api/Dockerfile`)**:
  - Build stage: `mcr.microsoft.com/dotnet/sdk:10.0`. Copies `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and `.csproj` files first, runs `dotnet restore`, then copies source code and runs `dotnet publish -c Release -o /app/publish --no-restore`.
  - Runtime stage: `mcr.microsoft.com/dotnet/aspnet:10.0`. Runs as non-root `app` user (`USER $APP_UID`, UID 1654), exposes HTTP port `8080`, and sets `ASPNETCORE_HTTP_PORTS=8080`.
- **Docker Ignore (`.dockerignore`)**:
  - Excluded `.git/`, `.env`, `**/bin/`, `**/obj/`, `node_modules/`, `apps/web/`, `apps/api/tests/`, and local IDE files to maintain a minimal build context (15.3 kB).
- **Docker Compose Integration (`docker-compose.yml`)**:
  - Added `api` service with build context `.`, container name `supportdesk-api`, host port mapping `5080:8080`.
  - Configured `depends_on.sqlserver.condition: service_healthy`.
  - Configured runtime environment variables for connection strings, JWT signing keys, and development credentials.
  - Ensured project name `name: northwind-support-main` to preserve existing database containers and data volumes.
- **Health Check Endpoint (`GET /health`)**:
  - Created `DatabaseHealthCheck` implementing `IHealthCheck` using `SupportDbContext.Database.CanConnectAsync()`.
  - Mapped `/health` endpoint with `.AllowAnonymous()`, returning HTTP 200 `Healthy` when SQL Server is reachable and HTTP 503 `Unhealthy` otherwise.
- **Frontend Proxy Verification**:
  - Verified React dev server proxying to containerized API using `VITE_API_PROXY_TARGET=http://localhost:5080`.

### Verification
- Docker build: `docker compose build api` succeeded with code 0.
- Layer caching: Rebuild without C# project file changes reuses cached restore layer, completing in ~2 seconds.
- Runtime container size: 455 MB.
- Container identity: `docker compose exec api whoami` confirmed `app` user (UID 1654).
- Health check: `curl http://localhost:5080/health` returned HTTP 200 `Healthy`.
- Security check: `curl http://localhost:5080/api/tickets` without token returned HTTP 401 Unauthorized; with valid token returned HTTP 200 with 40 tickets.
- Automated tests: All 133 backend tests (including `HealthCheckEndpointTests`) and 15 frontend tests passed.


