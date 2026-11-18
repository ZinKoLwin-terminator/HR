# Deployment guide (host-neutral)

Status: hosting is **not decided**. This guide describes what any host must provide. Nothing here deploys
anything. A human runs every step, and no secret or production connection string belongs in the repository. The
live GitHub Pages demo is the mock-data UI only (`.github/workflows/ci.yml`); it is not this deployment.

## 1. Build and publish

API (from `api/`):

```
dotnet publish src/Apex.Hrms.Api -c Release -o ./publish
```

Run it with `dotnet Apex.Hrms.Api.dll` (needs the ASP.NET Core 10 runtime) or as a service. Set
`ASPNETCORE_ENVIRONMENT=Production` (the default when unset, but set it explicitly).

UI (from `ui/`): `npm ci`, then a build whose data layer is the real API. The `api` build configuration replaces
`api-data-options.ts` with `baseUrl: '/api'`, so the UI calls **`/api` on its own origin**. The Angular dev proxy
(`ui/proxy.conf.json`, target `http://localhost:5045`) exists only for `ng serve`; it is not part of the output.

The session cookie is `__Host-hrms` (Secure, SameSite=Strict, no Domain), so UI and API must be served from the
**same origin** (same scheme, host and port). Two ways:

* **Reverse proxy** (recommended): the proxy serves the UI's static files (`ui/dist/hrms/browser`) and forwards
  `/api/*` to the API. Add a fallback to `index.html` for deep links (the SPA uses client routing).
* **API as the web server**: the API has no static file or SPA fallback middleware today (gap, see section 8).

Known gap: the `api` configuration in `ui/angular.json` has `optimization: false` and source maps on (it is a
development-style build, used for local API work). `--configuration production,api` does not fix it because `api`
wins. A production API-mode build needs a new configuration (for example `api-production`: production budgets and
hashing plus the `api` file replacement). Add it before the first real deployment. Do not ship source maps.

## 2. Configuration (environment variables)

`api/src/Apex.Hrms.Api/appsettings.json` holds safe defaults. Use environment variables (`__` for `:`, `__0` for list
items) or the host's secret store. Configuration is read at startup; restart after changing it.

| Variable | Notes |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT=Production` | Selects SMTP mail, `__Host-` cookies, Secure cookies, HSTS and HTTPS redirection, exact `AllowedHosts`. OpenAPI is off. |
| `ConnectionStrings__Hrms` | **Required.** Use the least-privilege runtime account only (below). No fallback exists. Example shape (no values): `Server=<host>;Database=<db>;User Id=<login>;Password=<from secret store>;Encrypt=True`. Prefer Windows auth with a gMSA where possible. |
| `AllowedHosts` | Exact host names separated by `;`. Wildcard or empty stops startup outside Development. The base file says `localhost`: override it. |
| `Cors__AllowedOrigins__0` | Same-origin needs no CORS. Note: arrays merge by index, so the base file's `http://localhost:4200` stays at index 0 unless you override `__0`. |
| `Email__Smtp__Host`, `Email__Smtp__FromAddress` | **Required** for password-reset mail. Optional: `Port` (587), `EnableSsl` (true), `FromName`. |
| `Email__Smtp__UserName`, `Email__Smtp__Password` | Both or neither. Secret store only. |
| `DataProtection__KeysPath` | Absolute directory for the key ring (see below). Default is `App_Data/keys` under the content root, which is wrong for a published folder that gets replaced. |
| `Hosting__ForwardedHeaders__Enabled=true` | Only behind a reverse proxy. Then fill `KnownProxies__0..` and/or `KnownNetworks__0..` (CIDR). Enabled with neither list stops startup. Leave off when Kestrel faces clients directly. |
| `Auth__RememberMeMaxDays` | Hard end of a remember-me session, 1 to 365, default 30 (confirm the value, FUTURE-TODO 3). |
| `Auth__RateLimit__PermitsPerMinute` | Login, forgot- and reset-password per client IP, default 10. |
| `Logging__LogLevel__Default` etc. | `Warning` default, `Apex.Hrms` at `Information`. Never log at Debug/Trace in production (SQL text and parameters). |
| `Business__TimeZone` | Default `Asia/Yangon`. |
| `Kpi__EvidenceFolder` | Absolute directory for the KPI evidence files (uploads on scorecard items), kept **permanently**. Configured paths: `D:\HrmsData\kpi-evidence` in `appsettings.json` (production and any non-Development environment) and `D:\HrmsDev\kpi-evidence` in `appsettings.Development.json` (local development), so development files never mix with real ones. On a server whose data drive differs, override it with this variable. Without any setting the code falls back to `App_Data/kpi-evidence` under the content root, which is wrong for a published folder that gets replaced. The API creates the folder on the first upload, but on a server create it yourself first so you can set its permissions. See "KPI evidence folder" below. |
| `Kpi__EvidenceUploadsPerMinute` | Evidence uploads one signed-in account may start per minute, default 10 (a positive whole number; anything else falls back to 10). Every attempt counts; the next one is `429` with `Retry-After`. In memory per instance (section 5). |

