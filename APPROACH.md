# Engineering Approach & Thought Process

## 1. Initial Architecture Understanding

When approaching the Northwind Support Desk codebase, the first priority was understanding the project's boundaries, patterns, and existing conventions.

The system is organized as a clean-architecture monorepo:
- **Backend (.NET 10)**:
  - `SupportDesk.Domain`: Pure domain models, value objects, and aggregate roots (e.g., `Ticket`, `Agent`, `Customer`, `Category`). Zero third-party dependencies.
  - `SupportDesk.Application`: Feature-driven CQRS handlers, commands, queries, validators (FluentValidation), and interfaces (`ITicketRepository`, `IClock`, `IAccessTokenService`).
  - `SupportDesk.Infrastructure`: Data persistence with Entity Framework Core and SQL Server, migrations, read queries (`TicketQueries`), and external service adapters.
  - `SupportDesk.Presentation`: Web API controllers, middleware, JWT authentication configuration, and composition root.
  - `tests`: `SupportDesk.ArchitectureTests` guarding clean architecture dependencies and `SupportDesk.UnitTests` covering domain, application, and infrastructure logic.
- **Frontend (React 19 / TypeScript / Vite)**:
  - Feature-oriented directory structure (`features/tickets`, `features/auth`).
  - Vite dev server with proxying: local browser requests to `/api` are proxied to the backend, avoiding browser CORS complications.
  - Custom React hooks managing state and data fetching.
- **Data Store**: SQL Server 2022 running in Docker with pre-seeded demo records.

The primary objective was to deliver high-quality, production-ready increments across Tasks 1 through 4 with minimal disruption to existing code, zero regressions, and strong test coverage.

---

## 2. Task 1 — Debugging & Server-Side Filtering Strategy

### DEFECT-117 Root Cause Analysis
The defect reported was that dropdown filter changes (such as selecting a status or priority) failed to update the ticket table in the UI.

Inspecting `apps/web/src/features/tickets/hooks/useTicketList.ts` revealed that while the filter dropdowns correctly updated local state, the `useEffect` responsible for triggering API requests only included `[page, pageSize, debouncedSearch]` in its dependency array. Because `status`, `priority`, `categoryId`, and other filter properties were missing from the dependency array, changing them never triggered a fetch.

