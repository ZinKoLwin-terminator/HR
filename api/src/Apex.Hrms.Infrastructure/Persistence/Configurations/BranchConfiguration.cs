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
        // Target of the composite foreign keys that keep Department (and Employee) inside the branch's company.
        builder.HasAlternateKey(b => new { b.Id, b.CompanyId }).HasName("UQ_Branch_Id_CompanyId");

        builder.HasOne<Company>().WithMany()
            .HasForeignKey(b => b.CompanyId)
            .HasConstraintName("FK_Branch_Company")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EmployeeReference>().WithMany()
            .HasForeignKey(b => b.ManagerEmployeeId)
            .HasConstraintName("FK_Branch_Employee_ManagerEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
