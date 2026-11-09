using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the standard Identity store entities onto the auth schema of the database project: auth.User, auth.UserClaim,
/// auth.UserLogin and auth.UserToken (no role tables). Names, lengths and key names follow the SSDT tables exactly;
/// the defaults of Identity (AspNetUsers ...) are replaced.
/// </summary>
internal static class IdentityModelConfiguration
{
    public const string Schema = "auth";

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityUser>(user =>
        {
            user.ToTable("User", Schema);
            user.HasKey(u => u.Id).HasName("PK_User");
            user.Property(u => u.Id).HasMaxLength(450);
            user.Property(u => u.UserName).HasMaxLength(256);
            user.Property(u => u.NormalizedUserName).HasMaxLength(256);
            user.Property(u => u.Email).HasMaxLength(256);
            user.Property(u => u.NormalizedEmail).HasMaxLength(256);
            user.Property(u => u.PhoneNumber).HasMaxLength(50);
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("UQ_User_NormalizedUserName").IsUnique();
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("IX_User_NormalizedEmail");
        });

        modelBuilder.Entity<IdentityUserClaim<string>>(claim =>
        {
            claim.ToTable("UserClaim", Schema);
            claim.HasKey(c => c.Id).HasName("PK_UserClaim");
            claim.Property(c => c.UserId).HasMaxLength(450);
            claim.HasIndex(c => c.UserId).HasDatabaseName("IX_UserClaim_UserId");
            claim.HasOne<IdentityUser>().WithMany().HasForeignKey(c => c.UserId)
                .HasConstraintName("FK_UserClaim_User").OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdentityUserLogin<string>>(login =>
        {
            login.ToTable("UserLogin", Schema);
            login.HasKey(l => new { l.LoginProvider, l.ProviderKey }).HasName("PK_UserLogin");
            login.Property(l => l.LoginProvider).HasMaxLength(128);
            login.Property(l => l.ProviderKey).HasMaxLength(128);
            login.Property(l => l.UserId).HasMaxLength(450);
            login.HasIndex(l => l.UserId).HasDatabaseName("IX_UserLogin_UserId");
            login.HasOne<IdentityUser>().WithMany().HasForeignKey(l => l.UserId)
                .HasConstraintName("FK_UserLogin_User").OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdentityUserToken<string>>(token =>
        {
            token.ToTable("UserToken", Schema);
            token.HasKey(t => new { t.UserId, t.LoginProvider, t.Name }).HasName("PK_UserToken");
            token.Property(t => t.UserId).HasMaxLength(450);
            token.Property(t => t.LoginProvider).HasMaxLength(128);
            token.Property(t => t.Name).HasMaxLength(128);
            token.HasOne<IdentityUser>().WithMany().HasForeignKey(t => t.UserId)
                .HasConstraintName("FK_UserToken_User").OnDelete(DeleteBehavior.Cascade);
        });
    }
}
