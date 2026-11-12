using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps hr.Employee. The table is system-versioned (temporal): SQL Server keeps hr.EmployeeHistory itself, so the
/// history table and the period columns are not mapped. CHECK constraints and defaults stay in the SSDT project.
/// </summary>
internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employee", "hr");
        builder.HasKey(employee => employee.Id).HasName("PK_Employee");
        builder.Property(employee => employee.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(employee => employee.EmployeeCode).HasMaxLength(Employee.CodeMaxLength);
        builder.HasIndex(employee => employee.EmployeeCode).IsUnique().HasDatabaseName("UQ_Employee_EmployeeCode");
        builder.Property(employee => employee.FirstName).HasMaxLength(60);
        builder.Property(employee => employee.LastName).HasMaxLength(60);
        builder.Property(employee => employee.Gender).HasTextConversion(GenderText.Map);
        builder.Property(employee => employee.WorkEmail).HasMaxLength(120);
        builder.HasIndex(employee => employee.WorkEmail).IsUnique().HasDatabaseName("UQ_Employee_WorkEmail");
        builder.Property(employee => employee.PhotoUrl).HasMaxLength(400);

        ConfigurePersonal(builder);

        builder.Property(employee => employee.EmploymentType).HasTextConversion(EmploymentTypeText.Map);
        builder.Property(employee => employee.Status).HasColumnName("EmploymentStatus").HasTextConversion(EmploymentStatusText.Map);
        builder.Property(employee => employee.JoiningDate).HasColumnType("date");
        builder.Property(employee => employee.ProbationEndDate).HasColumnType("date");
        builder.Property(employee => employee.ExitDate).HasColumnType("date");

        ConfigureRelationships(builder);
    }

    private static void ConfigurePersonal(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(employee => employee.DateOfBirth).HasColumnType("date");
        builder.Property(employee => employee.MaritalStatus).HasTextConversion(MaritalStatusText.Map);
        builder.Property(employee => employee.Nationality).HasMaxLength(60);
        builder.Property(employee => employee.NationalId).HasMaxLength(40);
        builder.Property(employee => employee.PersonalEmail).HasMaxLength(120);
        builder.Property(employee => employee.Phone).HasMaxLength(25);
        builder.Property(employee => employee.AlternatePhone).HasMaxLength(25);

        // Value objects stored in the columns of the employee row (table splitting).
        builder.OwnsOne(employee => employee.Address, address =>
        {
            address.Property(a => a.Line1).HasColumnName("AddressLine1").HasMaxLength(120);
            address.Property(a => a.Line2).HasColumnName("AddressLine2").HasMaxLength(120);
            address.Property(a => a.City).HasColumnName("AddressCity").HasMaxLength(60);
            address.Property(a => a.State).HasColumnName("AddressState").HasMaxLength(60);
            address.Property(a => a.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(12);
            address.Property(a => a.Country).HasColumnName("AddressCountry").HasMaxLength(60);
        });
        builder.Navigation(employee => employee.Address).IsRequired();

        builder.OwnsOne(employee => employee.EmergencyContact, contact =>
        {
            contact.Property(c => c.Name).HasColumnName("EmergencyContactName").HasMaxLength(80);
            contact.Property(c => c.Relationship).HasColumnName("EmergencyContactRelationship").HasTextConversion(EmergencyRelationshipText.Map);
            contact.Property(c => c.Phone).HasColumnName("EmergencyContactPhone").HasMaxLength(25);
        });
        builder.Navigation(employee => employee.EmergencyContact).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Employee> builder)
    {
        // The database foreign keys to Branch, Department, Section and Position are composite (for example BranchId +
        // CompanyId), so an employee cannot sit in a department of another branch. As for the organization tables, only
        // the plain parent column is modeled (a composite or alternate key would make the placement immutable in EF);
        // EmployeeRules explains violations first. There is no foreign key on CompanyId alone.
        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(employee => employee.BranchId)
            .HasConstraintName("FK_Employee_Branch")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(employee => employee.DepartmentId)
            .HasConstraintName("FK_Employee_Department")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Section>().WithMany()
            .HasForeignKey(employee => employee.SectionId)
            .HasConstraintName("FK_Employee_Section")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Position>().WithMany()
            .HasForeignKey(employee => employee.PositionId)
            .HasConstraintName("FK_Employee_Position")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<JobGrade>().WithMany()
            .HasForeignKey(employee => employee.JobGradeId)
            .HasConstraintName("FK_Employee_JobGrade")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(employee => employee.ManagerEmployeeId)
            .HasConstraintName("FK_Employee_Employee_ManagerEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        // ShiftId points at att.Shift, which is not ported yet: a plain column; the database foreign key still applies.

        // Owned by the employee: the database removes them with it (ON DELETE CASCADE). The bank account is always
        // loaded with the employee, so responses can show the masked account number without a second query.
        builder.HasOne(employee => employee.BankAccount).WithOne()
            .HasForeignKey<EmployeeBankAccount>(account => account.EmployeeId)
            .HasConstraintName("FK_EmployeeBankAccount_Employee")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(employee => employee.BankAccount).AutoInclude();

        builder.HasMany(employee => employee.History).WithOne()
            .HasForeignKey(entry => entry.EmployeeId)
            .HasConstraintName("FK_EmploymentHistory_Employee")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(employee => employee.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes of the SSDT project that serve the list filters and the foreign keys.
        builder.HasIndex(employee => new { employee.BranchId, employee.CompanyId }).HasDatabaseName("IX_Employee_BranchId_CompanyId");
        builder.HasIndex(employee => employee.Status).HasDatabaseName("IX_Employee_EmploymentStatus");
        builder.HasIndex(employee => employee.JoiningDate).HasDatabaseName("IX_Employee_JoiningDate");
    }
}

internal sealed class EmployeeBankAccountConfiguration : IEntityTypeConfiguration<EmployeeBankAccount>
{
    public void Configure(EntityTypeBuilder<EmployeeBankAccount> builder)
    {
        builder.ToTable("EmployeeBankAccount", "hr");
        builder.HasKey(account => account.Id).HasName("PK_EmployeeBankAccount");
        builder.Property(account => account.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.HasIndex(account => account.EmployeeId).IsUnique().HasDatabaseName("UQ_EmployeeBankAccount_EmployeeId");
        builder.Property(account => account.BankName).HasMaxLength(60);
        builder.Property(account => account.BranchName).HasMaxLength(60);
        builder.Property(account => account.AccountName).HasMaxLength(80);
        builder.Property(account => account.AccountNumber).HasMaxLength(20).IsUnicode(false);
    }
}

internal sealed class EmploymentHistoryEntryConfiguration : IEntityTypeConfiguration<EmploymentHistoryEntry>
{
    public void Configure(EntityTypeBuilder<EmploymentHistoryEntry> builder)
    {
        builder.ToTable("EmploymentHistory", "hr");
        builder.HasKey(entry => entry.Id).HasName("PK_EmploymentHistory");
        builder.Property(entry => entry.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(entry => entry.Date).HasColumnType("date");
        builder.Property(entry => entry.Type).HasTextConversion(HistoryEventTypeText.Map);
        builder.Property(entry => entry.Title).HasMaxLength(EmploymentHistoryEntry.TitleMaxLength);
        builder.Property(entry => entry.Details).HasMaxLength(EmploymentHistoryEntry.DetailsMaxLength);
        builder.Property(entry => entry.RecordedBy).HasMaxLength(EmploymentHistoryEntry.RecordedByMaxLength);
        builder.HasIndex(entry => new { entry.EmployeeId, entry.Date }).HasDatabaseName("IX_EmploymentHistory_EmployeeId_Date");
    }
}

internal sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("EmployeeDocument", "hr");
        builder.HasKey(document => document.Id).HasName("PK_EmployeeDocument");
        builder.Property(document => document.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(document => document.Type).HasTextConversion(DocumentTypeText.Map);
        builder.Property(document => document.Title).HasMaxLength(80);
        builder.Property(document => document.FileName).HasMaxLength(260);
        builder.Property(document => document.UploadedOn).HasColumnType("date");
        builder.Property(document => document.ExpiryDate).HasColumnType("date");
        builder.HasIndex(document => document.EmployeeId).HasDatabaseName("IX_EmployeeDocument_EmployeeId");

        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(document => document.EmployeeId)
            .HasConstraintName("FK_EmployeeDocument_Employee")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps hr.EmployeeBlacklist (system-versioned: the history table and the period columns stay in the database project).</summary>
internal sealed class EmployeeBlacklistEntryConfiguration : IEntityTypeConfiguration<EmployeeBlacklistEntry>
{
    public void Configure(EntityTypeBuilder<EmployeeBlacklistEntry> builder)
    {
        builder.ToTable("EmployeeBlacklist", "hr");
        builder.HasKey(entry => entry.Id).HasName("PK_EmployeeBlacklist");
        builder.Property(entry => entry.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(entry => entry.Reason).HasMaxLength(EmployeeBlacklistEntry.ReasonMaxLength);
        builder.Property(entry => entry.BlacklistedOn).HasColumnType("date");
        builder.Property(entry => entry.BlacklistedByName).HasMaxLength(EmployeeBlacklistEntry.NameMaxLength);
        builder.Property(entry => entry.LiftedOn).HasColumnType("date");
        builder.Property(entry => entry.LiftedByName).HasMaxLength(EmployeeBlacklistEntry.NameMaxLength);
        builder.Property(entry => entry.LiftReason).HasMaxLength(EmployeeBlacklistEntry.ReasonMaxLength);

        // One active entry per employee is a filtered unique index in the database (UQ_EmployeeBlacklist_ActiveEmployee).
        builder.HasIndex(entry => entry.EmployeeId).HasDatabaseName("IX_EmployeeBlacklist_EmployeeId");
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(entry => entry.EmployeeId)
            .HasConstraintName("FK_EmployeeBlacklist_Employee")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
