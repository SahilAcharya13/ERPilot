using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class Product : SoftDeleteEntity
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public decimal Rate { get; set; }
    public decimal GST { get; set; } = 0.00m;
    public string Status { get; set; } = "Active"; // Active, Inactive
}
