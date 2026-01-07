namespace Domain;

/// <summary>
/// Base entity with identity and audit properties.
/// Audit properties are populated by the infrastructure layer.
///
/// ID Strategy: UUID v7 (RFC 9562)
/// - Time-ordered for efficient database indexing
/// - Globally unique without central coordination
/// - Secure (unpredictable, prevents enumeration attacks)
/// - Database-agnostic (portable across MongoDB, PostgreSQL, SQL Server, etc.)
///
/// Why UUID v7 over alternatives:
/// - vs int: Secure, no central sequence needed, works in distributed systems
/// - vs MongoDB ObjectId: Standard RFC, portable, not vendor-locked
/// - vs UUID v4: Time-ordered for better index performance
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Primary identifier using UUID v7 (time-ordered, globally unique).
    /// Generated using Guid.CreateVersion7() in .NET 9+.
    /// </summary>
    public Guid Id { get; set; }

    // Audit properties - populated by infrastructure, read by application
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

