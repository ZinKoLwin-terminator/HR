using System.Reflection;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Activity;

/// <summary>The read side of the activity log: paging, filters, search, validation, the single entry, and the append-only rule.</summary>
public sealed class ActivityQueryTests : IDisposable
{
    private readonly TestHost host = new();

    public ActivityQueryTests()
    {
        // Three days of fictional entries: oldest first in the log, newest first in the feed.
        host.ActivityLog.Append(ActivityCategories.Leave, "LV-0001 submitted for approval", 2, "Aye Test", Utc(2026, 11, 7, 3));
        host.ActivityLog.Append(ActivityCategories.Organization, "Department \"Engineering\" created", 1, "Zin Test", Utc(2026, 11, 8, 4));
        host.ActivityLog.Append(ActivityCategories.Leave, "LV-0001 approved", 1, "Zin Test", Utc(2026, 11, 9, 5));
        host.ActivityLog.Append(ActivityCategories.System, "General settings updated", null, "System", Utc(2026, 11, 9, 6));
    }

    public void Dispose() => host.Dispose();

    private static DateTime Utc(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static PageRequest<ActivityFilter> Page(ActivityFilter? filter = null, string? search = null, int index = 0, int size = 10, SortState? sort = null) =>
        new(index, size, sort, search, filter ?? new ActivityFilter());

    private async Task<PagedResult<ActivityEntryDto>> QueryAsync(PageRequest<ActivityFilter> request) =>
        ResultAssert.Succeeded(await host.Get<IActivityQueryHandler>().HandleAsync(request, default));

    [Fact]
    public async Task The_feed_lists_the_newest_entry_first()
    {
        var page = await QueryAsync(Page());

        Assert.Equal(4, page.Total);
        Assert.Equal(["General settings updated", "LV-0001 approved", "Department \"Engineering\" created", "LV-0001 submitted for approval"], page.Items.Select(i => i.Message));
    }

    [Fact]
    public async Task An_entry_has_the_shape_of_the_frontend_model()
    {
        var item = (await QueryAsync(Page(new ActivityFilter(Category: "organization")))).Items.Single();

        Assert.Equal("organization", item.Category);
        Assert.Equal("Zin Test", item.Actor);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 4, 0, 0, TimeSpan.Zero), item.Timestamp);
        Assert.True(item.Id > 0);
    }

    [Fact]
    public async Task Paging_returns_the_requested_page_and_total()
    {
        var page = await QueryAsync(Page(index: 1, size: 3));

        Assert.Equal(4, page.Total);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal("LV-0001 submitted for approval", Assert.Single(page.Items).Message);
    }

    [Fact]
    public async Task The_category_filter_keeps_only_that_category()
    {
        var page = await QueryAsync(Page(new ActivityFilter(Category: "leave")));

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, item => Assert.Equal("leave", item.Category));
    }

    [Fact]
    public async Task The_actor_filter_matches_part_of_the_name()
    {
        var page = await QueryAsync(Page(new ActivityFilter(Actor: "zin")));

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, item => Assert.Equal("Zin Test", item.Actor));
    }

    [Fact]
    public async Task The_date_filters_are_inclusive_days()
    {
        var page = await QueryAsync(Page(new ActivityFilter(DateFrom: new DateOnly(2026, 11, 8), DateTo: new DateOnly(2026, 11, 8))));

        Assert.Equal("Department \"Engineering\" created", Assert.Single(page.Items).Message);
        Assert.Equal(3, (await QueryAsync(Page(new ActivityFilter(DateFrom: new DateOnly(2026, 11, 8))))).Total);
        Assert.Equal(2, (await QueryAsync(Page(new ActivityFilter(DateTo: new DateOnly(2026, 11, 8))))).Total);
    }

    [Fact]
    public async Task Search_matches_the_message_or_the_actor()
    {
        Assert.Equal(2, (await QueryAsync(Page(search: "LV-0001"))).Total);
        Assert.Equal("General settings updated", Assert.Single((await QueryAsync(Page(search: "system"))).Items).Message);
    }

    [Fact]
    public async Task Sorting_by_a_known_field_works_and_an_unknown_one_is_a_validation_error()
    {
        var oldestFirst = await QueryAsync(Page(sort: new SortState("timestamp", "asc")));
        var byCategory = await QueryAsync(Page(sort: new SortState("category", "asc")));

        Assert.Equal("LV-0001 submitted for approval", oldestFirst.Items[0].Message);
        Assert.Equal("leave", byCategory.Items[0].Category);
        ResultAssert.FailedOnField(await host.Get<IActivityQueryHandler>().HandleAsync(Page(sort: new SortState("message", "asc")), default), "sort");
    }

    [Fact]
    public async Task A_bad_filter_is_a_validation_error_naming_the_field()
    {
        var handler = host.Get<IActivityQueryHandler>();

        ResultAssert.FailedOnField(await handler.HandleAsync(Page(new ActivityFilter(Category: "payslips")), default), "category");
        ResultAssert.FailedOnField(await handler.HandleAsync(Page(search: new string('x', 101)), default), "search");
        ResultAssert.FailedOnField(
            await handler.HandleAsync(Page(new ActivityFilter(DateFrom: new DateOnly(2026, 11, 9), DateTo: new DateOnly(2026, 11, 1))), default),
            "dateTo");
    }

    [Fact]
    public async Task One_entry_is_returned_by_id_and_a_missing_one_is_not_found()
    {
        var id = host.ActivityLog.Entries[1].Id;

        var found = ResultAssert.Succeeded(await host.Get<IActivityGetHandler>().HandleAsync(id, default));
        var missing = await host.Get<IActivityGetHandler>().HandleAsync(9999, default);

        Assert.Equal("Department \"Engineering\" created", found.Message);
        ResultAssert.Failed(missing, ErrorType.NotFound);
    }

    // ---- Append-only: nothing in the code can change or remove an entry ----

    [Fact]
    public void The_read_port_and_the_handlers_offer_no_way_to_change_an_entry()
    {
        var writeVerbs = new[] { "Add", "Update", "Delete", "Remove", "Edit", "Save", "Set", "Create", "Insert" };
        var surfaces = new[] { typeof(IActivityLogStore), typeof(IActivityQueryHandler), typeof(IActivityGetHandler) };

        var names = surfaces.SelectMany(type => type.GetMethods()).Select(method => method.Name).ToList();

        Assert.DoesNotContain(names, name => writeVerbs.Any(verb => name.StartsWith(verb, StringComparison.Ordinal)));
    }

    [Fact]
    public void An_entry_has_no_public_setter_and_no_public_instance_method_that_changes_it()
    {
        var type = typeof(ActivityLogEntry);

        Assert.All(type.GetProperties(), property => Assert.False(property.SetMethod?.IsPublic ?? false, property.Name));
        var instanceMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName);
        Assert.Empty(instanceMethods);
    }
}
