namespace Apex.Hrms.Domain.Common;

/// <summary>A persisted record with a database-generated integer identity.</summary>
public abstract class Entity
{
    public int Id { get; private set; }
}
