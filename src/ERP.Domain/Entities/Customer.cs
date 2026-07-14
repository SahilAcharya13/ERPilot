using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class Customer : SoftDeleteEntity
{
    public int CustomerID { get; set; }
    public string CustomerCode { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
