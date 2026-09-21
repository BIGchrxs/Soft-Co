namespace SoftCo.Models;

/// <summary>
/// Join between an order and the project(s) it serves. Many-to-many from day one because the
/// existing tracker already has single lines spanning several projects - "Williams / Ravello 301",
/// "Arcadia / Williams", "Inospace / Inhouse Office Chairs" - and splitting that out later, once
/// live data exists, is far more painful than carrying the join table now.
/// </summary>
public class OrderProject
{
    public int SupplierOrderId { get; set; }
    public SupplierOrder? SupplierOrder { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }
}
