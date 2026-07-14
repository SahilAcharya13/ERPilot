using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Domain.Interfaces;

namespace ERP.WebAPI.Controllers;

[Authorize(Roles = "Admin,Manager,Sales")]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public ReportsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("sales-summary")]
    public async Task<IActionResult> GetSalesSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var orders = await _unitOfWork.Orders.GetAllAsync();

        // Apply date filters if provided
        if (startDate.HasValue)
        {
            orders = orders.Where(o => o.OrderDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            orders = orders.Where(o => o.OrderDate <= endDate.Value);
        }

        var orderList = orders.ToList();
        var totalSales = orderList.Sum(o => o.TotalAmount);
        var orderCount = orderList.Count;
        var averageOrderValue = orderCount > 0 ? totalSales / orderCount : 0;

        return Ok(new
        {
            totalSales = totalSales,
            orderCount = orderCount,
            averageOrderValue = averageOrderValue,
            startDate = startDate,
            endDate = endDate
        });
    }
}
