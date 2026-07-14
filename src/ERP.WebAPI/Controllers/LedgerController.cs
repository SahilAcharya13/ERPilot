using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Domain.Interfaces;

namespace ERP.WebAPI.Controllers;

[Authorize(Roles = "Admin,Manager,Accounts")]
[ApiController]
[Route("api/[controller]")]
public class LedgerController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public LedgerController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public class LedgerEntry
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = null!;
        public string Reference { get; set; } = null!;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetLedger(int customerId)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
        if (customer == null)
        {
            return NotFound(new { error = "Customer not found." });
        }

        var orders = await _unitOfWork.Orders.FindAsync(o => o.CustomerID == customerId);
        var payments = await _unitOfWork.Payments.FindAsync(p => p.CustomerID == customerId);

        var entries = new List<LedgerEntry>();

        foreach (var order in orders)
        {
            entries.Add(new LedgerEntry
            {
                Date = order.OrderDate,
                Type = "Order",
                Reference = order.OrderNumber,
                Debit = order.TotalAmount,
                Credit = 0
            });
        }

        foreach (var payment in payments)
        {
            entries.Add(new LedgerEntry
            {
                Date = payment.PaymentDate,
                Type = "Payment",
                Reference = payment.ReferenceNumber ?? $"PAY-{payment.PaymentID}",
                Debit = 0,
                Credit = payment.Amount
            });
        }

        // Sort chronologically
        var sortedEntries = entries.OrderBy(e => e.Date).ToList();

        // Calculate running balance
        decimal runningBalance = 0;
        foreach (var entry in sortedEntries)
        {
            runningBalance += (entry.Debit - entry.Credit);
            entry.RunningBalance = runningBalance;
        }

        return Ok(new
        {
            customerId = customer.CustomerID,
            companyName = customer.CompanyName,
            runningBalance = runningBalance,
            entries = sortedEntries
        });
    }
}
