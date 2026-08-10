namespace His.Hope.Infrastructure.DataLifecycle;

/// <summary>
/// Marks an entity as support soft-delete.
/// When an entity implementing this interface is deleted,
/// the SoftDeleteInterceptor sets DeletedAt and DeletedBy
/// instead of physically removing the row.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// UTC timestamp of when the record was soft-deleted.
    /// Null indicates the record is active.
    /// </summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>
    /// ID of the user who soft-deleted the record.
    /// Null if deleted by system or not yet deleted.
    /// </summary>
    Guid? DeletedBy { get; set; }
}
