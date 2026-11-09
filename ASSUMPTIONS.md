# Assumptions & Design Decisions

This document details all technical and architectural assumptions made during the implementation of the **School Admission System** for the Onnorokom Projukti Limited Take-Home Assignment. Each assumption documents where the assignment left behavior unspecified, the rationale behind the chosen design, and how the implementation adheres to it.

---

## 1. Student Identification (`X-Student-Id`)

- **What the assignment explicitly says:**  
  The assignment specifies that request identification is passed via the `X-Student-Id` header: *"Header X-Student-Id এর মান হবে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর।"* No login, registration, or password management is required.
- **What is not explicitly specified:**  
  The assignment does not specify the exact validation rules or the error status code when `X-Student-Id` is missing, empty, non-numeric, or contains fewer/greater than 17 digits on endpoints requiring student context.
- **What behavior was chosen:**  
  The system strictly validates the header against the regular expression `^[0-9]{17}$`. If the header is absent or does not contain exactly 17 numerical digits, the request is immediately rejected with HTTP `400 Bad Request` and a descriptive JSON error payload. For read-only class inspection (`GET /classes`, `GET /classes/{classId}`), the header is not required.
- **Why that behavior is reasonable:**  
  In Bangladesh, official Birth Registration Certificates (BRN) are strictly 17 digits. Validating this at the API boundary ensures that corrupted, malformed, or arbitrary strings never enter the database or corrupt hold ownership records.
- **How the implementation follows that assumption:**  
  Implemented in `StudentIdValidator.cs` and enforced across `POST /classes/{classId}/holds`, `POST /holds/{holdId}/confirm`, `DELETE /holds/{holdId}`, and `GET /me/holds`. Missing or invalid headers throw a `ValidationException`, which `ExceptionHandlingMiddleware` transforms into HTTP `400 Bad Request`.

---

## 2. Hold Ownership & Authorization

- **What the assignment explicitly says:**  
  The assignment states: *"Student নিজে চাইলে hold cancel করতে পারবে।"* and requires `GET /me/holds` to retrieve the current student's holds.
- **What is not explicitly specified:**  
  The assignment does not specify what happens if Student B attempts to confirm (`POST /holds/{holdId}/confirm`) or cancel (`DELETE /holds/{holdId}`) a hold originally reserved by Student A.
- **What behavior was chosen:**  
  Strict ownership verification is enforced. If the `X-Student-Id` header in the request does not match the `StudentId` associated with the hold record, the server denies the operation with HTTP `403 Forbidden`.
- **Why that behavior is reasonable:**  
  Allowing arbitrary applicants to confirm or cancel holds created by other applicants would allow denial-of-service attacks or seat hijacking between competing parents. Returning HTTP 403 correctly reflects that the client is identified but lacks authorization for that specific resource.
- **How the implementation follows that assumption:**  
  In `HoldService.ConfirmHoldAsync()` and `HoldService.CancelHoldAsync()`, the entity's `StudentId` is compared with the caller's ID:
  ```csharp
  if (hold.StudentId != studentId)
  {
      throw new ForbiddenException("You are not authorized to modify this hold.");
  }
  ```
  `ForbiddenException` maps directly to HTTP `403 Forbidden` in `ExceptionHandlingMiddleware`.

---

## 3. Hold Expiration Duration & Trigger Mechanism

- **What the assignment explicitly says:**  
  The assignment states: *"Seat কিছু সময়ের জন্য (যেমন ৫ মিনিট) hold থাকবে... মেয়াদ শেষ হলে seat নিজে থেকে ফেরত যাবে, কাউকে কোনো API call করতে হবে না।"*
- **What is not explicitly specified:**  
  The assignment does not specify whether the hold duration must be hardcoded or configurable, or whether expiration should be driven by an asynchronous background worker (cron/daemon) or evaluated dynamically upon query execution.
- **What behavior was chosen:**  
  - The hold duration defaults to 300 seconds (5 minutes) and is fully configurable via the `HOLD_DURATION_SECONDS` environment variable.
  - Expiration is evaluated **dynamically at query time** using UTC database timestamps (`ExpiresAt > DateTime.UtcNow`) rather than relying on a polling background worker.
- **Why that behavior is reasonable:**  
  A background worker running on a timer (e.g., every 30 or 60 seconds) would introduce latency where expired seats remain locked even though their timer has expired. Dynamic UTC evaluation guarantees **zero-latency seat reclamation**: the exact millisecond a hold lapses, the seat is instantly available to other applicants.
