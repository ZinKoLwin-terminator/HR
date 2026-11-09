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
