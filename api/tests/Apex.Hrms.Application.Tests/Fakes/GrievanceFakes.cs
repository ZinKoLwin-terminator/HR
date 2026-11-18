using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Notifications;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>The test pseudonym: HMACSHA256 with a fixed test-only key, as the real one but never a configured secret.</summary>
internal sealed class FakeGrievancePseudonyms : IGrievancePseudonyms
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("TEST-ONLY-grievance-pseudonym-key");

    public byte[] Compute(int employeeId) => HMACSHA256.HashData(Key, BitConverter.GetBytes(employeeId));
}

/// <summary>grv.CaseSubmitter in memory. Changes are visible at once (like a tracked row saved by the next unit of work).</summary>
internal sealed class FakeGrievanceSubmitterDirectory(IGrievancePseudonyms pseudonyms) : IGrievanceSubmitterDirectory
{
    public List<GrievanceCaseSubmitter> Rows { get; } = [];

    public IQueryable<GrievanceCaseSubmitter> Query() => Rows.AsQueryable();

    public Task<IReadOnlyList<int>> CaseIdsOfAsync(int employeeId, CancellationToken cancellationToken)
    {
        var pseudonym = pseudonyms.Compute(employeeId);
        return Task.FromResult<IReadOnlyList<int>>([.. Rows
            .Where(row => row.EmployeeId == employeeId || (row.SubmitterPseudonym is { } stored && stored.AsSpan().SequenceEqual(pseudonym)))
            .Select(row => row.CaseId)]);
    }

    public byte[] PseudonymOf(int employeeId) => pseudonyms.Compute(employeeId);

    public Task<GrievanceCaseSubmitter?> FindForChangeAsync(int caseId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.FirstOrDefault(row => row.CaseId == caseId));

    public void Add(GrievanceCaseSubmitter submitter)
    {
        Assert.True(submitter.CaseId > 0, "A submitter row was added before its case had an id.");
        Rows.Add(submitter);
    }
}

/// <summary>grv.AuditLog in memory: entries are staged and kept only when the fake unit of work saves.</summary>
internal sealed class FakeGrievanceAuditLog : IGrievanceAuditLog, IVersionedStore
{
    private static readonly PropertyInfo IdProperty = typeof(GrievanceAuditEntry).GetProperty(nameof(GrievanceAuditEntry.Id))!;
    private readonly List<GrievanceAuditEntry> staged = [];
    private long lastId;

    public FakeGrievanceAuditLog(FakeDatabase database) => database.Register(this);

    public List<GrievanceAuditEntry> Entries { get; } = [];

    public void Add(GrievanceAuditEntry entry) => staged.Add(entry);

    public IQueryable<GrievanceAuditEntry> Query() => Entries.ToList().AsQueryable();

    void IVersionedStore.StampChanged()
    {
        foreach (var entry in staged)
        {
            IdProperty.SetValue(entry, ++lastId);
            Entries.Add(entry);
        }

        staged.Clear();
    }
}

/// <summary>grv.CaseNoSeq: counts up from 1.</summary>
internal sealed class FakeGrievanceCaseNumbers : IGrievanceCaseNumberSequence
{
    private int last;

    public Task<int> NextAsync(CancellationToken cancellationToken) => Task.FromResult(++last);
}

/// <summary>Grievance file storage in memory. Refuses a key that is not 32 lower-case hex characters, like the real one.</summary>
internal sealed class FakeGrievanceFileStorage : IGrievanceFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public List<string> Deleted { get; } = [];

    public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
    {
        Assert.True(GrievanceAttachment.IsValidStorageKey(key));
        Assert.True(Files.TryAdd(key, content), "A file was stored twice under one key.");
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        Assert.True(GrievanceAttachment.IsValidStorageKey(key));
        return Task.FromResult<Stream?>(Files.TryGetValue(key, out var content) ? new MemoryStream(content, writable: false) : null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Assert.True(GrievanceAttachment.IsValidStorageKey(key));
        Deleted.Add(key);
        Files.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>Who holds which grant (employee ids), as sec.RolePermission and the reachable accounts would answer.</summary>
internal sealed class FakePermissionHolders : IPermissionHolders
{
    public Dictionary<string, HashSet<int>> Grants { get; } = [];

    public void Grant(string grant, params int[] employeeIds)
    {
        if (!Grants.TryGetValue(grant, out var set))
        {
            Grants[grant] = set = [];
        }

        set.UnionWith(employeeIds);
    }

    public IQueryable<int> EmployeesHolding(string moduleKey, string action) =>
        (Grants.TryGetValue($"{moduleKey}:{action}", out var set) ? set.ToList() : []).AsQueryable();
}

/// <summary>One account: its user id, its employee (null for the built-in administrator) and its grants.</summary>
internal sealed record FakeAccount(int UserId, int? EmployeeId, IReadOnlySet<string> Grants);

/// <summary>The notification recipients from a list of accounts.</summary>
internal sealed class FakeNotificationRecipients : INotificationRecipients
{
    public List<FakeAccount> Accounts { get; } = [];

    public Task<int?> UserIdOfEmployeeAsync(int employeeId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(account => account.EmployeeId == employeeId)?.UserId);

    public Task<IReadOnlyList<int>> UserIdsHoldingAsync(string moduleKey, string action, IReadOnlyCollection<int> excludedEmployeeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<int>>([.. Accounts
            .Where(account => account.Grants.Contains($"{moduleKey}:{action}"))
            .Where(account => account.EmployeeId is null || !excludedEmployeeIds.Contains(account.EmployeeId.Value))
            .Select(account => account.UserId)]);
}

/// <summary>The notifications written (kept only when the fake unit of work saves).</summary>
internal sealed class FakeNotificationWriter : INotificationWriter, IVersionedStore
{
    private readonly List<(int UserId, NotificationContent Content)> staged = [];

    public FakeNotificationWriter(FakeDatabase database) => database.Register(this);

    public List<(int UserId, NotificationContent Content)> Sent { get; } = [];

    public Result Add(IEnumerable<int> recipientUserIds, NotificationContent content)
    {
        foreach (var user in recipientUserIds.Distinct())
        {
            var check = Notification.Create(user, content.Title, content.Message, content.Icon, content.Link, DateTime.UtcNow);
            if (check.IsFailure)
            {
                return check;
            }

            staged.Add((user, content));
        }

        return Result.Success();
    }

    void IVersionedStore.StampChanged()
    {
        Sent.AddRange(staged);
        staged.Clear();
    }
}
