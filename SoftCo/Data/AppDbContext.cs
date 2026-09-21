using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SoftCo.Models;

namespace SoftCo.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SupplierOrder> SupplierOrders => Set<SupplierOrder>();
    public DbSet<OrderProject> OrderProjects => Set<OrderProject>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // --- Supplier ------------------------------------------------------------------------
        b.Entity<Supplier>(e =>
        {
            // Suppliers arrive from the tracker under long, inconsistent names
            // ("EMORY _ Foshan Shunde Xintimeinuo"), so the unique index is the guard against the
            // same manufacturer being entered twice under slightly different spellings.
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Type);
        });

        // --- Project -------------------------------------------------------------------------
        b.Entity<Project>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        // --- SupplierOrder -------------------------------------------------------------------
        b.Entity<SupplierOrder>(e =>
        {
            e.HasOne(x => x.Supplier)
             .WithMany(s => s.Orders)
             .HasForeignKey(x => x.SupplierId)
             // Deleting a supplier must never silently take its financial history with it.
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.FulfilmentStatus);
            e.HasIndex(x => x.InvoiceRef);
            e.HasIndex(x => x.InvoiceDate);
        });

        // --- OrderProject (join) -------------------------------------------------------------
        b.Entity<OrderProject>(e =>
        {
            e.HasKey(x => new { x.SupplierOrderId, x.ProjectId });

            e.HasOne(x => x.SupplierOrder)
             .WithMany(o => o.OrderProjects)
             .HasForeignKey(x => x.SupplierOrderId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Project)
             .WithMany(p => p.OrderProjects)
             .HasForeignKey(x => x.ProjectId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // --- OrderPayment --------------------------------------------------------------------
        b.Entity<OrderPayment>(e =>
        {
            e.HasOne(x => x.SupplierOrder)
             .WithMany(o => o.Payments)
             .HasForeignKey(x => x.SupplierOrderId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.PaidDate);
        });

        // --- AuditEvent ----------------------------------------------------------------------
        b.Entity<AuditEvent>(e =>
        {
            e.HasIndex(x => new { x.EntityName, x.EntityId });
            e.HasIndex(x => x.OccurredAt);
        });
    }
}
