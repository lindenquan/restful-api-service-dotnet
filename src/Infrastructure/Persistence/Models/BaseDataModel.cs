using MongoDB.Bson.Serialization.Attributes;

namespace Infrastructure.Persistence.Models;

/// <summary>
/// Metadata for audit fields and soft delete support.
/// This is an infrastructure concern, not part of the domain.
/// </summary>
public class DataModelMetadata
{
    // Audit fields - creation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }

    // Audit fields - modification
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>
/// Base data model for MongoDB persistence.
/// Uses UUID v7 for Id - see BaseEntity for rationale.
///
/// MongoDB Configuration:
/// - [BsonId] marks this as the document's _id field
/// - [BsonGuidRepresentation(Standard)] stores as standard UUID binary (subtype 4)
/// - MongoDB's default ObjectId generation is disabled
/// </summary>
public abstract class BaseDataModel
{
    /// <summary>
    /// Primary identifier using UUID v7.
    /// Stored as MongoDB's _id field with standard GUID representation.
    /// </summary>
    [BsonId]
    [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>
    /// Audit and soft-delete metadata - infrastructure concern.
    /// </summary>
    public DataModelMetadata Metadata { get; set; } = new();
}

