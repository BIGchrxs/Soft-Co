using System.ComponentModel.DataAnnotations;

namespace SoftCo.Models;

/// <summary>
/// Append-only record of material changes. The brief requires user, time, previous value, new
/// value and reason for every change to a tracked record; nothing in this table is ever updated
/// or deleted.
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }

    [Required, StringLength(100)] public string EntityName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string EntityId { get; set; } = string.Empty;

    /// <summary>Created | Updated | Deleted | PaymentRecorded | Imported</summary>
    [Required, StringLength(50)] public string Action { get; set; } = string.Empty;

    [StringLength(100)] public string? FieldName { get; set; }
    [StringLength(2000)] public string? OldValue { get; set; }
    [StringLength(2000)] public string? NewValue { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }

    [StringLength(450)] public string? UserId { get; set; }
    [StringLength(256)] public string? UserName { get; set; }
    [StringLength(64)] public string? IpAddress { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
