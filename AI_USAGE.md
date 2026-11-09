# AI Usage Report

## 1. AI Tool Used

**Primary Tool:** Antigravity (Google Gemini Coding Agent)

I used Antigravity as an AI-powered coding assistant throughout the development of the School Admission System. It helped with implementation planning, code generation, debugging, test case preparation, and documentation.

AI was used as a productivity assistant, while the final technical decisions, code review, implementation adjustments, and verification were handled by me.

## 2. Areas Where AI Was Used

### 2.1 Architecture and Database Design
- Discussed backend and frontend architecture.
- Explored database schema design and relationships.
- Reviewed PostgreSQL constraints, indexes, and data consistency.
- Discussed transaction handling and concurrency considerations.

### 2.2 Backend Development
- Generated initial ASP.NET Core project structure and boilerplate code.
- Assisted with REST API endpoint implementation.
- Helped structure services, controllers, DTOs, and validation.
- Assisted with Entity Framework Core configuration and database operations.

### 2.3 Frontend Development
- Assisted with React component structure and reusable UI components.
- Generated initial forms and API integration logic.
- Helped organize frontend state and user interactions.
- Reviewed TypeScript types and build-related issues.

### 2.4 Concurrency and Seat Management
- Discussed strategies for preventing double booking.
- Reviewed PostgreSQL row-level locking using `SELECT ... FOR UPDATE`.
- Assisted with implementing temporary seat holds and expiration handling.
- Explored concurrent booking scenarios and conflict responses.

### 2.5 Testing
- Assisted with preparing xUnit test cases.
- Reviewed class management, seat hold lifecycle, expiration, and concurrency scenarios.
- Helped prepare API-level verification scenarios.

### 2.6 Docker and Documentation
- Assisted with Dockerfile and Docker Compose configuration.
- Helped prepare project setup instructions.
- Reviewed README and AI usage documentation.

## 3. Development Issues and AI-Assisted Debugging

During development, several technical issues were identified and resolved with AI assistance.

### Issue 1: Swagger Configuration in .NET 10

**Problem:**  
The initially generated Swagger configuration using `AddSwaggerGen()` caused a `CS1061` compilation error because the required Swashbuckle setup was not available in the project.

**Resolution:**  
Updated the implementation to use the native `Microsoft.AspNetCore.OpenApi` approach with `AddOpenApi()` and `MapOpenApi()`.

### Issue 2: EF Core InMemory Transaction Support

**Problem:**  
The EF Core InMemory provider does not support user-initiated relational database transactions.

**Resolution:**  
Used `Database.IsRelational()` to handle transaction-related logic conditionally, allowing the application to work appropriately with both relational and InMemory database providers.

### Issue 3: TypeScript Unused Variable Warning

**Problem:**  
The frontend build reported a `TS6133` warning because a declared variable, `setActiveHoldId`, was not being used.

**Resolution:**  
Reviewed the affected component and removed redundant variables and unused imports to maintain a clean TypeScript build.

### Issue 4: PowerShell Concurrency Test Timing

**Problem:**  
The initial concurrency testing approach using PowerShell `Start-Job` introduced process startup overhead. This affected the accuracy of testing when the seat hold expiration time was short.

**Resolution:**  
Replaced the approach with concurrent asynchronous `HttpClient` requests to test simultaneous API requests more reliably.

## 4. Testing and Verification

AI assisted in preparing test scenarios, but the application behavior was verified through automated tests and practical execution.

### Automated Testing
- Prepared and executed xUnit tests for class management.
- Tested the seat hold lifecycle.
- Tested hold expiration and seat reclamation.
- Tested concurrent booking scenarios.

**Test Result:**
- Total Tests: 30
- Passed: 30
- Failed: 0
- Skipped: 0

### Concurrency Testing

The following scenarios were used to verify seat allocation behavior:

| Scenario | Expected Result |
|---|---|
| 50 requests competing for 5 seats | Maximum 5 successful allocations |
| 25 requests competing for the final 1 seat | Only 1 successful allocation |
| 15 live API requests competing for 1 seat | 1 HTTP 201 and 14 HTTP 409 responses |

### Build Verification

| Component | Verification | Result |
|---|---|---|
| Backend | `dotnet build` | Successful, 0 warnings and 0 errors |
| Backend Tests | `dotnet test` | 30 passed |
| Frontend | `tsc && vite build` | Successful, 0 warnings and 0 errors |
| Database | PostgreSQL 16 integration verification | Successful |
| Docker | `docker compose up --build` | Verified |

### Additional Manual Verification
- Reviewed PostgreSQL row-level locking using `SELECT ... FOR UPDATE`.
- Verified dynamic seat availability calculation.
- Checked hold ownership and expiration behavior.
- Verified idempotent confirmation behavior.
- Checked the one-active-hold-per-class rule.
- Verified 17-digit student ID validation.
- Executed the complete 14-step lifecycle verification.

## 5. Developer Responsibility

AI-generated code and suggestions were not accepted blindly.

I reviewed the generated code, evaluated the suggested approaches, adjusted implementations according to the project requirements, resolved development issues, and verified the final behavior through builds, automated tests, database integration, and API-level testing.

AI was used as a development assistant to improve productivity, not as a replacement for understanding, implementation, or technical decision-making.

## 6. Summary

Antigravity (Google Gemini Coding Agent) was used throughout the project for architecture discussions, code scaffolding, debugging assistance, testing preparation, and documentation.

The final implementation was reviewed and verified through automated tests, build checks, PostgreSQL integration testing, concurrency testing, and Docker execution.

## 7. Transparency

This report documents the areas where AI assistance was used during development. The final submitted project reflects my review, implementation adjustments, and verification of the application against the assignment requirements.
