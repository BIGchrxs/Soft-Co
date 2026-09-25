using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SoftCo.Data;
using SoftCo.Models;
using SoftCo.Services;
using SoftCo.ViewModels;

namespace SoftCo.Controllers;

/// <summary>
/// The International Payment Tracker. This is the screen that replaces the spreadsheet:
/// what was ordered from whom, for which projects, where it is, and what has been paid.
/// </summary>
[Authorize]
public class OrdersController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public OrdersController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // --- Grid ----------------------------------------------------------------------------

    public async Task<IActionResult> Index(OrderFilterViewModel filter)
    {
        var q = _db.SupplierOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.OrderProjects).ThenInclude(op => op.Project)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .AsQueryable();

        if (filter.SupplierId is int sid)
            q = q.Where(o => o.SupplierId == sid);

        if (filter.ProjectId is int pid)
            q = q.Where(o => o.OrderProjects.Any(op => op.ProjectId == pid));

        if (filter.Fulfilment is FulfilmentStatus fs)
            q = q.Where(o => o.FulfilmentStatus == fs);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            q = q.Where(o =>
                EF.Functions.Like(o.ProductDescription, $"%{term}%") ||
                (o.InvoiceRef != null && EF.Functions.Like(o.InvoiceRef, $"%{term}%")) ||
                EF.Functions.Like(o.Supplier!.Name, $"%{term}%"));
        }

        var orders = await q
            .OrderByDescending(o => o.InvoiceDate ?? DateOnly.MinValue)
            .ThenBy(o => o.Supplier!.Name)
            .ToListAsync();

        var rows = orders.Select(ToRow).ToList();

        // Settlement status is computed from payments, so it cannot be filtered in SQL.
        if (filter.Settlement is SettlementStatus ss)
            rows = rows.Where(r => r.Settlement == ss).ToList();

        var vm = new OrderListViewModel
        {
            Filter = filter,
            Rows = rows,
            Suppliers = await SupplierOptions(),
            Projects = await ProjectOptions()
        };

        return View(vm);
    }

    /// <summary>Everything not fully settled, grouped by supplier — the morning view.</summary>
    public async Task<IActionResult> Outstanding()
    {
        var orders = await _db.SupplierOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.OrderProjects).ThenInclude(op => op.Project)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .ToListAsync();

        var rows = orders
            .Select(ToRow)
            .Where(r => r.Settlement != SettlementStatus.Paid)
            .OrderByDescending(r => r.OutstandingZar)
            .ToList();

        var vm = new OrderListViewModel { Rows = rows };
        return View(vm);
    }

    // --- Detail --------------------------------------------------------------------------

    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.SupplierOrders
            .Include(o => o.Supplier)
            .Include(o => o.OrderProjects).ThenInclude(op => op.Project)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        ViewBag.History = await _db.AuditEvents.AsNoTracking()
            .Where(a => a.EntityName == nameof(SupplierOrder) && a.EntityId == id.ToString())
            .OrderByDescending(a => a.OccurredAt)
            .Take(25)
            .ToListAsync();

        return View(order);
    }

    // --- Create / Edit -------------------------------------------------------------------

    [Authorize(Roles = Roles.CanEditOrders)]
    public async Task<IActionResult> Create()
    {
        var vm = new OrderEditViewModel
        {
            AllSuppliers = await SupplierOptions(),
            AllProjects = await ProjectOptions()
        };
        return View("Edit", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.CanEditOrders)]
    public async Task<IActionResult> Create(OrderEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.AllSuppliers = await SupplierOptions();
            vm.AllProjects = await ProjectOptions();
            return View("Edit", vm);
        }

        var order = new SupplierOrder
        {
            SupplierId = vm.SupplierId,
            ProductDescription = vm.ProductDescription,
            FulfilmentStatus = vm.FulfilmentStatus,
            CargoReadinessDate = vm.CargoReadinessDate,
            InvoiceRef = vm.InvoiceRef,
            InvoiceDate = vm.InvoiceDate,
            CurrencyCode = vm.CurrencyCode.ToUpperInvariant(),
            ExchangeRate = vm.ExchangeRate,
            InvoiceValueForeign = vm.InvoiceValueForeign,
            InvoiceValueZar = vm.InvoiceValueZar,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedById = User.Identity?.Name
        };

        foreach (var pid in vm.ProjectIds.Distinct())
            order.OrderProjects.Add(new OrderProject { ProjectId = pid });

        _db.SupplierOrders.Add(order);
        await _db.SaveChangesAsync();

        _audit.Record(nameof(SupplierOrder), order.Id.ToString(), "Created",
                      newValue: $"{order.ProductDescription} / {order.InvoiceRef}");
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Order created.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [Authorize(Roles = Roles.CanEditOrders)]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await _db.SupplierOrders
            .Include(o => o.OrderProjects)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        var vm = new OrderEditViewModel
        {
            Id = order.Id,
            SupplierId = order.SupplierId,
            ProductDescription = order.ProductDescription,
            ProjectIds = order.OrderProjects.Select(op => op.ProjectId).ToList(),
            FulfilmentStatus = order.FulfilmentStatus,
            CargoReadinessDate = order.CargoReadinessDate,
            InvoiceRef = order.InvoiceRef,
            InvoiceDate = order.InvoiceDate,
            CurrencyCode = order.CurrencyCode,
            ExchangeRate = order.ExchangeRate,
            InvoiceValueForeign = order.InvoiceValueForeign,
            InvoiceValueZar = order.InvoiceValueZar,
            Notes = order.Notes,
            AllSuppliers = await SupplierOptions(),
            AllProjects = await ProjectOptions()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.CanEditOrders)]
    public async Task<IActionResult> Edit(OrderEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.AllSuppliers = await SupplierOptions();
            vm.AllProjects = await ProjectOptions();
            return View(vm);
        }

        var order = await _db.SupplierOrders
            .Include(o => o.OrderProjects)
            .FirstOrDefaultAsync(o => o.Id == vm.Id);

        if (order is null) return NotFound();

        // Field-level audit: the brief requires previous and new value, not just "changed".
        TrackChange(order.Id, "FulfilmentStatus", order.FulfilmentStatus.ToString(), vm.FulfilmentStatus.ToString());
        TrackChange(order.Id, "InvoiceValueForeign", order.InvoiceValueForeign.ToString(), vm.InvoiceValueForeign.ToString());
        TrackChange(order.Id, "InvoiceValueZar", order.InvoiceValueZar.ToString(), vm.InvoiceValueZar.ToString());
        TrackChange(order.Id, "ExchangeRate", order.ExchangeRate.ToString(), vm.ExchangeRate.ToString());
        TrackChange(order.Id, "InvoiceRef", order.InvoiceRef, vm.InvoiceRef);

        order.SupplierId = vm.SupplierId;
        order.ProductDescription = vm.ProductDescription;
        order.FulfilmentStatus = vm.FulfilmentStatus;
        order.CargoReadinessDate = vm.CargoReadinessDate;
        order.InvoiceRef = vm.InvoiceRef;
        order.InvoiceDate = vm.InvoiceDate;
        order.CurrencyCode = vm.CurrencyCode.ToUpperInvariant();
        order.ExchangeRate = vm.ExchangeRate;
        order.InvoiceValueForeign = vm.InvoiceValueForeign;
        order.InvoiceValueZar = vm.InvoiceValueZar;
        order.Notes = vm.Notes;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedById = User.Identity?.Name;

        var wanted = vm.ProjectIds.Distinct().ToHashSet();
        foreach (var gone in order.OrderProjects.Where(op => !wanted.Contains(op.ProjectId)).ToList())
            order.OrderProjects.Remove(gone);
        foreach (var pid in wanted.Where(p => order.OrderProjects.All(op => op.ProjectId != p)))
            order.OrderProjects.Add(new OrderProject { ProjectId = pid });

        await _db.SaveChangesAsync();

        TempData["Flash"] = "Order updated.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    // --- Payments ------------------------------------------------------------------------

    [Authorize(Roles = Roles.CanRecordPayments)]
    public async Task<IActionResult> RecordPayment(int id)
    {
        var order = await _db.SupplierOrders
            .Include(o => o.Supplier)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        var vm = new RecordPaymentViewModel
        {
            SupplierOrderId = order.Id,
            OrderSummary = $"{order.Supplier?.Name} — {order.ProductDescription}",
            CurrencyCode = order.CurrencyCode,
            ExchangeRate = order.ExchangeRate,
            OutstandingForeign = order.OutstandingForeign,
            Kind = order.Payments.Any(p => p.Kind == PaymentKind.Deposit)
                   ? PaymentKind.Settlement : PaymentKind.Deposit,
            AmountForeign = order.OutstandingForeign > 0 ? order.OutstandingForeign : 0
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.CanRecordPayments)]
    public async Task<IActionResult> RecordPayment(RecordPaymentViewModel vm)
    {
        var order = await _db.SupplierOrders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == vm.SupplierOrderId);

        if (order is null) return NotFound();

        if (!ModelState.IsValid)
        {
            vm.OutstandingForeign = order.OutstandingForeign;
            return View(vm);
        }

        _db.OrderPayments.Add(new OrderPayment
        {
            SupplierOrderId = order.Id,
            Kind = vm.Kind,
            AmountForeign = vm.AmountForeign,
            AmountZar = vm.AmountZar > 0 ? vm.AmountZar : Math.Round(vm.AmountForeign * order.ExchangeRate, 2),
            PaidDate = vm.PaidDate,
            Reference = vm.Reference,
            CreatedById = User.Identity?.Name
        });

        _audit.Record(nameof(SupplierOrder), order.Id.ToString(), "PaymentRecorded",
                      field: vm.Kind.ToString(),
                      newValue: $"{vm.AmountForeign} {order.CurrencyCode}");

        await _db.SaveChangesAsync();

        TempData["Flash"] = $"{vm.Kind} recorded.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    // --- Helpers -------------------------------------------------------------------------

    private void TrackChange(int orderId, string field, string? oldV, string? newV)
    {
        if (string.Equals(oldV, newV, StringComparison.Ordinal)) return;
        _audit.Record(nameof(SupplierOrder), orderId.ToString(), "Updated", field, oldV, newV);
    }

    private static OrderRowViewModel ToRow(SupplierOrder o) => new()
    {
        Id = o.Id,
        Supplier = o.Supplier?.Name ?? "",
        Product = o.ProductDescription,
        Projects = string.Join(", ", o.OrderProjects.Select(op => op.Project?.Name).Where(n => n != null)),
        Fulfilment = o.FulfilmentStatus,
        CargoReadiness = o.CargoReadinessDate,
        InvoiceDate = o.InvoiceDate,
        InvoiceRef = o.InvoiceRef,
        CurrencyCode = o.CurrencyCode,
        InvoiceValueForeign = o.InvoiceValueForeign,
        InvoiceValueZar = o.InvoiceValueZar,
        DepositForeign = o.Payments.Where(p => p.Kind == PaymentKind.Deposit).Sum(p => p.AmountForeign),
        SettlementForeign = o.Payments.Where(p => p.Kind == PaymentKind.Settlement).Sum(p => p.AmountForeign),
        OutstandingZar = o.OutstandingZar,
        Settlement = o.SettlementStatus
    };

    private async Task<List<(int, string)>> SupplierOptions() =>
        (await _db.Suppliers.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync())
        .Select(s => (s.Id, s.Name)).ToList();

    private async Task<List<(int, string)>> ProjectOptions() =>
        (await _db.Projects.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync())
        .Select(p => (p.Id, p.Name)).ToList();
}
