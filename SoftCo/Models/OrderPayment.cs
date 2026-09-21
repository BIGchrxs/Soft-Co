using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoftCo.Models;

/// <summary>
/// A single deposit or settlement paid against an order. Modelled as rows rather than two fixed
/// columns because the tracker already contains a line where the deposit and a partial settlement
/// together come to less than the invoice - fixed columns cannot represent that honestly.
/// </summary>
public class OrderPayment
{
    public int Id { get; set; }

    public int SupplierOrderId { get; set; }
    public SupplierOrder? SupplierOrder { get; set; }

    public PaymentKind Kind { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal AmountForeign { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal AmountZar { get; set; }

    public DateOnly? PaidDate { get; set; }

    [StringLength(200)]
    public string? Reference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [StringLength(450)] public string? CreatedById { get; set; }
}
