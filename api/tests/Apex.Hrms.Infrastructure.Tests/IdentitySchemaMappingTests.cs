using Apex.Hrms.Domain.Security;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Checks the Identity and security part of the EF model against the SSDT tables auth.User, auth.UserClaim, auth.UserLogin,
/// auth.UserToken, sec.AppUser, sec.Role and sec.RolePermission. No connection is ever opened.
/// </summary>
public sealed class IdentitySchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    private static IProperty Column(IEntityType entity, string column) =>
        entity.GetProperties().Single(property => property.GetColumnName() == column);

    [Fact]
    public void The_model_has_exactly_the_four_identity_tables_in_the_auth_schema_and_no_role_tables()
    {
        var auth = context.Model.GetEntityTypes().Where(type => type.GetSchema() == "auth").Select(type => type.GetTableName()!).Order().ToArray();

        Assert.Equal(["User", "UserClaim", "UserLogin", "UserToken"], auth);
        Assert.DoesNotContain(context.Model.GetEntityTypes(), type => (type.GetTableName() ?? string.Empty).StartsWith("AspNet", StringComparison.Ordinal));
        Assert.DoesNotContain(context.Model.GetEntityTypes(), type => type.GetTableName() is "UserPasskey" or "UserRole" or "RoleClaim");
    }

    [Theory]
    [InlineData(typeof(IdentityUser), "auth", "User")]
    [InlineData(typeof(IdentityUserClaim<string>), "auth", "UserClaim")]
    [InlineData(typeof(IdentityUserLogin<string>), "auth", "UserLogin")]
    [InlineData(typeof(IdentityUserToken<string>), "auth", "UserToken")]
    [InlineData(typeof(AppAccount), "sec", "AppUser")]
    [InlineData(typeof(AppRole), "sec", "Role")]
    [InlineData(typeof(RoleGrant), "sec", "RolePermission")]
    public void Tables_map_to_the_ssdt_schema_and_name(Type entity, string schema, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal(schema, type.GetSchema());
        Assert.Equal(table, type.GetTableName());
    }

    [Theory]
    [InlineData("Id", "nvarchar(450)", false)]
    [InlineData("UserName", "nvarchar(256)", true)]
    [InlineData("NormalizedUserName", "nvarchar(256)", true)]
    [InlineData("Email", "nvarchar(256)", true)]
    [InlineData("NormalizedEmail", "nvarchar(256)", true)]
    [InlineData("EmailConfirmed", "bit", false)]
    [InlineData("PasswordHash", "nvarchar(max)", true)]
    [InlineData("SecurityStamp", "nvarchar(max)", true)]
    [InlineData("ConcurrencyStamp", "nvarchar(max)", true)]
    [InlineData("PhoneNumber", "nvarchar(50)", true)]
    [InlineData("PhoneNumberConfirmed", "bit", false)]
    [InlineData("TwoFactorEnabled", "bit", false)]
    [InlineData("LockoutEnd", "datetimeoffset", true)]
    [InlineData("LockoutEnabled", "bit", false)]
    [InlineData("AccessFailedCount", "int", false)]
    public void auth_User_columns_match_the_ssdt_table(string column, string type, bool nullable)
    {
        var property = Column(Entity<IdentityUser>(), column);

        Assert.StartsWith(type, property.GetColumnType(), StringComparison.Ordinal);
        Assert.Equal(nullable, property.IsNullable);
    }

    [Fact]
    public void auth_User_has_exactly_the_ssdt_columns()
    {
        var columns = Entity<IdentityUser>().GetProperties().Select(property => property.GetColumnName()!).Order().ToArray();

        Assert.Equal(
            [
                "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "Id", "LockoutEnabled", "LockoutEnd",
                "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed",
                "SecurityStamp", "TwoFactorEnabled", "UserName",
            ],
            columns);
    }

    [Fact]
    public void auth_User_keys_and_indexes_match_the_ssdt_table()
    {
        var user = Entity<IdentityUser>();

        Assert.Equal("PK_User", user.FindPrimaryKey()!.GetName());
        Assert.Equal(["Id"], user.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.True(user.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken);

        var byName = user.GetIndexes().Single(index => index.GetDatabaseName() == "UQ_User_NormalizedUserName");
        Assert.True(byName.IsUnique);
        Assert.Equal(["NormalizedUserName"], byName.Properties.Select(property => property.Name));

        var byEmail = user.GetIndexes().Single(index => index.GetDatabaseName() == "IX_User_NormalizedEmail");
        Assert.False(byEmail.IsUnique);
        Assert.Equal(["NormalizedEmail"], byEmail.Properties.Select(property => property.Name));
    }

    [Fact]
    public void The_child_tables_match_the_ssdt_columns_keys_and_cascading_foreign_keys()
    {
        var claim = Entity<IdentityUserClaim<string>>();
        Assert.Equal("PK_UserClaim", claim.FindPrimaryKey()!.GetName());
        Assert.Equal(ValueGenerated.OnAdd, Column(claim, "Id").ValueGenerated);
        Assert.Equal("nvarchar(450)", Column(claim, "UserId").GetColumnType());
        Assert.Equal("nvarchar(max)", Column(claim, "ClaimType").GetColumnType());
        Assert.Equal("nvarchar(max)", Column(claim, "ClaimValue").GetColumnType());
        AssertCascade(claim, "FK_UserClaim_User", "UserId");
        Assert.Contains(claim.GetIndexes(), index => index.GetDatabaseName() == "IX_UserClaim_UserId");

        var login = Entity<IdentityUserLogin<string>>();
        Assert.Equal("PK_UserLogin", login.FindPrimaryKey()!.GetName());
        Assert.Equal(["LoginProvider", "ProviderKey"], login.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("nvarchar(128)", Column(login, "LoginProvider").GetColumnType());
        Assert.Equal("nvarchar(128)", Column(login, "ProviderKey").GetColumnType());
        Assert.Equal("nvarchar(max)", Column(login, "ProviderDisplayName").GetColumnType());
        Assert.Equal("nvarchar(450)", Column(login, "UserId").GetColumnType());
        AssertCascade(login, "FK_UserLogin_User", "UserId");
        Assert.Contains(login.GetIndexes(), index => index.GetDatabaseName() == "IX_UserLogin_UserId");

        var token = Entity<IdentityUserToken<string>>();
        Assert.Equal("PK_UserToken", token.FindPrimaryKey()!.GetName());
        Assert.Equal(["UserId", "LoginProvider", "Name"], token.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("nvarchar(450)", Column(token, "UserId").GetColumnType());
        Assert.Equal("nvarchar(128)", Column(token, "LoginProvider").GetColumnType());
        Assert.Equal("nvarchar(128)", Column(token, "Name").GetColumnType());
        Assert.Equal("nvarchar(max)", Column(token, "Value").GetColumnType());
        AssertCascade(token, "FK_UserToken_User", "UserId");
    }

    [Fact]
    public void sec_AppUser_maps_the_ssdt_columns_keys_and_foreign_keys()
    {
        var account = Entity<AppAccount>();

        Assert.Equal("PK_AppUser", account.FindPrimaryKey()!.GetName());
        Assert.Equal(ValueGenerated.OnAdd, Column(account, "Id").ValueGenerated);
        Assert.Equal("int", Column(account, "EmployeeId").GetColumnType());
        Assert.True(Column(account, "EmployeeId").IsNullable);
        Assert.Equal("nvarchar(130)", Column(account, "DisplayName").GetColumnType());
        Assert.Equal("nvarchar(120)", Column(account, "Email").GetColumnType());
        Assert.Equal("varchar(30)", Column(account, "Status").GetColumnType());
        Assert.Equal("datetime2(3)", Column(account, "LastLoginAtUtc").GetColumnType());
        Assert.Equal("nvarchar(450)", Column(account, "ExternalSubject").GetColumnType());
        Assert.True(Column(account, "ExternalSubject").IsNullable);
        Assert.True(Column(account, "RowVersion").IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, Column(account, "RowVersion").ValueGenerated);

        var email = account.GetIndexes().Single(index => index.GetDatabaseName() == "UQ_AppUser_Email");
        Assert.True(email.IsUnique);
        var subject = account.GetIndexes().Single(index => index.GetDatabaseName() == "UQ_AppUser_ExternalSubject");
        Assert.True(subject.IsUnique);
        Assert.Equal("[ExternalSubject] IS NOT NULL", subject.GetFilter());

        Assert.Equal(["RoleId"], ForeignKey(account, "FK_AppUser_Role").Properties.Select(property => property.Name));
        Assert.Equal(["EmployeeId"], ForeignKey(account, "FK_AppUser_Employee").Properties.Select(property => property.Name));
        var link = ForeignKey(account, "FK_AppUser_AuthUser_ExternalSubject");
        Assert.Equal(["ExternalSubject"], link.Properties.Select(property => property.Name));
        Assert.Equal(typeof(IdentityUser), link.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.NoAction, link.DeleteBehavior);
    }

    [Fact]
    public void sec_Role_and_RolePermission_map_the_ssdt_columns_and_keys()
    {
        var role = Entity<AppRole>();
        Assert.Equal("nvarchar(60)", Column(role, "Name").GetColumnType());
        Assert.Equal("nvarchar(300)", Column(role, "Description").GetColumnType());
        Assert.Equal("varchar(30)", Column(role, "Status").GetColumnType());
        Assert.Equal("bit", Column(role, "IsSystem").GetColumnType());
        Assert.True(role.GetIndexes().Single(index => index.GetDatabaseName() == "UQ_Role_Name").IsUnique);

        var grant = Entity<RoleGrant>();
        Assert.Equal("PK_RolePermission", grant.FindPrimaryKey()!.GetName());
        Assert.Equal(["RoleId", "ModuleKey", "Action"], grant.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("varchar(30)", Column(grant, "ModuleKey").GetColumnType());
        Assert.Equal("varchar(30)", Column(grant, "Action").GetColumnType());
    }

    private static IForeignKey ForeignKey(IEntityType entity, string name) =>
        entity.GetForeignKeys().Single(key => key.GetConstraintName() == name);

    private static void AssertCascade(IEntityType entity, string name, string column)
    {
        var key = ForeignKey(entity, name);

        Assert.Equal([column], key.Properties.Select(property => property.Name));
        Assert.Equal(typeof(IdentityUser), key.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, key.DeleteBehavior);
    }
}