A suggested `appsettings.Production.json` (placeholders only, no secrets, no connection string) is in
section 9. Prefer environment variables for everything host-specific.

**Database account.** The DB project (`HR DBPJ`, `Security/hrms_api.sql` and `hrms_api.Permissions.sql`) defines the role
`hrms_api`: data access on the module schemas and the `auth` schema, append-only on `app.ActivityLog`, no DDL. A human
maps the runtime account into it, for example a gMSA (`CREATE LOGIN ... FROM WINDOWS; CREATE USER ...; ALTER ROLE
[hrms_api] ADD MEMBER ...`). The API must never connect as `sa`, `dbo` or the publishing login.

**Data protection keys.** The key ring signs and encrypts sessions, antiforgery tokens and reset tokens.

* Put `DataProtection__KeysPath` on a path only the service account can read and write.
* Back it up (with the database backup set, stored separately). Losing it signs everyone out and invalidates pending
  reset tokens; leaking it lets an attacker forge sessions.
* All instances must share the same directory. A host that cannot write it fails on the first key.
* Optional encryption at rest with a certificate (`ProtectKeysWithCertificate`) is **not wired in**; add it in
  `Hosting/HostingSetup.cs` if the host has no OS-level protection (DPAPI on Windows, a protected volume elsewhere).

**KPI evidence folder.** The database keeps only the metadata of an evidence file (`kpi.Attachment`); the bytes are in
`Kpi__EvidenceFolder` under a server-generated key. The files are HR records.

* Put it **outside any web root** and never serve it directly: downloads go only through
  `GET /api/kpi/attachments/{id}`, which checks the caller's scorecard scope.
* Give the API's runtime identity (and nobody else) read and write access. Keep it out of the published folder (it
  would be replaced on the next deploy) and out of git (`App_Data/kpi-evidence/` is ignored).
* Use **backed-up** storage, and back it up **together with the database** (section 4). A restored database without
  its files has attachments that answer `404`; files without their rows are orphans.
* Several instances must share the same folder (or replace the local storage with an object store).
* Encrypt it at rest (disk or volume encryption) together with its backups.
* **There is no virus or malware scan** of uploads. The API checks the type by content, the size (10 MB), the count (5
  per KPI) and the name, and serves every file as a download with `nosniff`. Before real data, add a malware scan that
  holds a file back until it is clean, and agree a retention and deletion policy for the evidence of employees who leave
  (`api/README.md`, "Security note").
* Set the reverse proxy's request-size limit to match the API's evidence upload limit (11 MB for the whole request).

## 3. HTTPS, HSTS, cookies, hosts

* Outside Development the API calls `UseHsts` and `UseHttpsRedirection`. Terminate TLS at the proxy or at Kestrel with
  a valid certificate for the real host. Do not expose plain HTTP to clients. Verify the HSTS header after go-live and
  only then consider preloading.
* Cookies: session `__Host-hrms` and antiforgery `__Host-hrms-antiforgery` are HttpOnly, `Secure` always, SameSite=Strict,
  Path `/`, no Domain. The `__Host-` prefix means browsers refuse them over plain HTTP or with a Domain, so HTTPS
  end to end as seen by the browser is mandatory. The readable `XSRF-TOKEN` cookie is also Secure and Strict.
* Behind a proxy: terminate TLS at the proxy, send `X-Forwarded-For` and `X-Forwarded-Proto`, and enable
  `Hosting__ForwardedHeaders__*` with the proxy's address. Without it the API sees `http` (redirect loop or missing
  Secure) and every user as the proxy's IP (the per-IP rate limit and lockout then hit everyone together). The
  forward limit is 1: configure exactly one proxy hop, and make the proxy overwrite (not append to) client-supplied
  forwarded headers.
