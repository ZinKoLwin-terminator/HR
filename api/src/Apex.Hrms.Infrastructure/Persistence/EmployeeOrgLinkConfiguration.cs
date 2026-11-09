using Apex.Hrms.Application.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Maps the read-only <see cref="EmployeeOrgLink"/> onto the view hr.EmployeeOrgLink (placement without personal data).
/// The organization reference checks count employees through it; it is never written.
/// </summary>
internal sealed class EmployeeOrgLinkConfiguration : IEntityTypeConfiguration<EmployeeOrgLink>
{
    public void Configure(EntityTypeBuilder<EmployeeOrgLink> builder)
    {
        builder.ToView("EmployeeOrgLink", "hr");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).ValueGeneratedNever();
    }
}