- **How the implementation follows that assumption:**  
  Active holds are calculated in SQL via `Status == 'HELD' AND ExpiresAt > CURRENT_TIMESTAMP`. In `HoldService.cs` and `ClassService.cs`, any hold whose `ExpiresAt <= UtcNow` is logically treated as expired immediately.

---

## 4. Hold Confirmation & Status Transition

- **What the assignment explicitly says:**  
  The assignment states: *"'ভর্তি ফি দিন' চাপলে ধরে নেবেন payment সফল হয়েছে। আসল payment gateway লাগবে না। Confirm হওয়ার পর seat permanent হয়ে যাবে, আর expire করবে না।"*
- **What is not explicitly specified:**  
  The assignment does not define what request body payload is required for `POST /holds/{holdId}/confirm`, or what status code to return if confirmation is attempted on an expired or cancelled hold.
- **What behavior was chosen:**  
  - `POST /holds/{holdId}/confirm` requires no request body (empty body or `{}`). Identity is extracted from `X-Student-Id` and the target hold from the URL path.
  - Attempting to confirm an expired hold returns HTTP `409 Conflict`.
  - Attempting to confirm a cancelled hold returns HTTP `409 Conflict`.
  - Successful confirmation transitions `Status` to `CONFIRMED`, removes the expiration constraint, and returns HTTP `200 OK`.
- **Why that behavior is reasonable:**  
  Since payment gateway integration is simulated, requiring a dummy payload in the POST body would add unnecessary complexity. Rejecting confirmation of expired holds with 409 Conflict is critical to prevent overselling seats that have already been reclaimed by other parents.
- **How the implementation follows that assumption:**  
  `HoldService.ConfirmHoldAsync()` verifies `hold.ExpiresAt > DateTime.UtcNow` and `hold.Status == HoldStatus.Held`. If valid, it updates `Status = HoldStatus.Confirmed` and commits the transaction.

---

## 5. Hold Cancellation & Deletion Semantics

- **What the assignment explicitly says:**  
  The assignment defines `DELETE /holds/{holdId}` allowing students to voluntarily release their hold.
- **What is not explicitly specified:**  
  The assignment does not specify the HTTP response code on successful cancellation, or the expected behavior when attempting to cancel an already confirmed admission or an already expired hold.
- **What behavior was chosen:**  
  - Successful cancellation returns HTTP `204 No Content` with an empty body.
  - Attempting to cancel an already `CONFIRMED` admission returns HTTP `409 Conflict` (confirmed admissions are permanent).
  - Attempting to cancel a hold that is already `CANCELLED` or `EXPIRED` returns HTTP `409 Conflict`.
- **Why that behavior is reasonable:**  
  Standard REST guidelines specify HTTP `204 No Content` for successful deletions that return no response body. Disallowing cancellation of confirmed admissions directly enforces the assignment rule: *"Confirm হওয়ার পর seat permanent হয়ে যাবে"*.
- **How the implementation follows that assumption:**  
  `HoldsController.CancelHold()` returns `NoContent()`. `HoldService.CancelHoldAsync()` validates that the hold is currently `HELD` and unexpired before transitioning `Status = HoldStatus.Cancelled`.

---

## 6. Expired Holds Handling & Dynamic Seat Recovery

- **What the assignment explicitly says:**  
  The assignment states: *"মেয়াদ শেষ হলে seat নিজে থেকে ফেরত যাবে, কাউকে কোনো API call করতে হবে না।"*
- **What is not explicitly specified:**  
  The assignment does not specify what `GET /holds/{holdId}` or `GET /me/holds` should display when queried for a hold whose 5-minute window has elapsed, or if an applicant whose hold expired is allowed to apply again.
- **What behavior was chosen:**  
  - When querying `GET /holds/{holdId}` or `GET /me/holds`, if the stored status is `HELD` but `ExpiresAt <= DateTime.UtcNow`, the API dynamically projects the status as `EXPIRED`.
  - Once a hold has expired, the student is allowed to create a new hold in that class (if seats are available).
- **Why that behavior is reasonable:**  
  If a student's 5-minute reservation expires, permanently barring them from applying for that class would be excessively harsh. Allowing re-application after expiration matches real-world ticketing and admission platforms.
