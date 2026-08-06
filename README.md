# Mood Tracker

A web application for tracking a team's daily mood. Each person records how they are feeling once
per day, optionally with a comment, and an administrator can review every entry.

Built on the supplied starter template: C# / ASP.NET Core (.NET 8), Angular 18, MySQL 8.4,
containerised with Docker Compose.

---

## Running the application

Docker Desktop (or Docker Engine with the Compose plugin) is the only prerequisite. From the
directory containing `docker-compose.yml`:

```bash
docker compose build
docker compose up
```

Then open **http://localhost:4200**.

| Service  | URL                            | Notes                                  |
| -------- | ------------------------------ | -------------------------------------- |
| Frontend | http://localhost:4200          | The mood tracker                       |
| API      | http://localhost:8080          | ASP.NET Core                           |
| Swagger  | http://localhost:8080/swagger  | Interactive API documentation          |
| Health   | http://localhost:8080/health   | Readiness probe                        |
| MySQL    | localhost:3306                 | `app` / `password`, db `moodtrackerdb` |

The database schema is created automatically on first start — EF Core migrations are applied at
startup, and the API retries while MySQL finishes initialising.

### Admin sign-in

Navigate to **http://localhost:4200/admin/login** or use the *Admin* link in the header.

| Username | Password    |
| -------- | ----------- |
| `admin`  | `Admin123!` |

