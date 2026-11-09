using Apex.Hrms.Api;
using Apex.Hrms.Api.Endpoints;
using Apex.Hrms.Application;
using Apex.Hrms.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapOrganizationEndpoints();

await app.RunAsync();

/// <summary>Exposes the entry point to the API tests.</summary>
public partial class Program;
