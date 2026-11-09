using Apex.Hrms.Api;
using Apex.Hrms.Api.Endpoints;
using Apex.Hrms.Application;
using Apex.Hrms.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

ApiApplicationExtensions.EnsureHostFilteringIsConfigured(builder.Configuration, builder.Environment);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapOrganizationEndpoints();
app.MapEmployeeEndpoints();

await app.RunAsync();

/// <summary>Exposes the entry point to the API tests.</summary>
public partial class Program;
