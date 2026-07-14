namespace ERP.Domain.Entities;

public class OrderItem
{
    public int OrderItemID { get; set; }
    public int OrderID { get; set; }
    public Order Order { get; set; } = null!;
    
    public int ProductID { get; set; }
    public Product Product { get; set; } = null!;
    
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = null!;
    public decimal Rate { get; set; }
    
    // Backing field or property for computed DB column
    public decimal Amount { get; private set; }
}
