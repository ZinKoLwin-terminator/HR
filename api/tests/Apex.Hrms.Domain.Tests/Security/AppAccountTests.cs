using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Domain.Tests.Security;

public sealed class AppAccountTests
{
    private static AppAccount Unlinked() => AppAccount.Create(null, "Bootstrap Admin", "tst.admin@example.test", roleId: 1, "tst-subject");

    [Fact]
    public void LinkEmployee_links_an_unlinked_account_and_takes_the_trimmed_employee_name()
    {
        var account = Unlinked();

        var result = account.LinkEmployee(7, "  Mia Hartley ");

        Assert.True(result.IsSuccess);
        Assert.Equal(7, account.EmployeeId);
        Assert.Equal("Mia Hartley", account.DisplayName);
        Assert.Equal("tst.admin@example.test", account.Email);
        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void LinkEmployee_refuses_a_linked_account_and_leaves_it_unchanged()
    {
        var account = AppAccount.Create(3, "Mia Hartley", "mia.hartley@example.test", roleId: 1, "tst-subject");

        var result = account.LinkEmployee(7, "Other Person");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(AppAccount.AlreadyLinkedMessage, result.Error.Message);
        Assert.Equal("This account is already linked to an employee.", AppAccount.AlreadyLinkedMessage);
        Assert.Equal(3, account.EmployeeId);
        Assert.Equal("Mia Hartley", account.DisplayName);
    }

    [Fact]
    public void LinkEmployee_can_happen_only_once()
    {
        var account = Unlinked();
        Assert.True(account.LinkEmployee(7, "Mia Hartley").IsSuccess);

        Assert.True(account.LinkEmployee(8, "Other Person").IsFailure);
        Assert.Equal(7, account.EmployeeId);
    }

    [Theory]
    [InlineData(0, "Mia Hartley")]
    [InlineData(-1, "Mia Hartley")]
    public void LinkEmployee_needs_a_real_employee_id(int employeeId, string name) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Unlinked().LinkEmployee(employeeId, name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LinkEmployee_needs_a_name(string name) =>
        Assert.Throws<ArgumentException>(() => Unlinked().LinkEmployee(7, name));
}
