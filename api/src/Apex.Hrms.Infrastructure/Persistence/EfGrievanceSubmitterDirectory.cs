using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Grievances;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// grv.CaseSubmitter through the request's context, so its queries compose with the read-model queries into one SQL statement.
/// "My cases" is two seeks (IX_CaseSubmitter_EmployeeId and IX_CaseSubmitter_SubmitterPseudonym, both filtered), run as two separate
/// statements so that no statement carries both the employee id and the pseudonym. The pseudonym is a query parameter only: it is never
/// logged or returned.
/// </summary>
internal sealed class EfGrievanceSubmitterDirectory(HrmsDbContext context, IGrievancePseudonyms pseudonyms) : IGrievanceSubmitterDirectory
{
    private DbSet<GrievanceCaseSubmitter> Set => context.Set<GrievanceCaseSubmitter>();

    public IQueryable<GrievanceCaseSubmitter> Query() => Set.AsNoTracking();

    public async Task<IReadOnlyList<int>> CaseIdsOfAsync(int employeeId, CancellationToken cancellationToken)
    {
        // Two statements on purpose: the first carries only the employee id, the second only the pseudonym (see the port).
        var named = await Set.AsNoTracking().Where(submitter => submitter.EmployeeId == employeeId).Select(submitter => submitter.CaseId).ToListAsync(cancellationToken);
        var pseudonym = pseudonyms.Compute(employeeId);
        var anonymous = await Set.AsNoTracking().Where(submitter => submitter.SubmitterPseudonym == pseudonym).Select(submitter => submitter.CaseId).ToListAsync(cancellationToken);
        return [.. named.Concat(anonymous).Distinct().Order()];
    }

    public byte[] PseudonymOf(int employeeId) => pseudonyms.Compute(employeeId);

    public Task<GrievanceCaseSubmitter?> FindForChangeAsync(int caseId, CancellationToken cancellationToken) =>
        Set.SingleOrDefaultAsync(submitter => submitter.CaseId == caseId, cancellationToken);

    public void Add(GrievanceCaseSubmitter submitter)
    {
        ArgumentNullException.ThrowIfNull(submitter);
        if (submitter.CaseId <= 0)
        {
            throw new ArgumentException("Save the case before its submitter: the submitter row is keyed by the case id.", nameof(submitter));
        }

        Set.Add(submitter);
    }
}
