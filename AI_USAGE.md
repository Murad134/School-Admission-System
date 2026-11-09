# AI Usage Report

**Project:** School Admission System  
**Recruitment:** Assistant Software Engineer Recruitment, Onnorokom Projukti Limited  
**Submission Date:** November 2026  

---

## 1. Why AI Was Used

During the development of this take-home assignment, AI assistance (specifically Google Gemini / Antigravity coding assistant) was used as a pair-programming partner to:
- Accelerate repetitive boilerplate generation for both the ASP.NET Core Web API backend and React 18+ Vite frontend.
- Explore architectural trade-offs in high-concurrency seat reservation and dynamic temporal expiration.
- Rapidly scaffold comprehensive test scenarios, including parallel stress tests simulating hundreds of concurrent applicants.
- Assist in structuring exhaustive documentation and API contract specifications.

Using AI allowed more time to be focused on critical business invariants, database concurrency semantics, edge cases, manual verification, and end-to-end integration testing.

---

## 2. Areas Where AI Assistance Was Used

AI assistance was utilized across the following phases:
1. **System & Architectural Design:** Evaluating architectural trade-offs between background sweeper workers vs. dynamic query-time expiration.
2. **Database Modeling:** Formulating normalized PostgreSQL relational schemas, indexing strategies for temporal lookups, and constraint definitions.
3. **REST API Contract:** Outlining request/response schemas, DTO structures, and HTTP status codes matching the assignment requirements.
4. **Backend Implementation:** Scaffolding ASP.NET Core controllers, service classes, DTO mappings, and exception-handling middleware.
5. **Frontend Implementation:** Scaffolding React 18+ UI components, real-time polling logic, responsive CSS layouts, and student context persistence.
6. **Docker Orchestration:** Drafting multi-stage Dockerfiles and `docker-compose.yml`.
7. **Test Suite Generation:** Scaffolding unit and parallel concurrency test cases in xUnit.
8. **Technical Documentation:** Assisting in formatting `README.md`, `ASSUMPTIONS.md`, and this usage disclosure.

---

## 3. Architecture & Design Assistance

- **Concurrency Strategy:** The AI assisted in analyzing different concurrency control mechanisms (optimistic concurrency with version/xmin columns vs. distributed locking with Redis vs. pessimistic row-level locking in PostgreSQL). We selected PostgreSQL pessimistic row-level locking (`SELECT ... FOR UPDATE`) because it natively guarantees serialization across multiple backend instances without introducing external dependencies.
- **Dynamic Temporal Expiration:** The AI helped evaluate whether an asynchronous background daemon or dynamic query-time evaluation was superior for seat reclamation. Dynamic evaluation (`ExpiresAt > DateTime.UtcNow`) was selected because it guarantees zero-latency seat recovery the exact millisecond a hold lapses.
- **Service Layer Separation:** The AI assisted in structuring a clean three-tier architecture (Controllers $\rightarrow$ Services $\rightarrow$ EF Core / PostgreSQL) ensuring controllers remain thin HTTP adaptors and all domain rules live inside services.

---

## 4. Code-Generation Assistance

- **ASP.NET Core Web API:** Scaffolding DTO records, controller route signatures, dependency injection setup in `Program.cs`, and centralized exception handling middleware.
- **React Frontend Components:** Generating component scaffolds for `ClassList`, `HoldDetailModal`, `MyHolds`, `ClassCreateModal`, and `Header`.
- **Styling:** Generating clean, responsive CSS with color-coded status badges (`HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`) and real-time countdown indicators.
- **Dockerfiles:** Generating multi-stage Docker build files for .NET and Alpine-based Node.js runtime containers.

---

## 5. Debugging Assistance

AI assistance was instrumental in diagnosing and resolving several technical issues during development:

1. **.NET 10 SDK & Swashbuckle Compatibility:**
   - *Problem:* AI initially generated standard Swashbuckle Swagger configuration (`builder.Services.AddSwaggerGen()`), which failed to compile under .NET 10 (`CS1061: 'IServiceCollection' does not contain a definition for 'AddSwaggerGen'`).
   - *Resolution:* Assisted in replacing legacy Swashbuckle calls with the native .NET 10 `Microsoft.AspNetCore.OpenApi` package (`AddOpenApi()` and `MapOpenApi()`).

2. **EF Core InMemory Provider Transaction Incompatibility:**
   - *Problem:* In-memory unit tests threw `InvalidOperationException: The configured execution strategy 'InMemoryExecutionStrategy' does not support user-initiated transactions`.
   - *Resolution:* Assisted in introducing a relational provider check (`if (_dbContext.Database.IsRelational())`), executing PostgreSQL transactions in production while allowing fast in-memory execution during automated unit tests.

3. **Strict TypeScript Unused Variable Warnings:**
   - *Problem:* Frontend production build (`npm run build`) failed due to strict TypeScript checks (`TS6133: 'setActiveHoldId' is declared but its value is never read`).
   - *Resolution:* Identified and eliminated redundant destructured variables, bringing the frontend build to zero errors and zero warnings.

4. **PowerShell Concurrency Test Timing:**
   - *Problem:* An early PowerShell integration script using `Start-Job` showed false-negative capacity results because child process startup overhead (~5s) exceeded the test hold expiration window.
   - *Resolution:* Switched to high-throughput asynchronous `HttpClient` calls firing concurrently in under 50ms, confirming the expected 1-success, 14-conflict behavior under burst load.

---

## 6. Test-Generation Assistance

