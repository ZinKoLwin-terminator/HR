using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

// The pay schema. CHECK constraints, defaults, triggers and the system versioning of the temporal tables stay in the SSDT
// project (SQL Server keeps pay.SalaryAssignmentHistory and pay.PayrollRunHistory itself, so the period columns and the
// history tables are not mapped). EF never creates or migrates any of it.

/// <summary>Maps pay.SalaryComponent.</summary>
internal sealed class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.ToTable("SalaryComponent", "pay");
        builder.HasKey(component => component.Id).HasName("PK_SalaryComponent");
        builder.Property(component => component.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(component => component.Code).HasMaxLength(20);
        builder.HasIndex(component => component.Code).IsUnique().HasDatabaseName("UQ_SalaryComponent_Code");
        builder.Property(component => component.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(component => component.Name).HasMaxLength(60);
        builder.Property(component => component.Type).HasTextConversion(ComponentTypeText.Map);
        builder.Property(component => component.Calculation).HasTextConversion(CalculationMethodText.Map);
        builder.Property(component => component.Value).HasColumnType("decimal(18,2)");
        builder.Property(component => component.Description).HasMaxLength(250);
    }
}

/// <summary>Maps pay.SalaryStructure and its components (owned by the structure: the database removes them with it).</summary>
internal sealed class SalaryStructureConfiguration : IEntityTypeConfiguration<SalaryStructure>
{
    public void Configure(EntityTypeBuilder<SalaryStructure> builder)
    {
        builder.ToTable("SalaryStructure", "pay");
        builder.HasKey(structure => structure.Id).HasName("PK_SalaryStructure");
        builder.Property(structure => structure.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(structure => structure.Code).HasMaxLength(20);
        builder.HasIndex(structure => structure.Code).IsUnique().HasDatabaseName("UQ_SalaryStructure_Code");
        builder.Property(structure => structure.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(structure => structure.Name).HasMaxLength(60);
        builder.Property(structure => structure.Description).HasMaxLength(250);

        builder.Ignore(structure => structure.ComponentIds);
        builder.HasMany(structure => structure.Components).WithOne()
            .HasForeignKey(link => link.SalaryStructureId)
            .HasConstraintName("FK_SalaryStructureComponent_SalaryStructure")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(structure => structure.Components).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class SalaryStructureComponentConfiguration : IEntityTypeConfiguration<SalaryStructureComponent>
{
    public void Configure(EntityTypeBuilder<SalaryStructureComponent> builder)
    {
        builder.ToTable("SalaryStructureComponent", "pay");
        builder.HasKey(link => new { link.SalaryStructureId, link.SalaryComponentId }).HasName("PK_SalaryStructureComponent");
        builder.Property(link => link.SortOrder).HasColumnType("smallint");
        builder.HasIndex(link => new { link.SalaryStructureId, link.SortOrder }).IsUnique().HasDatabaseName("UQ_SalaryStructureComponent_SortOrder");
        builder.HasIndex(link => link.SalaryComponentId).HasDatabaseName("IX_SalaryStructureComponent_SalaryComponentId");

        builder.HasOne<SalaryComponent>().WithMany()
            .HasForeignKey(link => link.SalaryComponentId)
            .HasConstraintName("FK_SalaryStructureComponent_SalaryComponent")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>Maps pay.SalaryAssignment (temporal).</summary>
internal sealed class SalaryAssignmentConfiguration : IEntityTypeConfiguration<SalaryAssignment>
{
    public void Configure(EntityTypeBuilder<SalaryAssignment> builder)
    {
        builder.ToTable("SalaryAssignment", "pay");
        builder.HasKey(assignment => assignment.Id).HasName("PK_SalaryAssignment");
        builder.Property(assignment => assignment.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(assignment => assignment.BasicSalary).HasColumnType("decimal(18,2)");
        builder.Property(assignment => assignment.EffectiveFrom).HasColumnType("date");
        builder.Property(assignment => assignment.Remarks).HasMaxLength(SalaryAssignment.RemarksMaxLength);

        builder.HasIndex(assignment => new { assignment.EmployeeId, assignment.EffectiveFrom })
            .IsUnique()
            .HasDatabaseName("UQ_SalaryAssignment_EmployeeId_EffectiveFrom");
        builder.HasIndex(assignment => assignment.SalaryStructureId).HasDatabaseName("IX_SalaryAssignment_SalaryStructureId");

        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(assignment => assignment.EmployeeId)
            .HasConstraintName("FK_SalaryAssignment_Employee")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<SalaryStructure>().WithMany()
            .HasForeignKey(assignment => assignment.SalaryStructureId)
            .HasConstraintName("FK_SalaryAssignment_SalaryStructure")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>
/// Maps pay.PayrollRun (temporal) and its warnings. Triggers lock an approved run: the run's status and the order of the
/// statements decide what the database accepts (see <see cref="Application.Abstractions.IPayrollRunOutput"/>).
/// </summary>
internal sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        // The table has a trigger: EF must not use an OUTPUT clause when it saves (SQL Server refuses that on a table with triggers).
        builder.ToTable("PayrollRun", "pay", table => table.HasTrigger("TR_PayrollRun_PaidIsFinal"));
        builder.HasKey(run => run.Id).HasName("PK_PayrollRun");
        builder.Property(run => run.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(run => run.PeriodName).HasMaxLength(30);
        builder.Property(run => run.Year).HasColumnType("smallint").HasConversion<short>();
        builder.Property(run => run.Month).HasColumnType("tinyint").HasConversion<byte>();
        builder.Property(run => run.StartDate).HasColumnType("date");
        builder.Property(run => run.EndDate).HasColumnType("date");
        builder.Property(run => run.PayDate).HasColumnType("date");
        builder.Property(run => run.Status).HasTextConversion(PayrollRunStatusText.Map);
        builder.Property(run => run.TotalBasic).HasColumnType("decimal(18,2)");
        builder.Property(run => run.TotalAllowances).HasColumnType("decimal(18,2)");
        builder.Property(run => run.TotalGross).HasColumnType("decimal(18,2)");
        builder.Property(run => run.TotalDeductions).HasColumnType("decimal(18,2)");
        builder.Property(run => run.TotalNet).HasColumnType("decimal(18,2)");
        builder.Property(run => run.ProcessedOn).HasColumnType("date");
        builder.Property(run => run.ProcessedByName).HasMaxLength(PayrollRun.UserNameMaxLength);
        builder.Property(run => run.ApprovedByName).HasMaxLength(PayrollRun.UserNameMaxLength);
        builder.Property(run => run.ApprovedOn).HasColumnType("date");
        builder.Property(run => run.PaidOn).HasColumnType("date");

        builder.HasIndex(run => new { run.Year, run.Month }).IsUnique().HasDatabaseName("UQ_PayrollRun_Year_Month");

        builder.HasMany(run => run.Warnings).WithOne()
            .HasForeignKey(warning => warning.PayrollRunId)
            .HasConstraintName("FK_PayrollRunWarning_PayrollRun")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(run => run.Warnings).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class PayrollRunWarningConfiguration : IEntityTypeConfiguration<PayrollRunWarning>
{
    public void Configure(EntityTypeBuilder<PayrollRunWarning> builder)
    {
        builder.ToTable("PayrollRunWarning", "pay", table => table.HasTrigger("TR_PayrollRunWarning_PaidRunIsFinal"));
        builder.HasKey(warning => warning.Id).HasName("PK_PayrollRunWarning");
        builder.Property(warning => warning.Id).UseIdentityColumn();
        builder.Property(warning => warning.Message).HasMaxLength(PayrollRunWarning.MessageMaxLength);
        builder.Property(warning => warning.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(warning => warning.PayrollRunId).HasDatabaseName("IX_PayrollRunWarning_PayrollRunId");
    }
}

/// <summary>
/// Maps pay.Payslip and its lines (owned by the payslip: the database removes them with it). The payslip holds only the
/// masked bank account; the table refuses anything but four bullets, a space and four digits.
/// </summary>
internal sealed class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> builder)
    {
        builder.ToTable("Payslip", "pay", table => table.HasTrigger("TR_Payslip_PaidRunIsFinal"));
        builder.HasKey(payslip => payslip.Id).HasName("PK_Payslip");
        builder.Property(payslip => payslip.Id).UseIdentityColumn();

        builder.Property(payslip => payslip.PeriodName).HasMaxLength(30);
        builder.Property(payslip => payslip.EmployeeCode).HasMaxLength(Employee.CodeMaxLength);
        builder.Property(payslip => payslip.EmployeeName).HasMaxLength(130);
        builder.Property(payslip => payslip.DepartmentName).HasMaxLength(100);
        builder.Property(payslip => payslip.PositionTitle).HasMaxLength(100);
        builder.Property(payslip => payslip.CompanyName).HasMaxLength(100);
        builder.Property(payslip => payslip.BankName).HasMaxLength(60);
        builder.Property(payslip => payslip.AccountMasked).HasMaxLength(9);
        builder.Property(payslip => payslip.BasicSalary).HasColumnType("decimal(18,2)");
        builder.Property(payslip => payslip.GrossSalary).HasColumnType("decimal(18,2)");
        builder.Property(payslip => payslip.TotalDeductions).HasColumnType("decimal(18,2)");
        builder.Property(payslip => payslip.NetSalary).HasColumnType("decimal(18,2)");
        builder.Property(payslip => payslip.PeriodDays).HasColumnType("tinyint").HasConversion<byte>();
        builder.Property(payslip => payslip.DaysEmployed).HasColumnType("tinyint").HasConversion<byte>();
        builder.Property(payslip => payslip.WorkingDays).HasColumnType("tinyint").HasConversion<byte>();
        builder.Property(payslip => payslip.UnpaidLeaveDays).HasColumnType("decimal(4,1)");
        builder.Property(payslip => payslip.GeneratedOn).HasColumnType("date");
        builder.Property(payslip => payslip.CreatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasIndex(payslip => new { payslip.PayrollRunId, payslip.EmployeeId }).IsUnique().HasDatabaseName("UQ_Payslip_PayrollRunId_EmployeeId");
        builder.HasIndex(payslip => payslip.EmployeeId).HasDatabaseName("IX_Payslip_EmployeeId");
        builder.HasIndex(payslip => payslip.DepartmentId).HasDatabaseName("IX_Payslip_DepartmentId");

        builder.HasOne<PayrollRun>().WithMany()
            .HasForeignKey(payslip => payslip.PayrollRunId)
            .HasConstraintName("FK_Payslip_PayrollRun")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(payslip => payslip.EmployeeId)
            .HasConstraintName("FK_Payslip_Employee")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(payslip => payslip.Earnings);
        builder.Ignore(payslip => payslip.Deductions);
        builder.HasMany(payslip => payslip.Lines).WithOne()
            .HasForeignKey(line => line.PayslipId)
            .HasConstraintName("FK_PayslipLine_Payslip")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(payslip => payslip.Lines).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class PayslipLineConfiguration : IEntityTypeConfiguration<PayslipLine>
{
    public void Configure(EntityTypeBuilder<PayslipLine> builder)
    {
        builder.ToTable("PayslipLine", "pay", table => table.HasTrigger("TR_PayslipLine_PaidRunIsFinal"));
        builder.HasKey(line => line.Id).HasName("PK_PayslipLine");
        builder.Property(line => line.Id).UseIdentityColumn();

        builder.Property(line => line.LineType).HasTextConversion(PayLineTypeText.Map);
        builder.Property(line => line.SortOrder).HasColumnType("smallint");
        builder.Property(line => line.Code).HasMaxLength(20);
        builder.Property(line => line.Name).HasMaxLength(60);
        builder.Property(line => line.Amount).HasColumnType("decimal(18,2)");
        builder.Property(line => line.Basis).HasMaxLength(100);
        builder.HasIndex(line => new { line.PayslipId, line.LineType, line.SortOrder })
            .IsUnique()
            .HasDatabaseName("UQ_PayslipLine_PayslipId_LineType_SortOrder");
    }
}
