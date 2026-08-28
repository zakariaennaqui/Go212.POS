namespace Go212.POS.Domain.Entities;

/// <summary>
/// Base class for all entities with audit timestamps.
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
