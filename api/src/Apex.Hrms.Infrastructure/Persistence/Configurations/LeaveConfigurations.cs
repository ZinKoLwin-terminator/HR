using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>Maps lve.LeaveType. CHECK constraints and defaults stay in the SSDT project.</summary>
internal sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("LeaveType", "lve");
        builder.HasKey(type => type.Id).HasName("PK_LeaveType");
        builder.Property(type => type.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(type => type.Code).HasMaxLength(20);
        builder.HasIndex(type => type.Code).IsUnique().HasDatabaseName("UQ_LeaveType_Code");
        builder.Property(type => type.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(type => type.Name).HasMaxLength(60);
        builder.Property(type => type.Description).HasMaxLength(300);
        builder.Property(type => type.DefaultDaysPerYear).HasColumnType("decimal(5,1)");
        builder.Property(type => type.MaxConsecutiveDays).HasColumnType("smallint");
        builder.Property(type => type.MaxCarryForwardDays).HasColumnType("decimal(5,1)");
        builder.Property(type => type.Color).HasMaxLength(7).IsUnicode(false);

        // A nullable enum: EF skips the converter for null, so the converter itself is the plain enum one.
        builder.Property(type => type.EligibleGender)
            .HasConversion(new ValueConverter<Gender, string>(gender => GenderText.ToText(gender), text => GenderText.Parse(text)))
            .HasMaxLength(30)
            .IsUnicode(false);
    }
}

/// <summary>Maps lve.LeavePolicy.</summary>
internal sealed class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> builder)
    {
        builder.ToTable("LeavePolicy", "lve");
        builder.HasKey(policy => policy.Id).HasName("PK_LeavePolicy");
        builder.Property(policy => policy.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(policy => policy.Name).HasMaxLength(100);
        builder.HasIndex(policy => policy.Name).IsUnique().HasDatabaseName("UQ_LeavePolicy_Name");
        builder.Property(policy => policy.EmploymentType)
            .HasConversion(new ValueConverter<EmploymentType, string>(
                type => EmploymentTypeText.ToText(type),
                text => EmploymentTypeText.Parse(text)))
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(policy => policy.DaysPerYear).HasColumnType("decimal(5,1)");
        builder.Property(policy => policy.Accrual).HasTextConversion(LeaveAccrualText.Map);
        builder.Property(policy => policy.MinServiceMonths).HasColumnType("smallint");
        builder.Property(policy => policy.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);

        // The database also has a filtered unique index UQ_LeavePolicy_ActiveGroup (type, grade, employment type of
        // active policies); LeavePolicyRules explains a clash first.
        builder.HasOne<LeaveType>().WithMany()
            .HasForeignKey(policy => policy.LeaveTypeId)
            .HasConstraintName("FK_LeavePolicy_LeaveType")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<JobGrade>().WithMany()
            .HasForeignKey(policy => policy.JobGradeId)
            .HasConstraintName("FK_LeavePolicy_JobGrade")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>
/// Maps lve.LeaveApplication. The table is system-versioned (temporal): SQL Server keeps lve.LeaveApplicationHistory
/// itself, so the history table and the period columns are not mapped.
/// </summary>
internal sealed class LeaveApplicationConfiguration : IEntityTypeConfiguration<LeaveApplication>
{
    public void Configure(EntityTypeBuilder<LeaveApplication> builder)
    {
        builder.ToTable("LeaveApplication", "lve");
        builder.HasKey(application => application.Id).HasName("PK_LeaveApplication");
        builder.Property(application => application.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(application => application.ApplicationNo).HasMaxLength(20);
        builder.HasIndex(application => application.ApplicationNo).IsUnique().HasDatabaseName("UQ_LeaveApplication_ApplicationNo");
        builder.Property(application => application.StartDate).HasColumnType("date");
        builder.Property(application => application.EndDate).HasColumnType("date");
        builder.Property(application => application.Days).HasColumnType("decimal(4,1)");
        builder.Property(application => application.Reason).HasMaxLength(500);
        builder.Property(application => application.DocumentName).HasMaxLength(260);
        builder.Property(application => application.Status).HasTextConversion(LeaveStatusText.Map);
        builder.Property(application => application.AppliedOn).HasColumnType("date");
        builder.Property(application => application.ActionedByName).HasMaxLength(130);
        builder.Property(application => application.ActionedOn).HasColumnType("date");
        builder.Property(application => application.ApproverRemarks).HasMaxLength(250);

        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(application => application.EmployeeId)
            .HasConstraintName("FK_LeaveApplication_Employee")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<LeaveType>().WithMany()
            .HasForeignKey(application => application.LeaveTypeId)
            .HasConstraintName("FK_LeaveApplication_LeaveType")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(application => application.ApproverEmployeeId)
            .HasConstraintName("FK_LeaveApplication_Employee_ApproverEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        // Indexes of the SSDT project that serve the balance, overlap and calendar queries.
        builder.HasIndex(application => new { application.EmployeeId, application.StartDate }).HasDatabaseName("IX_LeaveApplication_EmployeeId_StartDate");
        builder.HasIndex(application => new { application.Status, application.StartDate }).HasDatabaseName("IX_LeaveApplication_Status_StartDate");
    }
}

/// <summary>Maps att.Holiday. Attendance writes it; the leave module reads it to count working days and show its calendar.</summary>
internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holiday", "att");
        builder.HasKey(holiday => holiday.Id).HasName("PK_Holiday");
        builder.Property(holiday => holiday.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(holiday => holiday.Date).HasColumnType("date");
        builder.Property(holiday => holiday.Name).HasMaxLength(100);
        builder.Property(holiday => holiday.Type).HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(holiday => holiday.Date).HasDatabaseName("IX_Holiday_Date");
    }
}
