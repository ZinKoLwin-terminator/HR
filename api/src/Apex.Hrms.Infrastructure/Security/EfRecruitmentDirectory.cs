using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Recruitment;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Security;

/// <summary>The openings an employee is hiring manager of, with the applications of those openings and their candidates.</summary>
internal sealed class EfRecruitmentDirectory(HrmsDbContext context) : IRecruitmentDirectory
{
    public async Task<ManagedRecruitment> ManagedByAsync(int hiringManagerEmployeeId, CancellationToken cancellationToken)
    {
        var openingIds = await context.Set<JobOpening>()
            .AsNoTracking()
            .Where(opening => opening.HiringManagerEmployeeId == hiringManagerEmployeeId)
            .Select(opening => opening.Id)
            .ToListAsync(cancellationToken);
        if (openingIds.Count == 0)
        {
            return new ManagedRecruitment([], [], []);
        }

        var applications = await context.Set<JobApplication>()
            .AsNoTracking()
            .Where(application => openingIds.Contains(application.JobOpeningId))
            .Select(application => new { application.Id, application.CandidateId })
            .ToListAsync(cancellationToken);
        return new ManagedRecruitment(
            openingIds,
            [.. applications.Select(application => application.Id)],
            [.. applications.Select(application => application.CandidateId).Distinct()]);
    }
}
