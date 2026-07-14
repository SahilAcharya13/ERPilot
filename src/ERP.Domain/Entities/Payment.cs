using System;
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

public class Payment : SoftDeleteEntity
{
    public int PaymentID { get; set; }
    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;
    
    public int? OrderID { get; set; }
    public Order? Order { get; set; }
    
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
}
