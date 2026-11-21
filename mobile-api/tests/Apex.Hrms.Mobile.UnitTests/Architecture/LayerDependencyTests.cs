using System.Reflection;
using NetArchTest.Rules;

namespace Apex.Hrms.Mobile.UnitTests.Architecture;

/// <summary>Clean Architecture dependency rule for the mobile API: source code dependencies point inwards only.</summary>
public sealed class LayerDependencyTests
{
    private const string MobileDomain = "Apex.Hrms.Mobile.Domain";
    private const string MobileApplication = "Apex.Hrms.Mobile.Application";
    private const string MobileInfrastructure = "Apex.Hrms.Mobile.Infrastructure";
    private const string MobileApi = "Apex.Hrms.MobileApi";

    private static readonly Assembly Domain = typeof(Apex.Hrms.Mobile.Domain.Sessions.MobileSession).Assembly;
    private static readonly Assembly Application = typeof(Apex.Hrms.Mobile.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Apex.Hrms.Mobile.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Apex.Hrms.MobileApi.Program).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_and_no_framework_package() =>
        AssertNoDependencies(
            Domain,
            MobileApplication,
            MobileInfrastructure,
            MobileApi,
            "Apex.Hrms.Application",
            "Apex.Hrms.Infrastructure",
            "Microsoft");

    [Fact]
    public void Application_does_not_depend_on_infrastructure_the_host_entity_framework_or_the_web_stack() =>
        AssertNoDependencies(
            Application,
            MobileInfrastructure,
            MobileApi,
            "Apex.Hrms.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.SqlClient",
            "Microsoft.AspNetCore",
            "Microsoft.IdentityModel");

    /// <summary>Infrastructure may use the Identity stores and IdentityModel, but none of the web stack.</summary>
    [Fact]
    public void Infrastructure_does_not_depend_on_the_host_or_the_web_stack() =>
        AssertNoDependencies(
            Infrastructure,
            MobileApi,
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Builder",
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Routing",
            "Microsoft.AspNetCore.Authentication",
            "Microsoft.AspNetCore.Authorization");

    [Fact]
    public void The_host_never_touches_entity_framework_or_the_domain_entities_of_the_shared_hrms() =>
        AssertNoDependencies(
            Api,
            "Microsoft.EntityFrameworkCore",
            "Apex.Hrms.Domain.Employees",
            "Apex.Hrms.Domain.Organization",
            MobileDomain);

    [Fact]
    public void Use_case_handlers_and_adapters_are_not_public()
    {
        var handlers = Types.InAssembly(Application).That().HaveNameEndingWith("Handler").Should().NotBePublic().GetResult();
        var adapters = Types.InAssembly(Infrastructure).That().ResideInNamespaceStartingWith(MobileInfrastructure + ".Persistence")
            .Or().HaveNameEndingWith("Gateway").Or().HaveNameEndingWith("Issuer")
            .Should().NotBePublic().GetResult();

        Assert.True(handlers.IsSuccessful, Describe(handlers));
        Assert.True(adapters.IsSuccessful, Describe(adapters));
    }

    private static void AssertNoDependencies(Assembly assembly, params string[] forbiddenNamespaces)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbiddenNamespaces).GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) => "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
