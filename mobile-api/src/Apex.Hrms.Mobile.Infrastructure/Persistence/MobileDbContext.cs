using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Mobile.Infrastructure.Persistence;

/// <summary>
/// The tables of the database project (HR DBPJ) the mobile API touches, and only the columns it uses. It writes one table,
/// auth.UserToken (the sessions); everything else is read. EF Core never creates or migrates anything: the schema is
/// published from the SSDT project, and <c>MobileSchemaMappingTests</c> checks these mappings against it.
/// </summary>
internal sealed class MobileDbContext(DbContextOptions<MobileDbContext> options) : DbContext(options)
{
    public DbSet<SessionTokenRow> SessionTokens => Set<SessionTokenRow>();

    public DbSet<IdentityUserRow> IdentityUsers => Set<IdentityUserRow>();

    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<EmployeeRow> Employees => Set<EmployeeRow>();

    public DbSet<DepartmentRow> Departments => Set<DepartmentRow>();

    public DbSet<PositionRow> Positions => Set<PositionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionTokenRow>(token =>
        {
            token.ToTable("UserToken", "auth");
            token.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
            token.Property(t => t.UserId).HasMaxLength(450);
            token.Property(t => t.LoginProvider).HasMaxLength(128);
            token.Property(t => t.Name).HasMaxLength(128);
            token.Property(t => t.Value);
        });

        modelBuilder.Entity<IdentityUserRow>(user =>
        {
            user.ToTable("User", "auth");
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).HasMaxLength(450);
        });

        modelBuilder.Entity<AccountRow>(account =>
        {
            account.ToTable("AppUser", "sec");
            account.HasKey(a => a.Id);
            account.Property(a => a.ExternalSubject).HasMaxLength(450);
        });

        modelBuilder.Entity<EmployeeRow>(employee =>
        {
            employee.ToTable("Employee", "hr");
            employee.HasKey(e => e.Id);
            employee.Property(e => e.EmployeeCode).HasMaxLength(12);
            employee.Property(e => e.FirstName).HasMaxLength(60);
            employee.Property(e => e.LastName).HasMaxLength(60);
            employee.Property(e => e.PhotoUrl).HasMaxLength(400);
        });

        modelBuilder.Entity<DepartmentRow>(department =>
        {
            department.ToTable("Department", "org");
            department.HasKey(d => d.Id);
            department.Property(d => d.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<PositionRow>(position =>
        {
            position.ToTable("Position", "org");
            position.HasKey(p => p.Id);
            position.Property(p => p.Title).HasMaxLength(100);
        });
    }
}

/// <summary>auth.UserToken: one row per mobile session (LoginProvider "hrms-mobile", Name = the device id).</summary>
internal sealed class SessionTokenRow
{
    public string UserId { get; set; } = string.Empty;

    public string LoginProvider { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Value { get; set; }
}

/// <summary>auth.User, read only: the security stamp a session is checked against.</summary>
internal sealed class IdentityUserRow
{
    public string Id { get; set; } = string.Empty;

    public string? SecurityStamp { get; set; }
}

/// <summary>sec.AppUser, read only: which identity user an employee signs in with.</summary>
internal sealed class AccountRow
{
    public int Id { get; set; }

    public int? EmployeeId { get; set; }

    public string? ExternalSubject { get; set; }
}

/// <summary>hr.Employee, read only: the work identity (no personal data columns are mapped).</summary>
internal sealed class EmployeeRow
{
    public int Id { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public int DepartmentId { get; set; }

    public int PositionId { get; set; }

    public string? PhotoUrl { get; set; }
}

/// <summary>org.Department, read only.</summary>
internal sealed class DepartmentRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>org.Position, read only.</summary>
internal sealed class PositionRow
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
}
