using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration() : CodedRecordConfiguration<Company>("Company")
{
    protected override void ConfigureColumns(EntityTypeBuilder<Company> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("UQ_Company_Name");
        builder.Property(c => c.LegalName).HasMaxLength(150);
        builder.Property(c => c.RegistrationNumber).HasMaxLength(40);
        builder.Property(c => c.TaxId).HasMaxLength(40);
        builder.Property(c => c.Industry).HasMaxLength(80);
        builder.Property(c => c.Email).HasMaxLength(120);
        builder.Property(c => c.Phone).HasMaxLength(25);
        builder.Property(c => c.Website).HasMaxLength(150);
        builder.Property(c => c.Address).HasMaxLength(200);
        builder.Property(c => c.City).HasMaxLength(60);
        builder.Property(c => c.Country).HasMaxLength(60);
        builder.Property(c => c.FoundedDate).HasColumnType("date");
    }
}
