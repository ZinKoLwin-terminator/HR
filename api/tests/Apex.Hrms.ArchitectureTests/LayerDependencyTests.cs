using System.Reflection;
using NetArchTest.Rules;

namespace Apex.Hrms.ArchitectureTests;

/// <summary>Clean Architecture dependency rule: source code dependencies point inwards only.</summary>
public sealed class LayerDependencyTests
{
    private const string Domain = "Apex.Hrms.Domain";
    private const string Application = "Apex.Hrms.Application";
    private const string Infrastructure = "Apex.Hrms.Infrastructure";
    private const string Api = "Apex.Hrms.Api.";

    private static readonly Assembly DomainAssembly = typeof(Apex.Hrms.Domain.Common.Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Apex.Hrms.Application.Paging).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Apex.Hrms.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_and_no_framework_package() =>
        AssertNoDependencies(
            DomainAssembly,
            Application,
            Infrastructure,
            Api,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "FluentValidation",
            "Microsoft.Extensions");

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_or_entity_framework() =>
        AssertNoDependencies(
            ApplicationAssembly,
            Infrastructure,
            Api,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.SqlClient",
            "Microsoft.AspNetCore");

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api() =>
        AssertNoDependencies(InfrastructureAssembly, Api, "Microsoft.AspNetCore");

    [Fact]
    public void Api_speaks_in_dtos_and_never_touches_domain_entities() =>
        AssertNoDependencies(ApiAssembly, "Apex.Hrms.Domain.Organization", "Apex.Hrms.Domain.Employees");

    [Fact]
    public void Use_case_handler_classes_are_not_public()
    {
        var leaked = Types.InAssembly(ApplicationAssembly)
            .That().AreClasses().And().HaveNameEndingWith("Handler")
            .Should().NotBePublic()
            .GetResult();

        Assert.True(leaked.IsSuccessful, Describe(leaked));
    }

    [Fact]
    public void Handlers_validators_and_rules_are_not_public_implementation_details()
    {
        var leaked = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("Rules")
            .Or().HaveNameEndingWith("Mapping")
            .Or().HaveNameEndingWith("RequestValidator")
            .Or().HaveNameEndingWith("QuerySpecification")
            .Should().NotBePublic()
            .GetResult();

        Assert.True(leaked.IsSuccessful, Describe(leaked));
    }

    private static void AssertNoDependencies(Assembly assembly, params string[] forbiddenNamespaces)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