* Antiforgery: the UI sends `X-XSRF-TOKEN` from the `XSRF-TOKEN` cookie. It works unchanged behind a proxy as long as the
  proxy passes `Cookie`, `Set-Cookie` and the header through and the browser origin is a single host.
* `AllowedHosts` must be the public host name the proxy forwards (preserve the `Host` header or list the internal name).

## 4. Database

* The schema is published by a **human** from the separate DB project to the target server, using a publishing login
  that has DDL rights (not the `hrms_api` account). Review the generated script first (`SqlPackage /Action:Script`)
  and publish a backup-verified server. Never from CI and never by an agent, and never without explicit approval.
* EF Core in the API never migrates.
* After the first publish, the post-deployment script creates roles and seeds. Create the first administrator on the
  server with an interactive terminal: `dotnet Apex.Hrms.Api.dll bootstrap-admin --email <address> [--name "<name>"]`
  with the production environment variables set in that shell (it prints the target database and asks for `yes`; the
  password is typed twice, never passed as an argument). It refuses if an active HR Administrator exists.
  Check the printed target before confirming.
* Backups: full plus log backups on the SQL Server side with a tested restore, retention to match policy, encrypted
  and stored off the server. Back up the data protection key ring alongside, and the KPI evidence folder at the same
  time as the database, so a restore brings back rows and files that belong together. Test a restore into a
  non-production server.

**KPI module schema** (database branch `feature/schema-kpi` of the SSDT project `D:\New folder\HR DBPJ\HR DBPJ`; it
adds the `kpi` schema, its tables, the `hrms_api` grants with `DENY UPDATE, DELETE` on `kpi.ReviewAction`, and the
`kpi-setup` and `kpi` permission modules):

* A **human** publishes it, and only after reviewing the script SqlPackage generates for the target
  (`SqlPackage /Action:Script`, then `/Action:Publish` with the same profile). Publish to a local or other
  non-production database first and run the KPI checks there (API integration tests, the **API** rows of
  `docs/E2E-TEST-SCENARIOS.md` section 17). Never from CI or by an agent, and never to production without explicit
  approval.
* **Starter grants apply only to newly inserted modules.** The post-deployment script adds the KPI starter grants
  (HR Officer `kpi-setup` all and `kpi` View, Create, Approve; Department Manager `kpi` View, Create, Approve; Employee,
  Payroll Officer and Recruiter `kpi` View, Create; Auditor View) only in the publish that inserts the `kpi-setup` and
  `kpi` modules, and only to roles that still exist. A later publish does not add them again, and does not bring back a
  grant an administrator removed. If the modules already exist (for example after an earlier test publish), tick the
  grants in Settings > Permissions.
* The default rating bands are inserted only into an empty `kpi.RatingBand` table.
* Granting `employees:Edit` together with `kpi:Approve` gives a role the KPI HR rights (every scorecard, the HR
  dashboard, the HR final review). Check the custom roles before go-live.

## 5. Multi-instance notes

Per-instance, in memory today (FUTURE-TODO 3.3):

* The sign-in / forgot / reset rate limiter (so a client gets N times the limit across N instances).
* The password-reset throttle.
* The grant cache safety net (permissions are read from the database on each request, so a stale cache is a safety
  net, not the source of truth).
* The KPI evidence upload rate limiter (`Kpi__EvidenceUploadsPerMinute`, fixed window per signed-in account), so an
  account gets N times the limit across N instances.

With more than one instance: share `DataProtection__KeysPath` and `Kpi__EvidenceFolder`, use a proxy without session affinity needs (cookie
sessions are stateless), and replace the four in-memory stores with a shared store (for example a distributed cache or
Redis, or SQL-backed counters) behind the existing abstractions. Until then run a single instance, or accept the weaker
throttling.

## 6. Host notes

* **Windows Server / IIS:** install the ASP.NET Core Hosting Bundle, publish to a folder, an app pool with "No Managed
  Code" under a gMSA that is the SQL login and has write access to the keys folder and the KPI evidence folder only. Set variables at machine or
  app pool level or from a vault; never put passwords in a `web.config` that is in source control. If IIS (or ARR in
  front) terminates TLS, enable forwarded headers and list that proxy's address. Serve the UI from the IIS site and add a URL Rewrite SPA fallback that
  excludes `/api`.
