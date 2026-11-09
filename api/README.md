# APEX HRMS API

.NET 10 Web API (Clean Architecture) for APEX HRMS. EF Core maps onto the existing SQL Server schema owned by the
SSDT project `HR DBPJ`; it never creates or migrates it.

| Project | Role |
| --- | --- |
| `Apex.Hrms.Domain` | Entities, domain rules, `Result` / `AppError`. No dependencies. |
| `Apex.Hrms.Application` | Use-case handlers, FluentValidation, DTOs, ports (`IRepository<>`, `IUnitOfWork`, `IClock`, `ICurrentUser`). |
| `Apex.Hrms.Infrastructure` | EF Core 10 SQL Server mapping, repositories, clock, placeholder current user. |
| `Apex.Hrms.Api` | Minimal API endpoints, ProblemDetails, CORS, OpenAPI. |

Run: put the connection string in `ConnectionStrings:Hrms` (`appsettings.Development.json` holds the integrated-auth
local one; use user-secrets or environment variables for anything with a password), then `dotnet run --project src/Apex.Hrms.Api`.
Test: `dotnet test`.

Auth is not implemented yet: endpoints are anonymous and `ICurrentUser` is a placeholder. Do not store real data.
