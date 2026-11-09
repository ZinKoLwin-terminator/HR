using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>Maps sec.AppUser. CHECK constraints and defaults stay in the SSDT project.</summary>
internal sealed class AppAccountConfiguration : IEntityTypeConfiguration<AppAccount>
{
    public void Configure(EntityTypeBuilder<AppAccount> builder)
    {
        builder.ToTable("AppUser", "sec");
        builder.HasKey(account => account.Id).HasName("PK_AppUser");
        builder.Property(account => account.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(account => account.DisplayName).HasMaxLength(AppAccount.DisplayNameMaxLength);
        builder.Property(account => account.Email).HasMaxLength(AppAccount.EmailMaxLength);
        builder.HasIndex(account => account.Email).IsUnique().HasDatabaseName("UQ_AppUser_Email");
        builder.Property(account => account.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(account => account.LastLoginAtUtc).HasColumnType("datetime2(3)");
        builder.Property(account => account.ExternalSubject).HasMaxLength(450);
        builder.HasIndex(account => account.ExternalSubject).IsUnique().HasDatabaseName("UQ_AppUser_ExternalSubject")
            .HasFilter("[ExternalSubject] IS NOT NULL");

        builder.HasOne<AppRole>().WithMany().HasForeignKey(account => account.RoleId)
            .HasConstraintName("FK_AppUser_Role").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany().HasForeignKey(account => account.EmployeeId)
            .HasConstraintName("FK_AppUser_Employee").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(account => account.ExternalSubject)
            .HasConstraintName("FK_AppUser_AuthUser_ExternalSubject").OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>Maps sec.Role (read only).</summary>
internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("Role", "sec");
        builder.HasKey(role => role.Id).HasName("PK_Role");
        builder.Property(role => role.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(role => role.Name).HasMaxLength(60);
        builder.HasIndex(role => role.Name).IsUnique().HasDatabaseName("UQ_Role_Name");
        builder.Property(role => role.Description).HasMaxLength(300);
        builder.Property(role => role.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
    }
}

/// <summary>Maps sec.RolePermission (read only; the computed ViewAction column is not needed to read grants).</summary>
internal sealed class RoleGrantConfiguration : IEntityTypeConfiguration<RoleGrant>
{
    public void Configure(EntityTypeBuilder<RoleGrant> builder)
    {
        builder.ToTable("RolePermission", "sec");
        builder.HasKey(grant => new { grant.RoleId, grant.ModuleKey, grant.Action }).HasName("PK_RolePermission");
        builder.Property(grant => grant.ModuleKey).HasMaxLength(30).IsUnicode(false);
        builder.Property(grant => grant.Action).HasMaxLength(30).IsUnicode(false);
        builder.HasOne<AppRole>().WithMany().HasForeignKey(grant => grant.RoleId)
            .HasConstraintName("FK_RolePermission_Role").OnDelete(DeleteBehavior.Cascade);
    }
}