AI was used to scaffold tests for the xUnit test suite (`backend/tests/SchoolAdmissionSystem.Tests/`), specifically:
- **Class Management Tests:** Class creation, uniqueness constraint enforcement, and seat capacity validation.
- **Hold Lifecycle Tests:** Hold reservation, confirmation, idempotent duplicate confirmation, voluntary cancellation, and 17-digit student identity validation.
- **Expiration & Reclamation Tests:** Validating that expired holds automatically release seats without manual intervention and that expired holds cannot be confirmed.
- **Burst Concurrency Tests:** Generating parallel execution tests using `Task.WhenAll` to simulate 50 concurrent requests competing for 5 seats, and 25 requests racing for the final 1 available seat.

---

## 7. Documentation Assistance

AI assisted in drafting and structuring the assignment's required markdown documentation:
- **`README.md`:** Formatting quick-start guides, Docker execution commands, API contract tables, and environment variable reference.
- **`ASSUMPTIONS.md`:** Structuring all 14 unspecified domain areas into the required 5-part format (Explicit assignment rule, Unspecified detail, Chosen behavior, Rationale, and Implementation proof).
- **Bonus Concurrency Proof:** Documenting the step-by-step transaction flow of how PostgreSQL row locks serialize requests between two backend instances running on separate ports.

---

## 8. How Generated Code Was Reviewed

All AI-generated code was thoroughly reviewed and validated by the developer prior to integration:
- **Domain Invariant Enforcement:** Verified that every hold creation path strictly validates $\text{held} + \text{confirmed} \le \text{totalSeats}$ within an active database transaction.
- **REST Semantic Compliance:** Verified that endpoints return correct status codes (`201 Created` with `Location` header, `200 OK`, `204 No Content`, `400 Bad Request`, `403 Forbidden`, `404 Not Found`, and `409 Conflict`).
- **DTO Isolation:** Ensured that Entity Framework Core entities are never exposed directly to clients; all endpoints receive and return strongly-typed DTOs.
- **Resource Cleanup & Async Safety:** Checked that all database interactions use asynchronous methods (`SaveChangesAsync`, `FirstOrDefaultAsync`, `CountAsync`) and properly dispose database connections and transactions.
- **Input Sanitization:** Ensured all string inputs are trimmed, bounds-checked, and that `X-Student-Id` is strictly verified against `^[0-9]{17}$`.

---

## 9. How Correctness Was Verified

AI did not independently guarantee correctness. System correctness was verified through multi-layered manual and automated verification:

1. **Automated Unit & Concurrency Test Suite:**
   - Executed `dotnet test` across 30 automated test cases.
   - Verified that high-concurrency parallel tests consistently enforce seat limits without race conditions.
   - Result: **30 Passed, 0 Failed, 0 Skipped**.

2. **Static Typing & Compilation:**
   - Backend compiled with zero warnings and zero errors via `dotnet build`.
   - Frontend compiled with strict TypeScript checking (`tsc && vite build`) with zero warnings and zero errors.

3. **End-to-End Live Integration Verification:**
   - Deployed against a live PostgreSQL 16 database.
   - Tested all 14 lifecycle steps via HTTP requests (class creation, seat hold, hold inspection, fee payment confirmation, cancellation, dynamic expiration, and student history retrieval).
   - Tested multi-threaded race conditions against the live API, verifying that 15 simultaneous requests competing for 1 seat resulted in exactly 1 HTTP 201 success and 14 HTTP 409 conflicts.

4. **Multi-Container Docker Verification:**
   - Verified clean orchestration via `docker compose up --build`.
   - Confirmed network resolution between frontend, backend, and PostgreSQL containers.

---

## 10. Important Parts That Were Manually Reviewed & Validated

The following mission-critical components were explicitly reviewed, refactored, and manually validated by the developer:

1. **PostgreSQL Row-Level Locking Query:**
   Manually verified the exact SQL syntax:
   ```sql
   SELECT * FROM classes WHERE "Id" = @classId FOR UPDATE;
   ```
   Ensured this lock is acquired before seat counting occurs, preventing the classic "check-then-act" race condition.

2. **Seat Availability Dynamic Calculation Formula:**
   Manually audited the seat counting logic:
   $$\text{activeHeld} = \text{Count}(\text{Status} = \text{'HELD'} \ \land \ \text{ExpiresAt} > \text{UtcNow})$$
   $$\text{confirmed} = \text{Count}(\text{Status} = \text{'CONFIRMED'})$$
   $$\text{available} = \text{TotalSeats} - (\text{activeHeld} + \text{confirmed})$$
   Ensured that expired holds are never counted as occupied seats, enabling immediate reclamation.

3. **Hold Ownership & Authorization Boundaries:**
   Manually audited `HoldService.ConfirmHoldAsync` and `HoldService.CancelHoldAsync` to verify that `hold.StudentId == studentId`. Verified that mismatched student IDs throw `ForbiddenException` (HTTP 403), preventing unauthorized tampering.

4. **Idempotent Confirmation Semantics:**
   Manually validated that repeated confirmation requests on an already confirmed hold return `200 OK` with the confirmed entity without incrementing seat counts or throwing errors.

5. **Single Active Hold Rule per Class:**
   Manually verified that an applicant cannot hold more than one seat in the same class simultaneously, while retaining the ability to re-apply if their previous hold expired or was cancelled.

6. **Student ID Format Enforcement:**
   Manually tested edge cases (16 digits, 18 digits, letters, special characters, whitespace, null) against `StudentIdValidator.cs` to ensure non-conforming inputs are rejected with HTTP 400.

---

## Conclusion

AI served as a productivity multiplier for boilerplate generation, test scaffolding, and documentation drafting. However, all architectural decisions, concurrency semantics, locking mechanisms, domain rule validations, code reviews, and end-to-end verifications were actively directed, implemented, and verified by the developer.
