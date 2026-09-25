using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftCo.Data;
using SoftCo.Models;

namespace SoftCo.Controllers;

[Authorize]
public class ProjectsController : Controller
{
    private readonly AppDbContext _db;

    public ProjectsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var projects = await _db.Projects.AsNoTracking().OrderBy(p => p.Name).ToListAsync();

        // Exposure per project, in Rand. Foreign columns mix CNY and USD and cannot be summed.
        var orders = await _db.SupplierOrders.AsNoTracking()
            .Include(o => o.OrderProjects)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .ToListAsync();

        ViewBag.Exposure = projects.ToDictionary(
            p => p.Id,
            p => orders.Where(o => o.OrderProjects.Any(op => op.ProjectId == p.Id))
                       .Sum(o => o.OutstandingZar));

        ViewBag.Counts = projects.ToDictionary(
            p => p.Id,
            p => orders.Count(o => o.OrderProjects.Any(op => op.ProjectId == p.Id)));

        return View(projects);
    }

    [Authorize(Roles = Roles.CanEditOrders)]
    public IActionResult Create() => View("Edit", new Project());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.CanEditOrders)]
    public async Task<IActionResult> Create(Project model)
    {
        if (!ModelState.IsValid) return View("Edit", model);

        _db.Projects.Add(model);
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Project added.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.CanEditOrders)]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Projects.FindAsync(id);
        return p is null ? NotFound() : View(p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.CanEditOrders)]
    public async Task<IActionResult> Edit(Project model)
    {
        if (!ModelState.IsValid) return View(model);

        var p = await _db.Projects.FindAsync(model.Id);
        if (p is null) return NotFound();

        p.Code = model.Code;
        p.Name = model.Name;
        p.IsActive = model.IsActive;

        await _db.SaveChangesAsync();

        TempData["Flash"] = "Project updated.";
        return RedirectToAction(nameof(Index));
    }
}
