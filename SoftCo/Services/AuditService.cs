using System.Security.Claims;
using SoftCo.Data;
using SoftCo.Models;

namespace SoftCo.Services;

/// <summary>
/// Writes the append-only change history the brief requires: user, time, previous value,
/// new value. Nothing here ever updates or deletes an existing event.
/// </summary>
public interface IAuditService
{
    void Record(string entityName, string entityId, string action,
                string? field = null, string? oldValue = null, string? newValue = null,
                string? reason = null);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public void Record(string entityName, string entityId, string action,
                       string? field = null, string? oldValue = null, string? newValue = null,
                       string? reason = null)
    {
        var user = _http.HttpContext?.User;

        _db.AuditEvents.Add(new AuditEvent
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            FieldName = field,
            OldValue = Trim(oldValue),
            NewValue = Trim(newValue),
            Reason = reason,
            UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = user?.Identity?.Name,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            OccurredAt = DateTime.UtcNow
        });
    }

    private static string? Trim(string? v) =>
        v is { Length: > 2000 } ? v[..2000] : v;
}
