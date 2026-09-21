using System.ComponentModel.DataAnnotations;

namespace SoftCo.Models;

/// <summary>
/// A client engagement goods are bought against — Fairmont, Ravello 202 &amp; 401, Williams,
/// De Wet, Arcadia, Estate Penthouse, T.M Mauritius, Harbour House V&amp;A in the current tracker.
/// Kept deliberately thin for this first slice: budgets, milestones and client records come later
/// and hang off this same row.
/// </summary>
public class Project
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderProject> OrderProjects { get; set; } = new List<OrderProject>();
}
