namespace Domain;

/// <summary>
/// Metadata for audit fields and soft delete support.
/// Stored as a sub-document in MongoDB.
/// </summary>
public class EntityMetadata
{
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

/// <summary>
/// Base entity with common properties for all entities.
/// Metadata fields are stored in a nested Metadata object.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Audit and soft-delete metadata stored as sub-document.
    /// </summary>
    public EntityMetadata Metadata { get; set; } = new();
}

