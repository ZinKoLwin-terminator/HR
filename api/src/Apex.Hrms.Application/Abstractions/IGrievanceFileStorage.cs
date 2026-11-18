namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Permanent storage of grievance files: the same contract and key rules as <see cref="IEvidenceStorage"/> (server-generated
/// keys of 32 lower-case hexadecimal characters from <see cref="IEvidenceStorage.NewKey"/>, never a client file name), but its own
/// folder (<c>Grievance:FileFolder</c>), so grievance files never sit with KPI evidence and the folder can be restricted and
/// backed up on its own terms. Content and names are never logged.
/// </summary>
public interface IGrievanceFileStorage : IEvidenceStorage;
