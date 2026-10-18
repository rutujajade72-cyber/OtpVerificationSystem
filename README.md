# One-Time Password Solution

A tool that generates a one-time password (OTP) from a **User Id** and a **date/time**, shows the
password with a live countdown, and validates it. Every password is valid for **30 seconds** and
can be used **once**.

| Part | Technology | Location |
| --- | --- | --- |
| Web API | ASP.NET Core 8 (.NET), layered architecture, dependency injection | `backend/src/Otp.Api` and the three layers below it |
| Frontend | React 18 + TypeScript + Vite | `frontend/` |
| Tests | xUnit (backend), Vitest + Testing Library (frontend) | `backend/tests`, `frontend/src/**/*.test.*` |

For how the code works internally (classes, algorithms, request flows, design decisions) read
[IMPLEMENTATION.md](IMPLEMENTATION.md). This file covers setup, running, configuration and the API.

---

## Contents

1. [Features](#1-features)
2. [Prerequisites](#2-prerequisites)
3. [Manual setup](#3-manual-setup)
4. [Using the application](#4-using-the-application)
5. [Configuration](#5-configuration)
6. [API reference](#6-api-reference)
7. [Running the tests](#7-running-the-tests)
8. [Project structure](#8-project-structure)
9. [Security notes](#9-security-notes)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Features

- **Generate** a 6-digit password from a User Id and a date/time (HMAC-SHA256 with RFC 4226 dynamic
  truncation). The same inputs always give the same code; a different user or a different instant
  gives a different one.
- **30-second validity**, enforced on the server clock. The date/time you send decides *which* code
  you get, never *how long* it lasts.
- **Live countdown** in the page: seconds remaining, a progress bar, a warning style for the last
  10 seconds, and a clear "Expired" state with the code struck through.
- **Validate** a password. A correct code is consumed, so it works exactly once.
- **Brute-force protection**: after 3 wrong codes within 60 seconds the user is locked out for the
  rest of that window and the password under attack is revoked. A per-client rate limit adds a
  second layer.
- Friendly errors: field validation, lockout wait times, network failures.
- Swagger UI for the API (Development only), structured logging that never contains a code,
  configuration that is validated at start-up.

## 2. Prerequisites

| Tool | Version | Check with |
| --- | --- | --- |
| .NET SDK | 10.0 or newer (the test projects target .NET 10; `backend/global.json` pins 8.0.100 and rolls forward to newer SDKs) | `dotnet --version` |
| Node.js | 18.18 or newer (20 or 22 recommended) | `node --version` |
| npm | Ships with Node.js | `npm --version` |

Both SDKs download packages from the internet the first time (NuGet and npm).

## 3. Manual setup

If you prefer to run each step yourself:

```bash
# Backend
cd backend
dotnet restore OtpVerificationSystem.sln
dotnet build OtpVerificationSystem.sln
dotnet test OtpVerificationSystem.sln

# Start the API (Development environment, http://localhost:5080)
dotnet run --project src/Otp.Api --launch-profile http
```

```bash
# Frontend (second terminal)
cd frontend
npm install          # or: npm ci, when a package-lock.json is present
npm test
npm run dev          # http://localhost:5173
```

In development the browser only talks to Vite (port 5173). Vite forwards every `/api/*` request to
the API on port 5080 (see `frontend/vite.config.ts`), so the app is same-origin and needs no CORS
setup. To point the proxy somewhere else: `API_PROXY_TARGET=http://localhost:6000 npm run dev`.

Visual Studio users can open `backend/OtpVerificationSystem.sln` and run the `Otp.Api` project (profiles
`http` and `https` are defined in `Properties/launchSettings.json`).

## 4. Using the application

1. **Generate.** Enter a User Id (up to 30 characters). The date and time default to now; press
   **Now** to reset them. Press **Generate password**.
2. The password appears with a countdown. The progress bar drains over 30 seconds and turns to the
   warning colour for the last 10. At zero the page shows **Expired**, strikes the code through and
   tells you to generate a new one.
3. **Validate.** The User Id is pre-filled. Enter the password (the field accepts exactly as many digits as the
   generated code, 6 by default) and press **Validate password**. The result is shown as *Valid.* or *Not valid.* with the reason.

Things to try while testing:

| Try this | Expected result |
| --- | --- |
| Generate, then validate immediately | Valid |
| Validate the same code a second time | Not valid: no active password (single use) |
| Wait for the countdown to reach 0, then validate | Not valid: expired |
| Validate a wrong code three times | Attempts remaining 2, then 1, then locked out until 60 seconds after the first wrong code |
| Press Generate while locked out | Error with the number of seconds to wait |
| Generate twice for the same user | The first password stops working; only the newest is valid |
| Leave the User Id empty | Inline validation message, no request is sent |
| Stop the API and press Generate | "Unable to reach the OTP service" message |

## 5. Configuration

All settings live in `backend/src/Otp.Api/appsettings.json`. Any setting can be overridden by an
environment variable: replace `:` with `__` (for example `Otp__SecretKey`). Out-of-range values stop
the application at start-up with a message that names the setting.

### One-time password (`Otp` section)

| Setting | Default | Allowed | Meaning |
| --- | --- | --- | --- |
| `SecretKey` | empty | at least 32 characters | HMAC key. **Required.** `appsettings.Development.json` supplies a development-only key; every other environment must set its own. |
| `ValiditySeconds` | 30 | 5 to 300 | How long a password stays valid. |
| `CodeLength` | 6 | 4 to 9 | Digits per code. |
| `MaxFailedAttempts` | 3 | 1 to 10 | Wrong codes inside the window that trigger a lockout. |
| `LockoutSeconds` | 60 | 10 to 86400 | Sliding failure window, which is also the lockout length. |
| `CleanupIntervalSeconds` | 60 | 5 to 3600 | How often expired data is purged from memory. |

### Rate limiting (`RateLimiting` section)

| Setting | Default | Allowed | Meaning |
| --- | --- | --- | --- |
| `PermitLimit` | 30 | 1 to 100000 | Requests one client IP may send per window, both endpoints combined. |
| `WindowSeconds` | 60 | 1 to 3600 | Window length. |

### CORS (`Cors` section)

`Cors:AllowedOrigins` is a list of browser origins that may call the API. It contains
`http://localhost:5173` in Development and is empty otherwise. It is only needed when the UI is
served from a different origin than the API, for example `Cors__AllowedOrigins__0=https://otp.example.com`.

### Frontend (`frontend/.env.local`, optional)

`VITE_API_BASE_URL` is the API's base URL for a production build that is served from a different
origin. Leave it empty in development.

`VITE_OTP_CODE_LENGTH` is the number of digits the validate field accepts before a password has been
generated (default 6). If you change `Otp:CodeLength` in the API, set the same value here. After a
password is generated the field follows that password's real length. See `frontend/.env.example`.

### Running outside Development

```bash
export ASPNETCORE_ENVIRONMENT=Production
export Otp__SecretKey="$(openssl rand -base64 48)"     # 32+ characters, kept secret
export Cors__AllowedOrigins__0="https://otp.example.com"
dotnet run --project backend/src/Otp.Api -c Release
```

Build the frontend with `npm run build` and serve `frontend/dist` from any static host or reverse
proxy that also forwards `/api` to the API.

## 6. API reference

Base URL in development: `http://localhost:5080`. Bodies are JSON (`Content-Type: application/json`).
Property names are camelCase. Errors use the RFC 7807 `application/problem+json` format.

### `POST /api/otp/generate`

Request:

```json
{ "userId": "john.doe", "dateTime": "2026-10-16T12:30:00.000Z" }
```

| Field | Rules |
| --- | --- |
| `userId` | Required, 1 to 30 characters, not only whitespace. Surrounding whitespace is trimmed. |
| `dateTime` | Required ISO 8601 date and time. The code is derived from the *instant*, so `14:30+02:00` and `12:30Z` give the same code. |

`200 OK`:

```json
{
  "userId": "john.doe",
  "code": "286130",
  "issuedAt": "2026-10-17T10:00:00+00:00",
  "expiresAt": "2026-10-17T10:00:30+00:00",
  "validitySeconds": 30,
  "remainingSeconds": 30
}
```

| Status | When |
| --- | --- |
| 200 | The password was generated. It replaces any earlier password of the same user. |
| 400 | Validation failed (missing, blank or too long `userId`, missing or malformed `dateTime`, body is not JSON). |
| 415 | The request is not `application/json`. |
| 429 | The user is locked out (response carries `Retry-After`), or the client exceeded the rate limit. |
| 500 | Unexpected error. The body is generic; details are only in the server log. |

### `POST /api/otp/validate`

Request:

```json
{ "userId": "john.doe", "code": "286130" }
```

| Field | Rules |
| --- | --- |
| `userId` | Required, 1 to 30 characters. |
| `code` | Required, 4 to 9 digits (the allowed range of `Otp:CodeLength`). Surrounding whitespace is ignored. A code of the wrong length is simply reported as `Invalid`; the UI only lets the user enter the configured length. |

`200 OK` (always, for a well-formed request: the outcome is in the body):

```json
{
  "isValid": false,
  "status": "Invalid",
  "message": "The one-time password is incorrect. 2 attempt(s) remaining.",
  "remainingAttempts": 2,
  "retryAfterSeconds": null
}
```

| `status` | Meaning | Extra fields |
| --- | --- | --- |
| `Valid` | Correct code. It is now consumed. | |
| `Invalid` | Wrong code. | `remainingAttempts` |
| `Expired` | The 30 seconds passed (the boundary is inclusive: at exactly 30 s it is expired). | |
| `NotFound` | The user has no active password, or it was already used. | |
| `AttemptsExceeded` | Locked out. The password was revoked and every attempt is refused until the lockout ends. | `retryAfterSeconds` |

Other statuses: `400` for a malformed request, `415`, `429` (rate limit), `500`.

### Example with curl

```bash
curl -s -X POST http://localhost:5080/api/otp/generate \
  -H "Content-Type: application/json" \
  -d '{"userId":"john.doe","dateTime":"2026-10-16T12:30:00.000Z"}'

curl -s -X POST http://localhost:5080/api/otp/validate \
  -H "Content-Type: application/json" \
  -d '{"userId":"john.doe","code":"286130"}'
```

`backend/src/Otp.Api/Otp.Api.http` contains the same requests for Visual Studio and VS Code
(REST Client).

## 7. Running the tests

```bash
# Backend: 212 test methods (277 cases counting Theory rows) in four projects
cd backend
dotnet test OtpVerificationSystem.sln
dotnet test OtpVerificationSystem.sln --collect:"XPlat Code Coverage"   # with coverage

# Frontend: 101 test definitions
cd frontend
npm test                  # run once
npm run test:watch        # watch mode
npm run test:coverage     # coverage report in frontend/coverage
npm run typecheck         # TypeScript
npm run lint              # ESLint
npm run format:check      # Prettier (npm run format fixes)
```

| Project | What it covers |
| --- | --- |
| `Otp.Domain.Tests` | Entity rules: expiry boundary, constant-time match, sliding-window lockout maths, thread safety. |
| `Otp.Application.Tests` | Use cases against the real in-memory stores and a fake clock: all five outcomes, single use, lockout and recovery, races, logging never contains the code, options validation, DI. |
| `Otp.Infrastructure.Tests` | HMAC generator (including reference vectors computed independently), repositories under concurrency, the cleanup job with a fake clock. |
| `Otp.Api.IntegrationTests` | The full HTTP pipeline in memory: both endpoints, validation errors, lockout over HTTP, rate limiting, CORS, error handling, Swagger. |
| Frontend tests | Date helpers, the API client with a stubbed `fetch`, `useCountdown` and `useOtp`, every component, and the whole `App` including the 30-second countdown with a fake clock. |

The backend tests use a fake clock (`FakeTimeProvider`), so none of them waits for real time.

## 8. Project structure

```
OtpVerificationSystem/
  README.md                  this file
  IMPLEMENTATION.md          how the solution is built
  .editorconfig              formatting, C# style and naming rules
  backend/
    OtpVerificationSystem.sln
    global.json                       pins the .NET SDK
    Directory.Build.props             settings shared by every project (warnings are errors)
    Directory.Packages.props          every NuGet version, declared once
    src/
      Otp.Domain/                     business rules, no dependencies
      Otp.Application/                use cases, ports (interfaces), options
      Otp.Infrastructure/             HMAC generator, in-memory stores, cleanup job
      Otp.Api/                        controller, contracts, rate limiting, CORS, Program.cs
    tests/
      Otp.Domain.Tests/
      Otp.Application.Tests/
      Otp.Infrastructure.Tests/
      Otp.Api.IntegrationTests/
  frontend/
    package.json, vite.config.ts, tsconfig*.json, eslint.config.js
    src/
      api/        typed API client and error type
      components/ OtpGeneratorForm, OtpDisplay, OtpValidatorForm
      hooks/      useOtp (state and API calls), useCountdown (timer)
      utils/      date helpers
      types/      API contracts
      App.tsx, main.tsx, index.css
```

Dependencies point inward only: `Api -> Application -> Domain`, and `Infrastructure -> Application`.
`Domain` references nothing. Details in [IMPLEMENTATION.md](IMPLEMENTATION.md#4-backend-architecture).

## 9. Security notes

- Codes come from HMAC-SHA256 with a **server-side secret**, so they cannot be predicted from the
  inputs. Keep `Otp:SecretKey` out of source control.
- Code comparison is constant-time. Logs identify the user but **never the code**.
- Wrong guesses are limited per user (lockout) and per client (rate limit).
- Unhandled errors return a generic message; stack traces stay in the server log.
- Swagger is only exposed in Development. CORS allows only the origins you configure, and only POST.
- Passwords live in process memory. They are lost when the API restarts, which is acceptable for a
  30-second credential. The stores are behind interfaces so a shared store can replace them.
- When running behind a reverse proxy or load balancer, configure ASP.NET Core forwarded headers so
  the rate limiter sees the real client address instead of the proxy's.

## 10. Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| `The .NET SDK ... is too old` or `A compatible .NET SDK was not found` | Install the .NET 8 SDK (or newer) from <https://dotnet.microsoft.com/download>. |
| `error TESTRUNABORT: Test Run Aborted` for every test project, or `You must install or update .NET to run this application` | The test projects target .NET 10, so they need the .NET 10 SDK (which includes the runtime). Run `dotnet --list-sdks` and install the .NET 10 SDK from <https://dotnet.microsoft.com/download> if no 10.x SDK is listed. |
| The API stops at start-up with `OptionsValidationException ... SecretKey` | Outside Development you must set `Otp__SecretKey` (32+ characters). In Development it comes from `appsettings.Development.json`. |
| `Port 5173 is already in use` | Another Vite instance is running. Stop it, or edit `server.port` in `frontend/vite.config.ts`. |
| `address already in use` on port 5080 | Another API instance is running, or change the port in `Properties/launchSettings.json` and `API_PROXY_TARGET`. |
| UI shows "Unable to reach the OTP service" | The API is not running or the proxy target is wrong. Open <http://localhost:5080/swagger> to check the API. |
| Browser console: CORS error | Only happens when the UI is served from an origin other than the API's. Add the origin to `Cors:AllowedOrigins`. |
| Every user gets `429 Too many requests` behind a proxy | All requests appear to come from the proxy's IP. Configure forwarded headers, or raise `RateLimiting:PermitLimit`. |
| `dotnet restore` fails with network errors | NuGet needs internet access (or a configured mirror) the first time. |
| `npm ci` complains about a missing lock file | Run `npm install` instead; it creates `package-lock.json`. The scripts do this automatically. |
| HTTPS profile shows a certificate warning | Run `dotnet dev-certs https --trust` once, or use the `http` profile. |
| `npm run format:check` lists files | Run `npm run format` once to normalise formatting. |
| The validate field stops at the wrong number of digits | You changed `Otp:CodeLength` in the API. Set `VITE_OTP_CODE_LENGTH` in `frontend/.env.local` to the same value and restart `npm run dev`. |