These are seeded from environment variables in `docker-compose.yml` on first run. See
[Security notes](#security-notes) for why they are in the file and what a real deployment would do
differently.

### Useful commands

```bash
docker compose up -d          # start detached
docker compose logs -f web    # follow API logs
docker compose down           # stop; mood entries are preserved in the named volume
docker compose down -v        # stop and wipe the database for a clean run
```

---

## Running without Docker

Useful for a faster development loop. Requires the .NET 8 SDK, Node 20+, and a reachable MySQL
instance.

```bash
# API — http://localhost:8080
cd InterviewProjectTemplate
dotnet run

# Frontend — http://localhost:4200
cd Client/web-client
npm install
npm start
```

`appsettings.Development.json` points at `localhost:3306` and supplies development-only auth
settings, so `dotnet run` works against a locally running MySQL without further configuration. The
quickest way to get one is `docker compose up mysql-db`.

## Running the tests

```bash
# Backend — 64 tests
dotnet test

# Frontend — 19 tests
cd Client/web-client
npm run test:ci
```

Both suites pass. The frontend script uses a headless Chrome launcher configured with `--no-sandbox`
so it also runs inside a container.

---

## Verified behaviour

The following was checked against the running Docker Compose stack, not just against unit tests:

| Check | Result |
| ----- | ------ |
| `GET /health` | 200 after migrations apply |
| `GET /api/moods/options` | 4 options, exact wording from the brief |
| `GET /api/moods/today` | 200, issues the `mood_tracker_user` cookie |
| `POST /api/moods` (first) | **201 Created** |
| `POST /api/moods` (same cookie, same day) | **409 Conflict** |
| `POST /api/auth/login` | 200 with a JWT |
| `GET /api/admin/moods` with token | 200, entry listed |
| `GET /api/admin/moods` without token | **401 Unauthorized** |
| `http://localhost:4200/` | 200, Angular app served |
| `http://localhost:4200/admin` (deep link) | 200 — nginx SPA fallback works |
| MySQL startup ordering | API waited for `service_healthy`, no failed first connection |
| `Australia/Melbourne` resolution | Resolved in-container; no UTC fallback warning logged |
| Admin password at rest | Stored as `210000.<salt>.<hash>`, never plaintext |

The database-level guarantee was confirmed by bypassing the application entirely and inserting a
duplicate directly in SQL:

```
ERROR 1062 (23000): Duplicate entry '<userkey>-2026-08-06'
for key 'MoodEntries.IX_MoodEntries_UserKey_MoodDate'
```

That is the once-per-day rule being enforced by the schema rather than by C#, which is the design
decision I would most want a reviewer to notice.

---

## How the requirements are met

| Requirement                                | Where                                                                                  |
| ------------------------------------------ | -------------------------------------------------------------------------------------- |
| Ask how the user feels, store in a database | `MoodsController.Create` → `MoodService` → `MoodEntries` table                          |
| Four options with the exact wording         | `MoodRatingLabels`, served via `GET /api/moods/options`                                |
| Optional comment                            | `MoodEntry.Comment`, nullable, max 1000 characters                                     |
| Once per day, no authentication             | Anonymous `HttpOnly` cookie + unique index on `(UserKey, MoodDate)`                    |
| Error message on a repeat attempt           | API returns `409 Conflict`; the UI shows the message from the response                 |
| Admin-only page, most recent first          | `AdminMoodsController` behind `[Authorize(Roles = "Admin")]`, ordered by `CreatedAtUtc` |

### API

| Method | Route                 | Auth       | Purpose                              |
| ------ | --------------------- | ---------- | ------------------------------------ |
| `GET`  | `/api/moods/options`  | Anonymous  | The four mood options with labels    |
| `GET`  | `/api/moods/today`    | Anonymous  | Whether the caller submitted today   |
| `POST` | `/api/moods`          | Anonymous  | Record today's mood                  |
| `POST` | `/api/auth/login`     | Anonymous  | Exchange admin credentials for a JWT |
| `GET`  | `/api/admin/moods`    | Admin JWT  | All entries, paged, newest first     |

---

## Design decisions

### Identifying a user without authentication

This is the interesting constraint: the once-per-day rule needs a stable identity, but the brief
forbids authentication for it.

On first contact the API issues a GUID in an **`HttpOnly`, `SameSite`-scoped cookie**. Page
JavaScript cannot read it, so an XSS bug cannot lift or rewrite someone's identity — which a
`localStorage` value would allow. The rule itself is enforced by a **unique index on
`(UserKey, MoodDate)`**, not by an application-level check.

That distinction matters. `MoodService.CreateAsync` does check for an existing entry first, but only
to produce a clean error on the common path; two simultaneous requests could both pass that check.
The unique index is what actually holds the line, and the resulting `DbUpdateException` is caught and
translated into the same `DuplicateMoodEntryException`. `MoodServiceTests` covers both routes,
including a test that bypasses the service entirely to prove the schema rejects a duplicate.

**Honest limitation:** this is a browser identity, not a person. Clearing cookies, opening a private
window, or switching device yields a new identity and permits another entry today. That is inherent
to "no authentication" — without an account there is nothing durable to bind to. It is the right
trade-off for a good-faith internal tool, and the wrong one if the data mattered adversarially.

### The calendar-day boundary

"Once per day" is a calendar-day rule, so it must be evaluated in the team's time zone rather than
UTC — in Melbourne, a 9am submission is on the previous UTC day. `IDateTimeProvider` exposes `Today`
in a configurable zone (`MoodTracker__TimeZone`), which also makes the rule testable at a fixed date
rather than dependent on when the suite runs.

### Admin authentication

An admin user is seeded on first start with a **PBKDF2-HMAC-SHA256** hashed password (210,000
iterations, per-user salt, iteration count stored in the hash so the work factor can be raised
later). Login returns a short-lived JWT carrying an `Admin` role claim; admin endpoints require it.

Full ASP.NET Core Identity was considered and rejected: for a single administrator it adds seven
tables and a lot of surface area without improving this solution. A constant-time comparison is used
on verification, and the unknown-user path still performs a hash so response timing does not reveal
which usernames exist.

### Not exposing raw user keys to the admin

The admin report shows an 8-character SHA-256 prefix of the user key, not the key itself. An admin
can still see that two entries came from the same person, but a leaked report does not hand anyone a
working identity cookie.

### Layering

`Domain` → `Application` → `Infrastructure`, with controllers as a thin HTTP layer. Kept as folders
inside one project rather than four assemblies: at this size the extra projects would be ceremony,
and the namespaces still make the dependency direction obvious. Business rules live in
`MoodService`, so they are testable without spinning up a web host.

### Testing approach

Tests run against **SQLite** rather than EF Core's InMemory provider, because InMemory does not
enforce unique indexes — it would happily accept two entries for the same user and day, which is
precisely the rule under test. A test that cannot fail is worse than no test.

---

## Fixes made to the template

The starter project needed several corrections to work as specified:

| Issue                                                                                                | Fix                                                     |
| ---------------------------------------------------------------------------------------------------- | ------------------------------------------------------- |
| **Angular 15.2 shipped**, though the brief specifies Angular 18                                       | Upgraded to Angular 18.2, standalone components         |
| **`UseHttpsRedirection`** would 307 every API call to port 8081, which has no certificate in the container | Removed; the container serves HTTP and TLS terminates upstream |
| **Angular Dockerfile copied `dist/web-client`**, but the Angular 17+ `application` builder emits to `dist/web-client/browser` | Corrected the copy path                                 |
| **No nginx SPA fallback**, so loading or refreshing `/admin` returned 404                              | Added `nginx.conf` with `try_files … /index.html`       |
| **`environment.apiUrl`** pointed at `https://localhost:44392`, an IIS Express port nothing binds       | Points at `http://localhost:8080`                       |
| **Connection string** in `appsettings.json` was missing its `Server=` prefix                           | Corrected                                               |
| **CORS allowed any origin**, which browsers reject for credentialed requests — and the identity cookie is credentialed | Explicit origin list with `AllowCredentials()`          |
| **`InvariantGlobalization=true`** would break time zone lookup                                        | Disabled, and `tzdata` installed in the runtime image   |
| **No test project**                                                                                   | Added xUnit project, 64 tests                           |
| No `.dockerignore`, so a Windows-built `node_modules` could be copied into a Linux image               | Added for both services                                 |
| No nginx config at all, so `docker compose up` served a directory listing rather than the app          | Added `nginx.conf` with caching rules and SPA fallback   |
| **Alpine build image could not install `lmdb`** — its prebuilt binaries target glibc, and Alpine has neither a matching binary nor Python/C++ to compile one | Build stage uses Debian `node:20`; runtime stage is still `nginx:alpine`, so the shipped image stays small |
| **npm 10 (shipped in `node:20`) writes a lockfile it then refuses to validate** — `sass` needs chokidar 4, `karma` needs chokidar 3, and `npm ci` failed its own consistency check | Pinned `npm@11.19.0` in the build stage, which resolves the split correctly |
| **Lockfile was generated against a bind-mounted Windows `node_modules`**, recording `@lmdb/lmdb-win32-x64` and no Linux binary | Regenerated in a clean container directory so all platform binaries are recorded |

---

## Security notes

The admin password and JWT signing key are literal values in `docker-compose.yml`. That is a
deliberate, and in any other context indefensible, choice: the brief requires the stack to come up
from two commands with no setup, and a reviewer cannot supply secrets they have not been given. They
are configuration-driven, so a real deployment injects them from a secret store with no code change.
Startup validation rejects a missing or too-short signing key rather than falling back to a default.

Also worth stating plainly:

- **Migrations run at application startup.** Convenient here, wrong for production — a rolling
  deployment would have several instances racing to alter the same schema. This belongs in a release
  pipeline.
- **Swagger is exposed in all environments** so the API is explorable during review. It would be
  gated in production.
- **`Secure` cookies are off by default** because the assessment runs over plain HTTP on localhost.
  `MoodTracker__UseSecureCookies=true` enables `Secure` + `SameSite=None` for a TLS deployment.
- **The admin JWT is held in `sessionStorage`**, which is readable by any script on the page. See
  next steps.

---

## What I would do next

Roughly in the order I would pick them up:

1. **Integration tests over the real HTTP pipeline.** The current tests cover services and
   controllers in isolation. `WebApplicationFactory` with Testcontainers-backed MySQL would prove the
   cookie round-trip, the CORS configuration, and the JWT middleware — the parts most likely to break
   in the container and least covered today.
2. **Move the admin session to an `HttpOnly` cookie.** The anonymous identity is already protected
   this way; the admin token, ironically, is not. Doing this properly means refresh tokens and CSRF
   protection, which is why it did not fit here.
3. **Rate-limit the login endpoint.** Nothing currently slows down credential stuffing.
4. **A real admin dashboard.** Trends over time, filtering by date range, per-person history, CSV
   export. The current page is a correct table with a per-page breakdown; it is not yet an insight
   tool, and mood data is only useful if someone can see a trend.
5. **Argon2id instead of PBKDF2.** PBKDF2 is acceptable and needs no extra dependency, which is why I
   used it; Argon2id is the better modern choice.
6. **CI pipeline.** Build, both test suites, and `docker compose build` on every push. The container
   build is verified now, but only manually — and the three portability bugs I hit are exactly the
   class of thing a clean-room CI build catches and a developer machine hides.
7. **Structured logging and correlation IDs.** Currently the default console logger.
8. **Accessibility audit.** The forms use real radio groups, labels, `role="alert"` and visible focus
   rings, but I have not run axe or tested with a screen reader.

---

## Self-critique

The brief asks for an objective view of the work, so:

- **The container build took four attempts to get right.** `docker compose build` and
  `docker compose up` now work from clean, and the running stack is verified end to end (see
  [Verified behaviour](#verified-behaviour)). But the frontend image failed three times first, and
  every failure was a portability problem invisible from a passing local build: an npm-version-skewed
  lockfile, Alpine's musl libc versus `lmdb`'s glibc binaries, and a lockfile generated against a
  bind-mounted host `node_modules` that recorded a Windows binary and no Linux one. Worth stating
  plainly because it is the lesson of the exercise: "it builds on my machine" and "it builds in the
  container" are different claims, and only the second one is what gets reviewed.
- **The comment field is stored and displayed verbatim.** Angular escapes interpolated text so this
  is not an XSS vector, but there is no length-aware truncation or profanity handling, and the admin
  table will render an awkwardly long comment poorly.
- **The mood summary on the admin page counts only the current page**, which is nearly useless for a
  real report. It is labelled as such rather than being quietly misleading, but a proper aggregate
  query would have been better than a caveat.
- **`IsUniqueConstraintViolation` matches on exception message text.** It works across MySQL and
  SQLite and keeps the code provider-agnostic, but string matching on a driver message is fragile —
  a provider upgrade could change the wording. Checking the MySQL error number (1062) would be more
  precise at the cost of coupling the service to a provider.
- **No pagination on the frontend beyond previous/next.** Fine for a small team, not for a year of
  data.
- **The Angular upgrade was done by editing the manifests directly** rather than by running
  `ng update`. The result builds and tests clean, but `ng update` would have applied schematics I may
  have missed.
- **`DatabaseInitialiser` retries for up to a minute and then rethrows.** Correct behaviour, but the
  retry is a fixed delay rather than exponential backoff, and it duplicates the final attempt in a
  way I am not fond of.

---

## Project structure

```
├── docker-compose.yml
├── InterviewProjectTemplate/            # ASP.NET Core API
│   ├── Domain/                          # Entities and enums; no dependencies
│   ├── Application/                     # Services, DTOs, options, abstractions
│   ├── Infrastructure/                  # EF Core, security, identity, error handling
│   ├── Controllers/                     # HTTP layer
│   └── Dockerfile
├── InterviewProjectTemplate.Tests/      # xUnit — 64 tests
└── Client/web-client/                   # Angular 18
    ├── src/app/core/                    # Models, services, guard, interceptor
    ├── src/app/features/                # Mood tracker and admin screens
    ├── nginx.conf
    └── Dockerfile
```