### Frontend Fix
- Expanded the effect dependency array to include all filter parameters (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`).
- Preserved the 300 ms debounce for text search while ensuring discrete filter selections trigger immediately.
- Implemented request cancellation/sequence tracking using an incrementing `requestId` ref so that if multiple requests are fired in rapid succession, slower out-of-order responses are discarded and do not overwrite fresh results.

### Server-Side Filtering (`TicketQueries.cs`)
Although the API accepted query parameters, the database query previously returned all tickets.
- Implemented filtering in `TicketQueries.cs` using composable `IQueryable<Ticket>` expressions.
- Crucially, filters are evaluated **before** `CountAsync()` and `Skip/Take` pagination. This ensures that `totalCount` accurately reflects the filtered count rather than the total table rows.
- Applied trimmed, case-insensitive substring searching across ticket title, reference number, and customer name.

---

## 3. Task 2 — Business Rules, Triage & Escalation Design

### Configurable SLA Policy
- **Design Decision**: Instead of hardcoding SLA deadlines inside domain entities or SQL statements, created `SlaOptions` and `SlaPolicy`.
- Base windows by priority: Low (72h), Medium (24h), High (8h), Critical (4h).
- Premium customer adjustment: Multiplied by `0.5`, with an enforced floor (`MinimumWindowHours = 1h`).
- Configurable via `appsettings.json` and validated at startup using .NET's `ValidateOnStart()` pattern to fail fast on malformed configuration.

### SLA Status Evaluation
SLA status is evaluated dynamically based on the current timestamp (`now`):
- `Breached`: `now >= DueDateUtc` (or resolved after deadline).
- `AtRisk`: Not yet breached, unresolved, and remaining duration is <= 25% of the total SLA window (`CreatedAtUtc` to `DueAtUtc`).
- `Met`: Ticket resolved or closed before `DueDateUtc`.
- `WithinSla`: Unresolved, before deadline, and outside the at-risk threshold.

### Intelligent Automatic Assignment
When a ticket is created:
1. Category requirement check: If `Category.RequiresSpecialist` is true, only agents with specialist qualification are eligible.
2. Availability check: Agent must be active (`IsActive == true`) and have strictly fewer open tickets than `MaxOpenTickets`.
3. Workload balancing: From all eligible agents, select the agent with the lowest open ticket count.
4. Deterministic tie-breaking: If multiple agents tie for fewest open tickets, select the agent with the lowest `Agent.Id`.
5. If no agent qualifies, the ticket remains unassigned with a clear triage explanation returned in the API response.

### Escalation Architecture
Escalation changes state and must be strictly audited:
- Priority moves up exactly one step: `Low` -> `Medium` -> `High` -> `Critical`. Critical tickets cannot be escalated (enforced in domain and API with HTTP 409).
- Mandatory reason (minimum 10 characters).
- Due date recalculated from `now` using the new priority window and customer tier.
- Assignee re-evaluated: If the current agent is still eligible and within their capacity, they are retained; otherwise, the least-loaded eligible agent is assigned.
- **Immutable History**: Every escalation writes a row to `TicketEscalations` recording previous/new priorities, dates, previous/new assignees, reason, who performed the escalation, and the exact UTC timestamp.

---

## 4. Task 3 — Authentication & Security Strategy

### Agent Credentials on `Agent` Aggregate
- **Decision**: Added `PasswordHash` directly to the `Agent` aggregate rather than introducing a separate ASP.NET Identity database or external `Users` table.
- **Rationale**: The domain model already treats `Agent` as the core operational entity. Adding a hashed credential column via a committed EF Core migration avoided unnecessary tables, complex joins, and excess Identity overhead, while keeping agent profile and authentication synchronized.

### Secure Password Hashing
- Utilized ASP.NET Core's vetted `PasswordHasher<Agent>` (`Microsoft.AspNetCore.Identity.Core`), encapsulated behind `IPasswordHashService`.
- Uses PBKDF2 with HMAC-SHA512, cryptographic per-user salting, and 100,000+ iterations.
- Never stores plaintext passwords, reversible tokens, or homemade hashes.
- Development credentials (`max@northwind.local`) are seeded at startup using runtime environment variables (`DevelopmentCredentials__AgentPassword`).

### Centralized Authorization Policy
- Implemented a global fallback policy:
  ```csharp
  builder.Services.AddAuthorization(options => {
      options.FallbackPolicy = new AuthorizationPolicyBuilder()
          .RequireAuthenticatedUser()
          .Build();
  });
  ```
- This ensures **secure-by-default**: any new endpoint added to the application automatically requires a valid JWT unless explicitly marked with `[AllowAnonymous]`.
- Anonymous endpoints: `POST /api/auth/login`, `GET /health`, and Swagger UI in development.

### Secure Escalation Actor
- Instead of trusting an `escalatedBy` field in the request body, `EscalateTicketCommandHandler` resolves `ICurrentAgent` from the authenticated JWT claims (`ClaimTypes.NameIdentifier` / `sub`). Spoofing the escalation actor is impossible.

### Frontend Session Management
- Tokens and agent metadata are kept in `sessionStorage`.
- **Trade-off**: `sessionStorage` isolates tokens per browser tab and automatically purges them when the session terminates. While vulnerable to XSS if malicious scripts run, it is immune to CSRF.
- Axios client interceptor attaches `Authorization: Bearer <token>` to all requests and intercepts HTTP 401 responses to clear storage and redirect to `/login`.

---

## 5. Task 4 — Docker Strategy & Verification

### Multi-Stage Dockerfile
- **Build Stage**: Uses `mcr.microsoft.com/dotnet/sdk:10.0`. Copies `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and all `.csproj` files first, then executes `dotnet restore`. C# source files (`apps/api/src/`) are copied afterwards.
- **Layer Caching**: Changes to application code do not invalidate the cached NuGet package restore layer, reducing iterative build times from ~40s to ~2s.
- **Runtime Stage**: Uses `mcr.microsoft.com/dotnet/aspnet:10.0`. Runs as non-root user (`USER $APP_UID`, UID 1654 / `app`). Listens on HTTP port `8080` without baking in HTTPS certificates or secrets.

### Docker Compose Integration
- Added `api` service to `docker-compose.yml` mapped to host port `5080`.
- Depends on `sqlserver` with `condition: service_healthy`.
- Supplies database connection string and JWT signing keys via environment variables.

### Database Health Check
- Created `DatabaseHealthCheck` implementing `IHealthCheck` using `SupportDbContext.Database.CanConnectAsync()`.
- Exposed at `GET /health` with `.AllowAnonymous()`. Returns HTTP 200 `Healthy` when SQL Server is reachable, and HTTP 503 `Unhealthy` if disconnected.

---

## 6. Technical Challenges & Solutions

### Challenge 1: SQL Server vs. SQLite Date Arithmetic Incompatibility
- **Problem**: EF Core translates `EF.Functions.DateDiffSecond` into native T-SQL for SQL Server. However, SQLite (used for in-memory unit tests) does not support `DateDiffSecond`, causing query tests to throw `InvalidOperationException`.
- **Solution**: In `TicketQueries.cs`, inspected `db.Database.IsSqlServer()`. When executing against SQL Server, the query uses `EF.Functions.DateDiffSecond`. When executing against SQLite or in-memory providers during tests, it falls back to provider-compatible `Ticks` calculation. This maintained database-level SQL filtering in production while preserving fast in-memory SQLite unit testing.

### Challenge 2: Dual EF Core Provider Registration in Test Host
- **Problem**: In integration tests using `WebApplicationFactory`, attempting to replace SQL Server with in-memory SQLite caused EF Core to complain that multiple database providers (`SqlServer` and `Sqlite`) were registered in the same `ServiceCollection`.
- **Solution**: Instead of re-registering the entire DbContext in `HealthCheckEndpointTests`, used `services.PostConfigure<HealthCheckServiceOptions>` to substitute the health check delegate for endpoint routing and status code verification, and tested `DatabaseHealthCheck` directly in isolated unit tests with SQLite and SQL Server options.

### Challenge 3: Preserving Existing SQL Server Container Data
- **Problem**: The assessment database already contained seeded development data in an existing Docker volume (`northwind-support-main_supportdesk-sql`). Running standard Compose commands without configuration could recreate volumes or cause name collision errors.
- **Solution**: Explicitly set `name: northwind-support-main` in `docker-compose.yml`. This allowed Compose to recognize the existing healthy `supportdesk-sql` container and its data volume, preserving all existing records while starting the new `api` service.

---

## 7. Design Alternatives & Trade-offs

| Decision | Alternative Considered | Why Chosen | Trade-off |
|---|---|---|---|
| **Credential Storage** | ASP.NET Identity (`AspNetUsers`) | Added `PasswordHash` to `Agent` aggregate | Avoided introducing 7+ Identity tables and complex joins for a simple agent pool. | Does not provide built-in lockout, 2FA, or claims management. |
| **Token Storage** | `localStorage` or HttpOnly cookie | `sessionStorage` | Immune to CSRF and clears upon tab close; fits SPA architecture. | Token is accessible to JavaScript (XSS risk) and not shared across tabs. |
| **Authorization** | Per-controller `[Authorize]` attributes | Global Fallback Policy | Secure-by-default; guarantees newly added endpoints are never inadvertently public. | Requires explicit `.AllowAnonymous()` on public endpoints. |
| **Filtering Execution** | In-memory filtering after query | Composable `IQueryable` in SQL before paging | Essential for scalability and correct pagination totals. | Query logic is tightly coupled to EF Core LINQ provider capabilities. |
| **Container User** | Default `root` user | Official `app` user (UID 1654) | Principle of least privilege; prevents container breakout risks. | Container cannot bind to privileged ports (<1024) or install packages at runtime. |

---

## 8. Testing & Verification Summary

The test suite consists of 133 automated backend tests and 15 frontend tests:
- **Architecture Tests (4)**: Enforce layer separation (Domain has no dependencies; Application does not reference Infrastructure or Presentation).
- **Domain & Unit Tests (129)**:
  - SLA deadline calculations across all priorities, customer tiers, and minimum floors.
  - SLA status transitions (`WithinSla`, `AtRisk`, `Breached`, `Met`).
  - Intelligent assignment: specialist filtering, workload caps, least-loaded balancing, tie-breaking.
  - Escalation rules: priority step increases, validation rejections, history creation.
  - Password hashing and verification.
  - JWT generation, expiration, and claims mapping.
  - Health check endpoint routing, status codes, and database connectivity.
- **Frontend Tests (15)**:
  - Ticket table rendering, SLA badges, escalation panel integration.
  - Login page validation, pending states, error messages, and authenticated session storage.
  - API client Authorization header injection and 401 handling.
- **Live Verification**:
  - `docker compose up --build`: Successful build and start.
  - `curl http://localhost:5080/health`: HTTP 200 `Healthy`.
  - `curl http://localhost:5080/api/tickets`: HTTP 401 Unauthorized without token; HTTP 200 with Bearer token.
  - `docker compose exec api whoami`: Confirmed `app`.
  - Frontend proxy: Verified full login, ticket browsing, and triage against container on `localhost:5080`.

---

## 9. Future Improvements With More Time

1. **Refresh Token Flow**: Introduce sliding-window refresh tokens stored in HttpOnly, SameSite cookies to improve user experience without extending raw access token lifetimes.
2. **Rate Limiting & Account Lockout**: Implement ASP.NET Core rate limiting middleware on `POST /api/auth/login` to prevent brute-force credential stuffing.
3. **Containerized Production Frontend**: Create a production NGINX container serving the compiled React bundle to package the entire system for staging deployments.
4. **Redis Caching for Reference Data**: Cache categories, customers, and agent profiles to reduce repetitive database queries on read-heavy workflows.
5. **Database Migration Bundle in CI/CD**: Decouple runtime database migrations from API startup by using EF Core migration bundles or dedicated deployment pipeline steps.
