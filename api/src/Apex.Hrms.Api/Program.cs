using Apex.Hrms.Api;
using Apex.Hrms.Api.Cli;
using Apex.Hrms.Api.Endpoints;
using Apex.Hrms.Application;
using Apex.Hrms.Infrastructure;
using Apex.Hrms.Infrastructure.Email;
using Apex.Hrms.Infrastructure.Excel;

// A command line tool, never an HTTP endpoint: it builds its own host and never starts the web server.
if (args is [BootstrapAdminCommand.Name, ..])
{
    return await BootstrapAdminHost.RunAsync(args[1..]);
}

var builder = WebApplication.CreateBuilder(args);

ApiApplicationExtensions.EnsureHostFilteringIsConfigured(builder.Configuration, builder.Environment);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddEmailSender(builder.Configuration, builder.Environment.IsDevelopment(), builder.Environment.ContentRootPath)
    .AddExcelUploadFolder(builder.Configuration, builder.Environment.ContentRootPath)
    .AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseApiPipeline();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapAuthEndpoints();
app.MapOrganizationEndpoints();
app.MapEmployeeEndpoints();
app.MapLeaveEndpoints();
app.MapAttendanceEndpoints();
app.MapActivityEndpoints();
app.MapRecruitmentEndpoints();
app.MapSettingsEndpoints();
app.MapExcelEndpoints();

await app.RunAsync();
return 0;

/// <summary>Exposes the entry point to the API tests.</summary>
public partial class Program;
