# Assumptions & Architectural Decisions

This document outlines the architectural assumptions, interpretations of ambiguous specifications, and the multi-instance concurrency proof for the **School Admission System**.

---

## 1. Assumptions & Design Decisions

### 1. Student Identity & `X-Student-Id` Format
- **Specification:** "Header X-Student-Id এর মান হবে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর।"
- **Assumption:** In Bangladesh, official birth registration numbers are 17 digits. We strictly enforce regex `^[0-9]{17}$`. Requests missing the header or providing non-17-digit numeric strings fail early with `400 Bad Request`.
- **Reason:** Enforcing structural validity at the API boundary prevents corrupt identifiers from reaching the database.

### 2. Entity Identifier Format
- **Specification:** "Example-এর ID (c_123, h_789) শুধু উদাহরণ, ID-এর format আপনার পছন্দমতো।"
- **Assumption:** We adopted deterministic, human-readable prefixed identifiers:
  - Classes: `c_` followed by an 8-character alphanumeric string (e.g., `c_4a91fbc2`).
  - Holds: `h_` followed by an 8-character alphanumeric string (e.g., `h_7d82e10a`).
- **Reason:** Provides URL-friendly identifiers that clearly denote resource types while avoiding sequential ID enumeration vulnerabilities.

### 3. Expiration Logic & Zero-Latency Seat Reclamation
- **Specification:** "মেয়াদ শেষ হলে seat নিজে থেকে ফেরত যাবে, কাউকে কোনো API call করতে হবে না।"
- **Assumption:** Instead of relying on a periodic cron job or background sweeper that could introduce seconds or minutes of delay before seats are returned, **expiration is evaluated dynamically against UTC `CURRENT_TIMESTAMP`**:
  $$\text{activeHeldSeats} = \sum (\text{status} = \text{'HELD'} \ \land \ \text{expiresAt} > \text{UtcNow})$$
- **Reason:** Guarantees zero latency. The exact millisecond `expiresAt` passes, the seat becomes instantly available to other parents without waiting for any background process. Furthermore, when `GET /holds/{id}` or `GET /me/holds` is called on a hold whose window has passed, the status dynamically projects as `EXPIRED`.

### 4. Student Re-Application Policy
- **Specification:** "একজন শিক্ষার্থী একটা class-এ একই সময়ে একটার বেশি active আবেদন বা নিশ্চিত ভর্তি রাখতে পারবে না।"
- **Assumption:** A student is blocked from creating a hold in a class **only if** they have a currently active hold (`status = 'HELD' AND expiresAt > UtcNow`) or a confirmed admission (`status = 'CONFIRMED'`). If their previous hold in that class has `EXPIRED` or was `CANCELLED`, they are allowed to submit a new application.
- **Reason:** Punishing parents whose temporary reservation expired by permanently barring them from applying would be counter-intuitive to the user experience.

### 5. Idempotent Confirmation
- **Specification:** "একই আবেদনে confirm একাধিকবার এলেও confirmedSeats একবারই বাড়বে।"
- **Assumption:** Rapid duplicate clicks on "ভর্তি ফি দিন" (Confirm) return `200 OK` with `{"holdId": "...", "status": "CONFIRMED"}` without error and without incrementing the confirmed seats counter.
- **Reason:** Ensures reliable network retry semantics for parents with unstable connections.

### 6. Simulated Payment Flow
- **Specification:** "'ভর্তি ফি দিন' চাপলে ধরে নেবেন payment সফল হয়েছে। আসল payment gateway লাগবে না।"
- **Assumption:** `POST /holds/{holdId}/confirm` directly transitions the hold to `CONFIRMED`. No payment gateway integration or request body payload is required.

---

## 2. Bonus: Multi-Instance Concurrency Guarantee

**Question:** How is `held + confirmed <= totalSeats` guaranteed even if two (or more) backend processes run simultaneously on separate ports or separate Docker containers against the same PostgreSQL database?

### Architectural Proof & Explanation

1. **Elimination of Process-Bound Synchronization:**
   In-process locking (such as C# `lock`, `Monitor`, or `SemaphoreSlim`) only synchronizes threads within a single operating system process. If Process 1 runs on port 8080 and Process 2 runs on port 8081, process-level locks cannot see each other.

2. **PostgreSQL Row-Level Locking (`SELECT ... FOR UPDATE`):**
   To achieve 100% concurrency safety across multiple backend instances, serialization is offloaded to the **PostgreSQL database engine**:
   ```sql
   BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

   -- Acquires exclusive tuple write lock on the target class row
   SELECT * FROM classes WHERE "Id" = @classId FOR UPDATE;

   -- Count active holds (unexpired HELD + CONFIRMED)
   SELECT COUNT(*) FROM holds 
   WHERE "ClassId" = @classId 
     AND ("Status" = 'CONFIRMED' OR ("Status" = 'HELD' AND "ExpiresAt" > CURRENT_TIMESTAMP));

   -- If available <= 0, ROLLBACK & abort (HTTP 409 Conflict)
   -- If available > 0, INSERT hold and COMMIT
   COMMIT;
   ```

3. **Step-by-Step Execution Sequence Across Processes:**
   - **Time $T_0$:** Process 1 (Port 8080) and Process 2 (Port 8081) receive simultaneous hold requests for Class `c_1` with 1 seat remaining.
   - **Time $T_1$:** Process 1 executes `SELECT ... FOR UPDATE` on Class `c_1`. PostgreSQL grants the tuple lock (`XMAX` lock) to Process 1's transaction.
   - **Time $T_2$:** Process 2 executes `SELECT ... FOR UPDATE` on Class `c_1`. PostgreSQL detects the row lock and halts Process 2, placing it in a **WAITING queue**.
   - **Time $T_3$:** Process 1 checks seat availability (1 available), inserts the hold, and executes `COMMIT`.
   - **Time $T_4$:** PostgreSQL releases the row lock and unblocks Process 2. Under `Read Committed`, Process 2 immediately re-evaluates the row and reads the committed state.
   - **Time $T_5$:** Process 2 counts active holds, observes that 0 seats remain, rolls back, and returns `409 Conflict`.

### Conclusion
Because the row lock is enforced inside the central PostgreSQL server, the invariant:
$$\text{heldSeats} + \text{confirmedSeats} \le \text{totalSeats}$$
is mathematically guaranteed regardless of how many backend instances or containers are deployed in parallel.