* **Azure App Service:** use application settings (or Key Vault references) for the variables, a managed identity for
  SQL where possible, persistent storage (mounted Azure Files, not the ephemeral folder) for the key ring and the KPI evidence folder, and
  HTTPS Only on. The front end terminates TLS, so enable forwarded headers with the front end's ranges. Several
  instances need the shared keys path plus the section 5 changes.
* **Docker / Linux reverse proxy:** build with `dotnet publish`, run a non-root user, mount volumes for
  `DataProtection__KeysPath` and `Kpi__EvidenceFolder`, pass secrets as orchestrator secrets or an env file kept out of the image and repo.
  nginx or similar serves the UI files, proxies `/api/` with `X-Forwarded-For`, `X-Forwarded-Proto` and `Host`, and
  falls back to `index.html`. Put the API on a private network and list the proxy's address in `KnownProxies`.
  Do not publish the API port directly.

## 7. Rollback

* API: keep the previous publish folder or image. Roll back by redeploying it with the same configuration. Safe while
  the database schema is unchanged.
* UI: keep the previous `dist` output; swap it back.
* Database: schema changes are the hard part. Take a backup before every publish; prefer additive, backward
  compatible changes so the old API still works against the new schema. Restore from backup only as a last resort
  and accept data loss since the backup.
* Do not delete the key ring on rollback: sessions and reset tokens depend on it.
* Do not delete or replace the KPI evidence folder on rollback. If the database is restored from a backup, restore the
  evidence folder from the same point in time.
* The mock-data demo on GitHub Pages is unaffected by this deployment.

## 8. Pre-go-live checklist

- [ ] Hosting decided and documented; FUTURE-TODO section 3 updated.
- [ ] Data protection review done (PII, bank details, documents) **before any real HR data** is loaded.
- [ ] Payroll rules replaced or signed off by a payroll specialist. The current rules are demonstrations, not statutory
      calculations.
- [ ] `security-auditor` review of the team and payslip scoping (FUTURE-TODO 3) and of this configuration.
- [ ] All secrets (connection string, SMTP password) in a secret store; none in files, images, pipelines or logs.
- [ ] Runtime account is `hrms_api` only; nothing connects as a DBA.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`. Verified in code: outside Development `AddEmailSender` registers
      `SmtpEmailSender`; the Development mail sink (which writes reset tokens to disk) is only used in Development, and
      an unconfigured SMTP answers `503` on forgot-password (fails closed, no silent drop). Send a test reset mail.
- [ ] `AllowedHosts` exact; `Cors` index 0 overridden; HTTPS valid; HSTS header present; cookies show `__Host-`, Secure.
- [ ] Forwarded headers on with the real proxy only; sign-in rate limit sees real client IPs (test from two clients).
- [ ] Key ring on a protected, shared, backed-up path; restore tested.
- [ ] KPI: `feature/schema-kpi` script reviewed and published by a human (non-production first); KPI starter grants
      checked in Settings > Permissions; roles with both `kpi:Approve` and `employees:Edit` reviewed.
- [ ] KPI evidence folder outside the web root, writable by the API identity only, encrypted at rest, backed up with the
      database; malware scanning of uploads in place (none today); proxy request-size limit set; retention policy agreed.
- [ ] Database backups and restore tested; bootstrap-admin run; first admin password stored in a vault.
- [ ] A production API-mode UI build exists and has no source maps (see section 1).
- [ ] Monitoring and alerts: the API has **no health endpoint** today (gap). Add liveness (process up) and readiness
      (database reachable) endpoints, anonymous but minimal, and point the host's probe at them.
- [ ] Log retention and access policy defined (logs must not contain PII; activity log retention, FUTURE-TODO 3).
- [ ] Remember-me maximum confirmed (default 30 days).
- [ ] Single instance, or the shared store work from section 5 is done.
- [ ] Rollback rehearsed.

## 9. Suggested `appsettings.Production.json`

Placeholders only. The repository tooling blocked creating this file automatically, so a human adds it next to
`appsettings.json` if wanted (everything in it can equally be set by environment variables).

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Apex.Hrms": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "hrms.example.com",
  "Auth": { "RememberMeMaxDays": 30, "RateLimit": { "PermitsPerMinute": 10 } },
  "Email": { "Smtp": { "Port": 587, "EnableSsl": true } }
}
```

No connection string, SMTP host, user name, password or key path is included on purpose.
