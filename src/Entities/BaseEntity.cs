namespace Entities;

/// <summary>
/// Base entity with common properties for all entities.
/// Includes audit fields and soft delete support.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    // Audit fields - creation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    // Audit fields - modification
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