- **How the implementation follows that assumption:**  
  `HoldService.MapToHoldDetail()` evaluates:
  ```csharp
  var status = hold.Status == HoldStatus.Held && hold.ExpiresAt <= DateTime.UtcNow
      ? HoldStatus.Expired
      : hold.Status;
  ```
  `CreateHoldAsync()` checks active holds and excludes expired ones:
  ```csharp
  h.Status == HoldStatus.Held && h.ExpiresAt > DateTime.UtcNow
  ```

---

## 7. Seat Availability Calculation & Dynamic Accounting

- **What the assignment explicitly says:**  
  The core invariant of the assignment is:  
  $$\text{held} + \text{confirmed} \le \text{totalSeats}$$  
  at all times, under all concurrent loads.
- **What is not explicitly specified:**  
  Whether available seats should be maintained via an incremented/decremented integer column on the `classes` table or calculated dynamically from the `holds` table.
- **What behavior was chosen:**  
  Seat availability is calculated dynamically on-the-fly:
  $$\text{availableSeats} = \max(0, \text{totalSeats} - (\text{confirmedSeats} + \text{activeHeldSeats}))$$
  where $\text{activeHeldSeats}$ only includes records where `Status = 'HELD'` and `ExpiresAt > CURRENT_TIMESTAMP`.
- **Why that behavior is reasonable:**  
  Maintaining a mutable counter column on `classes` requires continuous database updates whenever holds expire, introducing race conditions, counter drifts, and distributed locking overhead. Dynamic calculation combined with row-level locking guarantees single-source-of-truth accuracy at all times.
- **How the implementation follows that assumption:**  
  In `ClassService.cs` and `HoldService.cs`, seat counts are derived via:
  ```csharp
  var activeHeld = await _dbContext.Holds.CountAsync(h => 
      h.ClassId == classId && 
      h.Status == HoldStatus.Held && 
      h.ExpiresAt > now);
  var confirmed = await _dbContext.Holds.CountAsync(h => 
      h.ClassId == classId && 
      h.Status == HoldStatus.Confirmed);
  ```

---

## 8. Duplicate Operations & Idempotency

- **What the assignment explicitly says:**  
  The assignment states: *"একই আবেদনে confirm একাধিকবার এলেও confirmedSeats একবারই বাড়বে।"* and *"একই সময়ে একজন শিক্ষার্থীর একটা class-এ একটার বেশি active আবেদন বা নিশ্চিত ভর্তি থাকতে পারবে না।"*
- **What is not explicitly specified:**  
  What response code and payload should be returned when a confirmation is repeated, and how rapid double-clicks on hold creation should be handled.
- **What behavior was chosen:**  
  - **Idempotent Confirmation:** Calling `POST /holds/{holdId}/confirm` on an already confirmed hold returns HTTP `200 OK` with the confirmed hold details. `confirmedSeats` is not incremented again.
  - **Single Active Hold per Student per Class:** If a student submits a new hold request while already holding an active or confirmed seat in that class, the second request is rejected with HTTP `409 Conflict`.
  - **Applying to Different Classes:** A student is permitted to hold a seat in Class A and another in Class B simultaneously, as long as each class has at most one active application per student.
- **Why that behavior is reasonable:**  
  Guardians with poor internet connectivity may click "ভর্তি ফি দিন" multiple times; returning `200 OK` without side effects prevents UI failure banners while protecting data integrity.
- **How the implementation follows that assumption:**  
  In `HoldService.ConfirmHoldAsync()`:
  ```csharp
  if (hold.Status == HoldStatus.Confirmed)
  {
      return MapToHoldDetail(hold); // Idempotent 200 OK
  }
  ```
  In `HoldService.CreateHoldAsync()`, a check enforces the single active hold rule per `(studentId, classId)`.

---

## 9. Data Validation & Business Constraints

- **What the assignment explicitly says:**  
  The assignment lists class attributes: class name, shift, and total seats. Seat numbers must be positive.
- **What is not explicitly specified:**  
  Field length limits, acceptable ranges for `totalSeats`, whether shift is a separate column or part of the descriptive name, and whether duplicate class names are allowed.
