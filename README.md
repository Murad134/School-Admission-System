# School Admission System

An online admission system designed for high-concurrency "first-come, first-served" (FCFS) enrollment with **no lottery**, built as a take-home assignment for **Onnorokom Projukti Limited** (Assistant Software Engineer Recruitment).

---

## Tech Stack

- **Backend:** C# (.NET 8+ / ASP.NET Core Web API)
- **Database:** PostgreSQL 16
- **ORM & Concurrency:** Entity Framework Core with native PostgreSQL row-level locking (`SELECT ... FOR UPDATE`)
- **Frontend:** React 18+, TypeScript, Vite, Modern CSS
- **Containerization & Orchestration:** Docker & Docker Compose

---

## Repository Structure

```text
School Admission System/
├── frontend/
│   ├── src/
│   │   ├── components/         # Header, ClassList, HoldDetailModal, MyHolds, etc.
│   │   ├── context/            # StudentContext (persists 17-digit ID in localStorage)
│   │   ├── services/           # Typed API service with X-Student-Id injection
│   │   ├── types/              # DTO TypeScript interfaces
│   │   ├── App.tsx             # Main view router
│   │   ├── index.css           # Responsive styles & status badges
│   │   └── main.tsx
│   ├── public/
│   ├── .env.example
│   ├── Dockerfile
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── backend/
│   ├── src/
│   │   └── SchoolAdmissionSystem.Api/
│   │       ├── Common/         # StudentIdValidator (17 numeric digits)
│   │       ├── Controllers/    # ClassesController, HoldsController, MeController
│   │       ├── Data/           # ApplicationDbContext with composite & unique indexes
│   │       ├── DTOs/           # Request & Response public DTO contracts
│   │       ├── Exceptions/     # DomainExceptions (mapped to 400, 403, 404, 409)
│   │       ├── Middleware/     # ExceptionHandlingMiddleware
│   │       ├── Models/         # SchoolClass, SeatHold, HoldStatus
│   │       ├── Services/       # ClassService, HoldService (concurrency row lock)
│   │       ├── Program.cs      # Startup, CORS, DI, Kestrel port 8080
│   │       ├── appsettings.json
│   │       └── SchoolAdmissionSystem.Api.csproj
│   │
│   ├── tests/
│   │   └── SchoolAdmissionSystem.Tests/
│   │       ├── ClassManagementTests.cs
│   │       ├── HoldLifecycleTests.cs
│   │       ├── ExpirationAndCancellationTests.cs
│   │       ├── StudentRuleTests.cs
│   │       ├── ConcurrencyTests.cs
│   │       └── TestDbContextFactory.cs
│   │
│   ├── Dockerfile
│   └── SchoolAdmissionSystem.sln
│
├── docker-compose.yml
├── README.md
├── ASSUMPTIONS.md
├── AI_USAGE.md
├── .env.example
└── .gitignore
```

---

## Quick Start with Docker

The entire stack (PostgreSQL, Backend API, and Frontend SPA) runs with a single command. **No local dependencies (Node, .NET, or PostgreSQL) need to be installed on the host machine.**

### 1. Start the System
From the repository root:
```bash
docker compose up --build
```

### 2. Access the Application
- **Frontend UI:** Open your browser at [http://localhost:5173](http://localhost:5173)
- **Backend API:** Available at [http://localhost:8080](http://localhost:8080)
- **PostgreSQL Database:** Internal `postgres:5432` (mapped to `localhost:5432`)

### 3. Stop the System
```bash
# Stop containers (preserves database data via named volume)
docker compose down

# Stop containers and remove database volume
docker compose down -v
```

### 4. View Logs
```bash
docker compose logs -f backend
docker compose logs -f frontend
docker compose logs -f postgres
```

---

## Concurrency Invariant: `held + confirmed <= totalSeats`

Under no circumstances can total occupied seats exceed `totalSeats`, even under high burst concurrent load.

- **PostgreSQL Row-Level Locking:** During hold creation, the backend acquires an exclusive row lock on the parent class:
  ```sql
  SELECT * FROM classes WHERE "Id" = @classId FOR UPDATE;
  ```
- **Serialization:** All concurrent transactions competing for seats in the same class queue sequentially behind this row lock.
- **Dynamic Expiration:** Expired holds (`ExpiresAt <= NOW()`) are automatically excluded from the count, releasing seats with zero latency.
- **Multi-Instance Safe:** Because row locking happens inside the PostgreSQL engine, multiple backend processes or containers connected to the same database are 100% concurrency-safe.

---

## Running Automated Tests

The backend test suite contains **30 automated tests** covering all domain rules and high-concurrency race conditions.

### Run Backend Tests:
```bash
dotnet test backend/tests/SchoolAdmissionSystem.Tests/SchoolAdmissionSystem.Tests.csproj
```

**Key Tests Included:**
- `CreateHold_UnderHighBurstConcurrency_NeverExceedsTotalSeats`: 50 concurrent requests for 5 seats. Proves exactly 5 succeed, 45 fail with 409 Conflict, and `held + confirmed <= totalSeats` is maintained.
- `CreateHold_SimultaneousRaceForFinalAvailableSeat_AllowsOnlyOneSuccess`: 25 concurrent requests competing simultaneously for the final 1 available seat. Proves only 1 succeeds and 24 fail with 409 Conflict.
- `ConfirmHold_WhenAlreadyConfirmed_IsIdempotent`: Duplicate confirmation calls return 200 without increasing confirmed seats.
- `ExpiredHold_AutomaticallyReleasesSeat_WithoutManualIntervention`: Expired holds release seats automatically without manual API calls.

---

## API Contract Reference

| Method | Endpoint | Required Headers | Description | Status Codes |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/classes` | None | Create new class shift | `201`, `400` |
| `GET` | `/classes` | None | List classes with live seat counts | `200` |
| `GET` | `/classes/{classId}` | None | Single class seat counts | `200`, `404` |
| `POST` | `/classes/{classId}/holds` | `X-Student-Id` | Reserve a seat hold | `201`, `400`, `404`, `409` |
| `GET` | `/holds/{holdId}` | None | Get hold status (`HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`) | `200`, `404` |
| `POST` | `/holds/{holdId}/confirm` | `X-Student-Id` | Pay admission fee & confirm | `200`, `400`, `403`, `404`, `409` |
| `DELETE` | `/holds/{holdId}` | `X-Student-Id` | Cancel hold and free seat | `204`, `400`, `403`, `404`, `409` |
| `GET` | `/me/holds` | `X-Student-Id` | List requesting student's holds | `200`, `400` |

---

## Environment Variables

Copy `.env.example` to `.env` if custom configurations are desired:

| Variable | Default Value | Description |
| :--- | :--- | :--- |
| `POSTGRES_DB` | `school_admission` | PostgreSQL database name |
| `POSTGRES_USER` | `postgres` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `postgres` | PostgreSQL password |
| `POSTGRES_PORT` | `5432` | Host port for PostgreSQL |
| `HOLD_DURATION_SECONDS` | `600` | Seat reservation duration in seconds (10 mins) |
| `ASPNETCORE_URLS` | `http://+:8080` | Backend API listening URL |
| `VITE_API_BASE_URL` | `http://localhost:8080` | API target for the React frontend |

