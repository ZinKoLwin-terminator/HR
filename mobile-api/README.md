# Apex HRMS Mobile API

The API of the employee mobile app (`hr_employee_app`). A host of its own, next to the web API in `api/`, sharing its
database and its HRMS Domain, Application and Infrastructure layers. The contract is
`hr_employee_app/docs/api_contracts.md` §1 and §2.

## Structure (Clean Architecture)

Dependencies point inwards only; `tests/Apex.Hrms.Mobile.UnitTests/Architecture` enforces it.

| Project | Layer | Holds |
| --- | --- | --- |
| `src/Apex.Hrms.Mobile.Domain` | Domain | `MobileSession`: refresh windows, rotation, reuse detection, security stamp. Plain C#. |
| `src/Apex.Hrms.Mobile.Application` | Application | Use cases (`LoginHandler`, `RefreshSessionHandler`, `LogoutHandler`, `GetMeHandler`, `ChangePasswordHandler`, `MobileAccessValidator`) returning `Result<T>` / `AppError`, and the ports they need (`IMobileSessionStore`, `IMobileEmployeeDirectory`, `IIdentityGateway`, `ITokenIssuer`). |
| `src/Apex.Hrms.Mobile.Infrastructure` | Infrastructure | `MobileDbContext` (only the columns used), `EfMobileSessionStore`, `EfMobileEmployeeDirectory`, `IdentityGateway` (ASP.NET Core Identity), `JwtTokenIssuer`, `MobileTokenSettings`. |
| `src/Apex.Hrms.MobileApi` | Presentation / host | `/api/v1/auth/*` endpoints, the `{ success, message, data, errors, traceId }` envelope, bearer authentication, rate limit, health probes. |
| `../api/src/*` | Shared | `Result`, `AppError`, `IAccountDirectory`, `SecurityPolicy`, Identity's `UserManager` (`AddPersistenceAndIdentity`). |

## Endpoints

| Method and path | Auth | Answer |
| --- | --- | --- |
| `POST /api/v1/auth/login` `{ userName, password, deviceId, deviceName }` | anonymous, rate limited | tokens + user. `userName` is the employee code or the work email. |
| `POST /api/v1/auth/refresh` `{ refreshToken, deviceId }` | anonymous, rate limited | new tokens (the refresh token is rotated) |
| `POST /api/v1/auth/logout` | bearer | 204; the device's session ends at once |
| `GET /api/v1/auth/me` | bearer | user, role and permissions |
| `POST /api/v1/auth/change-password` `{ currentPassword, newPassword }` | bearer, rate limited | 204; every other device and web session ends |
| `GET /health/live`, `GET /health/ready` | anonymous | `Healthy` / `Unhealthy` |

Sessions are rows of the existing `auth.UserToken` table (`LoginProvider = 'hrms-mobile'`, `Name` = device id). Only
SHA-256 hashes of refresh tokens are stored. No schema change was needed.

## Run

```bash
cd mobile-api
dotnet build Apex.Hrms.MobileApi.slnx    # zero warnings required
dotnet test Apex.Hrms.MobileApi.slnx     # no database needed
dotnet run --project src/Apex.Hrms.MobileApi   # http://localhost:5000, Development
```

Point the app at it with `--dart-define=API_BASE_URL=http://10.0.2.2:5000/api/v1 --dart-define=USE_MOCK_DATA=false`
(Android emulator).

## Configuration

| Key | Notes |
| --- | --- |
| `ConnectionStrings:Hrms` | The same database as the web API. |
| `MobileAuth:SigningKey` | **Required.** At least 32 random bytes in Base64. Development has a labelled development-only key; anywhere else set it with user-secrets or `MobileAuth__SigningKey`. The host does not start without it. |
| `MobileAuth:AccessTokenMinutes` / `RefreshTokenDays` / `SessionMaxDays` | 15 / 30 / 90 by default. |
| `MobileAuth:RateLimit:PermitsPerMinute` | Per client IP for login, refresh and change-password; 20 by default. |
| `Hosting:ForwardedHeaders` | As in the web API; enable behind a load balancer or every caller shares one rate-limit bucket. |
| `AllowedHosts` | Exact host names outside Development; wildcards refuse to start. |

## Not done yet

- No CI job builds or tests this solution, and no deployment exists for it.
- Attendance, leave, payslip, profile, notification and grievance endpoints for the app.
- A Flutter web build would need CORS.
