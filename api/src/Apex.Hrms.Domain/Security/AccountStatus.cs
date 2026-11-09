namespace Apex.Hrms.Domain.Security;

/// <summary>Status of an application account (sec.AppUser). Only an Active account can sign in.</summary>
public enum AccountStatus
{
    Active,
    Inactive,
    Locked,
}