- **What behavior was chosen:**  
  - `Name`: Required, trimmed, 1 to 200 characters (incorporates both class and shift, e.g., "Class 6 – Morning Shift").
  - `TotalSeats`: Required positive integer between 1 and 10,000.
  - A unique constraint is enforced on `Name`: creating two classes with identical names is disallowed and returns HTTP `409 Conflict`.
- **Why that behavior is reasonable:**  
  In school admissions, each grade and shift (e.g. "Class 6 – Morning Shift") is a distinct offering. Allowing duplicate classes with the same name causes applicant confusion and splits seat quotas. Bounding `TotalSeats` prevents zero or negative allocations.
- **How the implementation follows that assumption:**  
  `ClassService.CreateClassAsync()` validates inputs and checks `_dbContext.Classes.AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower())`. In `ApplicationDbContext.cs`, an explicit database unique index (`idx_classes_name_unique`) is enforced on `classes.Name`.

---

## 10. Error Handling & Response Contract

- **What the assignment explicitly says:**  
  The assignment specifies standard HTTP status codes:
  - `201 Created` for resource creation
  - `200 OK` for successful queries and operations
  - `204 No Content` for successful deletion
  - `400 Bad Request` for validation failures
  - `404 Not Found` for non-existent entities
  - `409 Conflict` when seats are unavailable or state transitions are invalid
- **What is not explicitly specified:**  
  The exact JSON schema for error response payloads across all error scenarios.
- **What behavior was chosen:**  
  A standardized, dual-compatible JSON error payload is returned for all 4xx and 5xx responses:
  ```json
  {
    "error": "Descriptive message explaining the cause of the failure.",
    "detail": "Descriptive message explaining the cause of the failure.",
    "status": 409,
    "title": "Conflict",
    "timestamp": "2026-11-09T12:00:00Z"
  }
  ```
- **Why that behavior is reasonable:**  
  Providing both `"error"` and RFC 7807 `"detail"` / `"title"` fields ensures that client libraries inspecting simple `body.error` and frontend components inspecting `body.detail` parse error messages reliably without failure.
- **How the implementation follows that assumption:**  
  Implemented via `ExceptionHandlingMiddleware.cs` in ASP.NET Core:
  - `ValidationException` $\rightarrow$ 400 Bad Request
  - `ForbiddenException` $\rightarrow$ 403 Forbidden
  - `NotFoundException` $\rightarrow$ 404 Not Found
  - `ConflictException` $\rightarrow$ 409 Conflict
  - Unhandled exceptions $\rightarrow$ 500 Internal Server Error

---

## 11. School Admin Persona & Resource Management

- **What the assignment explicitly says:**  
  The assignment requires features for both School Admin (creating classes, viewing seat statistics) and Guardian/Student (holding seats, confirming, cancelling). It also explicitly specifies: *"কোনো login, user registration বা role management লাগবে না।"*
- **What is not explicitly specified:**  
  How administrative endpoints (`POST /classes`) should be protected if login and role management are explicitly disallowed.
- **What behavior was chosen:**  
  In adherence to the "no authentication/login" constraint:
  - `POST /classes` is a public endpoint that does not require `X-Student-Id` or bearer tokens.
  - In the React frontend, the UI provides an administrative section (modal button: *"+ নতুন ক্লাস যোগ করুন"*) so that recruitment evaluators can create classes immediately without credential setup.
- **Why that behavior is reasonable:**  
  Introducing JWTs, session cookies, or API keys for admin actions would directly contradict the explicit instructions not to implement login or role management.
- **How the implementation follows that assumption:**  
  `ClassesController.CreateClass()` has no authorization attribute or header requirements, allowing direct access from the frontend's Admin modal.

---

## 12. Guardian/Student Persona & Client-Side Identity

- **What the assignment explicitly says:**  
  The assignment states: *"Header X-Student-Id এর মান হবে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর।"* The frontend must provide screens for viewing classes, holding seats, viewing hold details with a timer, confirming, cancelling, and viewing student holds.
- **What is not explicitly specified:**  
  How the 17-digit student identity is managed in the React UI across page navigations and browser refreshes.
- **What behavior was chosen:**  
  - The React frontend provides an identity management bar in the header where the user enters and can switch their 17-digit Student ID.
  - The ID is persisted in browser `localStorage` under the key `student_id`.
  - An Axios request interceptor automatically attaches the stored ID to the `X-Student-Id` header on every outgoing API request.
  - If no ID is set, the UI gracefully prompts the user to enter their 17-digit birth registration number before holding a seat.
