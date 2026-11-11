using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

/// <summary>Maps att.Shift. CHECK constraints and defaults stay in the SSDT project.</summary>
internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shift", "att");
        builder.HasKey(shift => shift.Id).HasName("PK_Shift");
        builder.Property(shift => shift.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(shift => shift.Code).HasMaxLength(20);
        builder.HasIndex(shift => shift.Code).IsUnique().HasDatabaseName("UQ_Shift_Code");
        builder.Property(shift => shift.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.Property(shift => shift.Name).HasMaxLength(60);
        builder.Property(shift => shift.StartTime).HasColumnType("time(0)");
        builder.Property(shift => shift.EndTime).HasColumnType("time(0)");
        builder.Property(shift => shift.BreakMinutes).HasColumnType("smallint");
        builder.Property(shift => shift.GraceMinutes).HasColumnType("smallint");
        builder.Property(shift => shift.Color).HasMaxLength(7).IsUnicode(false);

        // Owned by the shift: the database removes them with it (ON DELETE CASCADE). Always loaded with the shift.
        builder.HasMany(shift => shift.WorkDays).WithOne()
            .HasForeignKey(day => day.ShiftId)
            .HasConstraintName("FK_ShiftWorkDay_Shift")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(shift => shift.WorkDays).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ShiftWorkDayConfiguration : IEntityTypeConfiguration<ShiftWorkDay>
{
    public void Configure(EntityTypeBuilder<ShiftWorkDay> builder)
    {
        builder.ToTable("ShiftWorkDay", "att");
        builder.HasKey(day => new { day.ShiftId, day.DayOfWeek }).HasName("PK_ShiftWorkDay");
        builder.Property(day => day.ShiftId).ValueGeneratedNever();
        builder.Property(day => day.DayOfWeek).HasColumnType("tinyint").ValueGeneratedNever();
    }
}

/// <summary>
/// Maps att.AttendanceRecord. Its key is a bigint in the table; the entity id is an int like every other, so the key is
/// converted (the table would need billions of rows to outgrow it).
/// </summary>
internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecord", "att");
        builder.HasKey(record => record.Id).HasName("PK_AttendanceRecord");
        builder.Property(record => record.Id).HasConversion<long>().HasColumnType("bigint").UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(record => record.Date).HasColumnType("date");
        builder.Property(record => record.CheckIn).HasColumnType("time(0)");
        builder.Property(record => record.CheckOut).HasColumnType("time(0)");
        builder.Property(record => record.WorkedMinutes).HasColumnType("smallint");
        builder.Property(record => record.LateMinutes).HasColumnType("smallint");
        builder.Property(record => record.EarlyLeaveMinutes).HasColumnType("smallint");
        builder.Property(record => record.OvertimeMinutes).HasColumnType("smallint");
        builder.Property(record => record.Status).HasTextConversion(AttendanceStatusText.Map);
        builder.Property(record => record.Source).HasTextConversion(AttendanceSourceText.Map);
        builder.Property(record => record.Remarks).HasMaxLength(AttendanceRecord.RemarksMaxLength);

        builder.HasIndex(record => new { record.EmployeeId, record.Date }).IsUnique().HasDatabaseName("UQ_AttendanceRecord_EmployeeId_Date");
        builder.HasIndex(record => record.Date).HasDatabaseName("IX_AttendanceRecord_Date");
        builder.HasIndex(record => record.ShiftId).HasDatabaseName("IX_AttendanceRecord_ShiftId");

        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(record => record.EmployeeId)
            .HasConstraintName("FK_AttendanceRecord_Employee")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Shift>().WithMany()
            .HasForeignKey(record => record.ShiftId)
            .HasConstraintName("FK_AttendanceRecord_Shift")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>
/// Maps att.AttendanceAdjustment. The table links to the record by employee and date (FK_AttendanceAdjustment_AttendanceRecord);
/// that foreign key is left to the database. The filtered unique index of pending requests is too: the handler explains a
/// clash first and the save translates the rest into a conflict.
/// </summary>
internal sealed class AttendanceAdjustmentConfiguration : IEntityTypeConfiguration<AttendanceAdjustment>
{
    public void Configure(EntityTypeBuilder<AttendanceAdjustment> builder)
    {
        builder.ToTable("AttendanceAdjustment", "att");
        builder.HasKey(adjustment => adjustment.Id).HasName("PK_AttendanceAdjustment");
        builder.Property(adjustment => adjustment.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(adjustment => adjustment.Date).HasColumnType("date");
        builder.Property(adjustment => adjustment.CurrentCheckIn).HasColumnType("time(0)");
        builder.Property(adjustment => adjustment.CurrentCheckOut).HasColumnType("time(0)");
        builder.Property(adjustment => adjustment.RequestedCheckIn).HasColumnType("time(0)");
        builder.Property(adjustment => adjustment.RequestedCheckOut).HasColumnType("time(0)");
        // A nullable enum: EF skips the converter for null, so the converter itself is the plain enum one.
        builder.Property(adjustment => adjustment.RequestedStatus)
            .HasConversion(new ValueConverter<AttendanceStatus, string>(
                status => AttendanceStatusText.ToText(status),
                text => AttendanceStatusText.Parse(text)))
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(adjustment => adjustment.Reason).HasMaxLength(AttendanceAdjustment.ReasonMaxLength);
        builder.Property(adjustment => adjustment.Status).HasTextConversion(ApprovalStatusText.Map);
        builder.Property(adjustment => adjustment.RequestedOn).HasColumnType("date");
        builder.Property(adjustment => adjustment.DecidedByName).HasMaxLength(130);
        builder.Property(adjustment => adjustment.DecidedOn).HasColumnType("date");
        builder.Property(adjustment => adjustment.Remarks).HasMaxLength(ApprovalRequest.RemarksMaxLength);

        builder.HasIndex(adjustment => new { adjustment.EmployeeId, adjustment.Date })
            .HasDatabaseName("IX_AttendanceAdjustment_EmployeeId_Date");
        builder.HasIndex(adjustment => adjustment.Status).HasDatabaseName("IX_AttendanceAdjustment_Status");
        builder.HasIndex(adjustment => new { adjustment.EmployeeId, adjustment.Date })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'")
            .HasDatabaseName("UQ_AttendanceAdjustment_EmployeeId_Date_Pending");
    }
}

/// <summary>Maps att.OvertimeRecord.</summary>
internal sealed class OvertimeRecordConfiguration : IEntityTypeConfiguration<OvertimeRecord>
{
    public void Configure(EntityTypeBuilder<OvertimeRecord> builder)
    {
        builder.ToTable("OvertimeRecord", "att");
        builder.HasKey(overtime => overtime.Id).HasName("PK_OvertimeRecord");
        builder.Property(overtime => overtime.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(overtime => overtime.Date).HasColumnType("date");
        builder.Property(overtime => overtime.StartTime).HasColumnType("time(0)");
        builder.Property(overtime => overtime.EndTime).HasColumnType("time(0)");
        builder.Property(overtime => overtime.Minutes).HasColumnType("smallint");
        builder.Property(overtime => overtime.Reason).HasMaxLength(OvertimeRecord.ReasonMaxLength);
        builder.Property(overtime => overtime.Status).HasTextConversion(ApprovalStatusText.Map);
        builder.Property(overtime => overtime.RequestedOn).HasColumnType("date");
        builder.Property(overtime => overtime.DecidedByName).HasMaxLength(130);
        builder.Property(overtime => overtime.DecidedOn).HasColumnType("date");
        builder.Property(overtime => overtime.Remarks).HasMaxLength(ApprovalRequest.RemarksMaxLength);

        builder.HasIndex(overtime => new { overtime.EmployeeId, overtime.Date }).IsUnique().HasDatabaseName("UQ_OvertimeRecord_EmployeeId_Date");
        builder.HasIndex(overtime => new { overtime.Status, overtime.Date }).HasDatabaseName("IX_OvertimeRecord_Status_Date");

        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(overtime => overtime.EmployeeId)
            .HasConstraintName("FK_OvertimeRecord_Employee")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
