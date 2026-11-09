using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration() : CodedRecordConfiguration<Branch>("Branch")
{
    protected override void ConfigureColumns(EntityTypeBuilder<Branch> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(100);
        builder.Property(b => b.Type)
            .HasConversion(new ValueConverter<BranchType, string>(
                type => BranchTypeText.ToText(type),
                text => BranchTypeText.Parse(text)))
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(b => b.Email).HasMaxLength(120);
        builder.Property(b => b.Phone).HasMaxLength(25);
        builder.Property(b => b.Address).HasMaxLength(200);
        builder.Property(b => b.City).HasMaxLength(60);
        builder.Property(b => b.Country).HasMaxLength(60);
        builder.Property(b => b.OpenedDate).HasColumnType("date");
    }

    protected override void ConfigureRelationships(EntityTypeBuilder<Branch> builder)
    {
        // The database also has UQ_Branch_Id_CompanyId and composite foreign keys that keep Department (and
        // Employee) inside the branch's company. They are deliberately not modeled: an alternate key would make
        // CompanyId immutable in EF, so moving a branch to another company would throw. The database still
        // enforces them, and BranchRules reports a clear error before the database has to.
        builder.HasOne<Company>().WithMany()
            .HasForeignKey(b => b.CompanyId)
            .HasConstraintName("FK_Branch_Company")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(b => b.ManagerEmployeeId)
            .HasConstraintName("FK_Branch_Employee_ManagerEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
