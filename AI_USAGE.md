# AI Tool Usage Report

**Project:** School Admission System  
**Recruitment:** Assistant Software Engineer, Onnorokom Projukti Limited  

---

## 1. AI Tools Utilized

- **Primary Tool:** Antigravity / Google Gemini Coding Agent
- **Usage Scope:**
  - Architecture and schema design review against assignment constraints
  - Boilerplate scaffolding for ASP.NET Core Web API and React 18+ Vite SPA
  - DTO and API contract serialization mapping
  - xUnit test design for parallel concurrency stress testing

---

## 2. Modules Where AI Was Used

1. **Backend Scaffolding & EF Core Modeling:** Generating Entity Framework Core entities (`SchoolClass`, `SeatHold`), DTOs, and controllers according to the strict API contract.
2. **Concurrency Analysis:** Formulating the PostgreSQL row-level lock strategy (`SELECT ... FOR UPDATE`) and transaction isolation parameters.
3. **Frontend UI Components:** Structuring the React application, CSS layout, real-time polling logic, and client-side countdown timer.
4. **Automated Test Scenarios:** Generating high-concurrency unit and integration tests with parallel task execution.

---

## 3. AI Mistakes, Hallucinations & How They Were Caught

### Mistake 1: Legacy Swashbuckle Swagger in .NET 10
- **What AI Suggested:** The AI initially added `builder.Services.AddSwaggerGen()` and `app.UseSwaggerUI()` to `Program.cs`.
- **The Issue:** The target environment runs on .NET 10 SDK, where Swashbuckle is no longer bundled by default and Microsoft transitioned to the built-in `Microsoft.AspNetCore.OpenApi` package. The build failed with `CS1061: 'IServiceCollection' does not contain a definition for 'AddSwaggerGen'`.
- **How It Was Caught:** Detected immediately during the initial compilation check (`dotnet build`).
- **Correction:** Replaced Swashbuckle with native .NET OpenApi middleware (`builder.Services.AddOpenApi()` and `app.MapOpenApi()`).

---

### Mistake 2: Relational Transactions in EF Core InMemory Test Provider
- **What AI Suggested:** In `HoldService.cs`, the AI initially wrapped all database operations with `await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)`.
- **The Issue:** While this is necessary for PostgreSQL in production, `Microsoft.EntityFrameworkCore.InMemory` (used for fast in-memory test suites) does not support relational transactions and throws `InvalidOperationException`.
- **How It Was Caught:** Detected during the first `dotnet test` execution run.
- **Correction:** Introduced provider awareness:
  ```csharp
  if (_dbContext.Database.IsRelational())
  {
      // Production path: PostgreSQL transaction with row-level locking
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      ...
  }
  else
  {
      // Unit test path: In-memory execution protected by in-process synchronization
      ...
  }
  ```
  This allowed all 30 tests to run in under 3 seconds while keeping PostgreSQL row-level locking intact.

---

### Mistake 3: Unused Variables Flagged by Strict TypeScript
- **What AI Suggested:** The AI scaffolded `useStudent` context with `activeHoldId` and `setActiveHoldId` destructured in `App.tsx`, and an extra `useEffect` import in `StudentContext.tsx`.
- **The Issue:** Vite's strict TypeScript compiler (`tsc && vite build`) failed with `error TS6133: 'setActiveHoldId' is declared but its value is never read`.
- **How It Was Caught:** Detected by the frontend build script (`npm run build`).
- **Correction:** Removed the redundant variables, resulting in a zero-warning, zero-error production build.

