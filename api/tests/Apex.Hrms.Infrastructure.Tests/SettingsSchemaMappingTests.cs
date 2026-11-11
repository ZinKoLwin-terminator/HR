using Apex.Hrms.Domain.Security;
using Apex.Hrms.Domain.Settings;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Checks the settings part of the EF model against cfg.GeneralSettings, sec.PermissionModule, sec.PermissionModuleAction
/// and the audit columns of sec.RolePermission. No connection is ever opened.
/// </summary>
public sealed class SettingsSchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private static IProperty Column(IEntityType entity, string column) =>
        entity.GetProperties().Single(property => property.GetColumnName() == column);

    [Fact]
    public void cfg_GeneralSettings_maps_the_ssdt_columns_and_is_a_single_row_with_a_rowversion()
    {
        var settings = context.Model.FindEntityType(typeof(GeneralSettings))!;

        Assert.Equal("cfg", settings.GetSchema());
        Assert.Equal("GeneralSettings", settings.GetTableName());
        Assert.Equal("PK_GeneralSettings", settings.FindPrimaryKey()!.GetName());
        Assert.Equal(ValueGenerated.Never, Column(settings, "Id").ValueGenerated);
        Assert.Equal("nvarchar(80)", Column(settings, "OrganizationName").GetColumnType());
        Assert.Equal("nvarchar(120)", Column(settings, "ContactEmail").GetColumnType());
        Assert.Equal("varchar(40)", Column(settings, "Timezone").GetColumnType());
        Assert.Equal("varchar(20)", Column(settings, "DateFormat").GetColumnType());
        Assert.Equal("char(3)", Column(settings, "CurrencyCode").GetColumnType());
        Assert.Equal("tinyint", Column(settings, "FiscalYearStartMonth").GetColumnType());
        Assert.Equal("bit", Column(settings, "EmailNotifications").GetColumnType());
        Assert.Equal("bit", Column(settings, "WeeklyDigest").GetColumnType());
        Assert.True(Column(settings, "RowVersion").IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, Column(settings, "RowVersion").ValueGenerated);
    }

    [Fact]
    public void The_permission_reference_tables_are_mapped_with_the_ssdt_keys()
    {
        var modules = context.Model.GetEntityTypes().Single(type => type.GetTableName() == "PermissionModule" && type.GetSchema() == "sec");
        var actions = context.Model.GetEntityTypes().Single(type => type.GetTableName() == "PermissionModuleAction" && type.GetSchema() == "sec");

        Assert.Equal("PK_PermissionModule", modules.FindPrimaryKey()!.GetName());
        Assert.Equal("varchar(30)", Column(modules, "ModuleKey").GetColumnType());
        Assert.Equal("nvarchar(60)", Column(modules, "Label").GetColumnType());
        Assert.Equal("PK_PermissionModuleAction", actions.FindPrimaryKey()!.GetName());
        Assert.Equal(["ModuleKey", "Action"], actions.FindPrimaryKey()!.Properties.Select(property => property.Name));
    }

    [Fact]
    public void A_role_grant_row_carries_who_created_it_and_lets_the_database_fill_the_time_and_the_view_column()
    {
        var grant = context.Model.FindEntityType(typeof(RoleGrant))!;

        Assert.Equal("datetime2(3)", Column(grant, "CreatedAtUtc").GetColumnType());
        Assert.Equal("SYSUTCDATETIME()", Column(grant, "CreatedAtUtc").GetDefaultValueSql());
        Assert.True(Column(grant, "CreatedByUserId").IsNullable);
        Assert.DoesNotContain(grant.GetProperties(), property => property.GetColumnName() == "ViewAction");
    }
}
