using System;
using System.Collections.Generic;
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

public class Order : SoftDeleteEntity
{
    public int OrderID { get; set; }
    public string OrderNumber { get; set; } = null!;
    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;
    
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveryDate { get; set; }
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
    
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    
    // Backing field or property for computed DB column
    public decimal PendingAmount { get; private set; }
    
    public string? Remarks { get; set; }
    
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
