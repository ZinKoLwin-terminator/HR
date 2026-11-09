namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Rows written with plain SQL, as another session or a bulk script would. They skip every Application rule, so
/// they are how tests create the situations the pre-checks cannot see.
/// </summary>
internal static class RawRows
{
    public static async Task<int> CompanyAsync(string code, string name) =>
        await Sql.ScalarAsync<int>(
            """
            INSERT INTO org.Company (Code, Name, LegalName, Industry, Email, Phone, City, Country, FoundedDate)
            OUTPUT INSERTED.Id
            VALUES (@code, @name, @name, N'Software', N'raw@example.test', N'+95 1 555 0199', N'Yangon', N'Myanmar', '2015-03-01');
            """,
            ("@code", code),
            ("@name", name));

    /// <summary>An employee written with plain SQL (fictional data). The work email must start with the tag of the test so the cleanup finds it.</summary>
    public static async Task<int> EmployeeAsync(
        int companyId,
        int branchId,
        int departmentId,
        int positionId,
        int jobGradeId,
        string code,
        string workEmail) =>
        await Sql.ScalarAsync<int>(
            """
            INSERT INTO hr.Employee
                (EmployeeCode, FirstName, LastName, Gender, DateOfBirth, MaritalStatus, Nationality, NationalId, WorkEmail, Phone,
                 AddressLine1, AddressCity, AddressCountry, EmergencyContactName, EmergencyContactRelationship, EmergencyContactPhone,
                 CompanyId, BranchId, DepartmentId, PositionId, JobGradeId, EmploymentType, EmploymentStatus, JoiningDate)
            OUTPUT INSERTED.Id
            VALUES
                (@code, N'Raw', N'Testwell', 'Male', '1990-01-01', 'Single', N'Myanmar', N'NRC-RAW-000001', @email, N'+95 9 555 0111',
                 N'1 Raw Street', N'Yangon', N'Myanmar', N'Raw Contact', 'Friend', N'+95 9 555 0112',
                 @companyId, @branchId, @departmentId, @positionId, @jobGradeId, 'Full-time', 'Active', '2024-01-01');
            """,
            ("@code", code),
            ("@email", workEmail),
            ("@companyId", companyId),
            ("@branchId", branchId),
            ("@departmentId", departmentId),
            ("@positionId", positionId),
            ("@jobGradeId", jobGradeId));

    public static async Task<int> BranchAsync(int companyId, string code, string name) =>
        await Sql.ScalarAsync<int>(
            """
            INSERT INTO org.Branch (CompanyId, Code, Name, Type, City, Country, OpenedDate)
            OUTPUT INSERTED.Id
            VALUES (@companyId, @code, @name, 'Branch', N'Yangon', N'Myanmar', '2020-01-01');
            """,
            ("@companyId", companyId),
            ("@code", code),
            ("@name", name));
}
