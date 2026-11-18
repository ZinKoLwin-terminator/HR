namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// The keyed pseudonym of an anonymous grievance submitter: HMACSHA256(Grievance:PseudonymKey, employee id), 32 bytes, stored in
/// grv.CaseSubmitter.SubmitterPseudonym instead of the employee id. The same employee always gets the same value, so "my
/// grievances" can match it, and without the key nobody can tell from the database whose case it is. The key lives only in the
/// API's configuration (never in the database or source). Changing the key cuts every anonymous case off from its submitter.
/// </summary>
public interface IGrievancePseudonyms
{
    public const int Length = 32;

    /// <summary>A new 32-byte array with the pseudonym of the employee.</summary>
    byte[] Compute(int employeeId);
}