- **Why that behavior is reasonable:**  
  Persisting the ID in `localStorage` prevents user frustration from losing identity on page reload, while allowing recruitment evaluators to test multiple student identities simultaneously by changing the number in the header.
- **How the implementation follows that assumption:**  
  Implemented in `frontend/src/context/StudentContext.tsx` and consumed across `ClassList.tsx`, `MyHolds.tsx`, and `HoldDetailModal.tsx`.

---

## 13. Entity Identifier Formats

- **What the assignment explicitly says:**  
  The assignment states: *"Example-এর ID (c_123, h_789) শুধু উদাহরণ, ID-এর format আপনার পছন্দমতো।"*
- **What is not explicitly specified:**  
  The exact string format or generation strategy for class and hold IDs.
- **What behavior was chosen:**  
  Deterministic prefixed strings:
  - Class IDs: `c_` followed by an 8-character lowercase hexadecimal UUID substring (e.g., `c_4a91fbc2`).
  - Hold IDs: `h_` followed by an 8-character lowercase hexadecimal UUID substring (e.g., `h_7d82e10a`).
- **Why that behavior is reasonable:**  
  Matches the assignment's example format while ensuring globally unique, URL-safe identifiers that prevent sequential enumeration attacks.
- **How the implementation follows that assumption:**  
  Generated in `ClassService.cs` (`$"c_{Guid.NewGuid():N}[..8]"`) and `HoldService.cs` (`$"h_{Guid.NewGuid():N}[..8]"`).

---

## 14. Bonus: Multi-Instance Concurrency Guarantee

- **What the assignment explicitly says:**  
  Bonus question: *"২টা backend instance আলাদা port-এ চলছে, কিন্তু database একটাই। এই অবস্থায় held + confirmed <= totalSeats কীভাবে নিশ্চিত করবেন? কোড বা আর্কিটেকচার ব্যাখ্যা দিয়ে বুঝিয়ে দিতে পারলে ভালো।"*
- **What is not explicitly specified:**  
  Which specific database concurrency control mechanism should be utilized.
- **What behavior was chosen:**  
  PostgreSQL row-level locking via `SELECT ... FROM classes WHERE "Id" = @classId FOR UPDATE` inside an explicit database transaction with `Read Committed` isolation.
- **Why that behavior is reasonable:**  
  - In-process locks (`lock`, `SemaphoreSlim`) fail completely across multiple backend processes because memory is not shared.
  - Distributed locks (e.g., Redis Redlock) introduce unnecessary third-party infrastructure.
  - PostgreSQL row-level locks natively serialize concurrent transactions at the database tuple level across any number of backend instances or Docker containers.
- **How the implementation follows that assumption:**  
  In `HoldService.CreateHoldAsync()`:
  ```csharp
  if (_dbContext.Database.IsRelational())
  {
      transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
      // Acquire exclusive row lock on the class row across all backend instances
      await _dbContext.Classes
          .FromSqlInterpolated($"SELECT * FROM classes WHERE \"Id\" = {classId} FOR UPDATE")
          .FirstOrDefaultAsync();
  }

  // Active seats counted under the row lock
  var activeHeld = await _dbContext.Holds.CountAsync(...);
  var confirmed = await _dbContext.Holds.CountAsync(...);

  if (activeHeld + confirmed >= schoolClass.TotalSeats)
  {
      if (transaction != null) await transaction.RollbackAsync();
      throw new ConflictException("No available seats in this class.");
  }

  // Insert hold and commit
  _dbContext.Holds.Add(hold);
  await _dbContext.SaveChangesAsync();
  if (transaction != null) await transaction.CommitAsync();
  ```
  When Instance A and Instance B simultaneously attempt to hold the last seat:
  1. Instance A acquires the row lock on Class `c_1`.
  2. Instance B attempts `SELECT ... FOR UPDATE` and is paused by PostgreSQL in a waiting queue.
  3. Instance A checks capacity (1 available), inserts hold `h_1`, and commits.
  4. Instance A's lock is released; PostgreSQL resumes Instance B.
  5. Instance B re-reads the committed state, counts active seats, detects 0 available, rolls back, and returns `409 Conflict`.
  
  Therefore, $\text{held} + \text{confirmed} \le \text{totalSeats}$ is strictly guaranteed across any number of concurrent instances.
