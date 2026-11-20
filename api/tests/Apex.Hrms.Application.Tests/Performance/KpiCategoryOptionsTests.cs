using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Tests.Fakes;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// The KPI category picker: every category, active and inactive, by name, with only id, code, name and status (no description
/// or row version). Category names are unique, so the id tie-break cannot occur through the create handler.
/// </summary>
public sealed class KpiCategoryOptionsTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private IKpiCategoryOptionsHandler Options => host.Get<IKpiCategoryOptionsHandler>();

    [Fact]
    public async Task No_categories_give_an_empty_list()
    {
        var options = ResultAssert.Succeeded(await Options.HandleAsync(default));

        Assert.Empty(options);
    }

    [Fact]
    public async Task Every_category_is_listed_with_inactive_ones_by_name()
    {
        var quality = await host.CreateCategoryAsync("QUAL", "Quality");
        var customer = await host.CreateCategoryAsync("CUST", "Customer Service", status: "Inactive");
        var productivity = await host.CreateCategoryAsync();

        var options = ResultAssert.Succeeded(await Options.HandleAsync(default));

        Assert.Equal(
            [
                new KpiCategoryOptionDto(customer.Id, "CUST", "Customer Service", "Inactive"),
                new KpiCategoryOptionDto(productivity.Id, "PROD", "Productivity", "Active"),
                new KpiCategoryOptionDto(quality.Id, "QUAL", "Quality", "Active"),
            ],
            options);

        // The same code, name and status as the full category DTO.
        foreach (var full in new[] { quality, customer, productivity })
        {
            var option = options.Single(row => row.Id == full.Id);
            Assert.Equal((full.Code, full.Name, full.Status), (option.Code, option.Name, option.Status));
        }
    }

    [Fact]
    public void The_option_carries_only_the_contract_fields()
    {
        Assert.Equal(["Id", "Code", "Name", "Status"], typeof(KpiCategoryOptionDto).GetProperties().Select(property => property.Name));
    }
}
