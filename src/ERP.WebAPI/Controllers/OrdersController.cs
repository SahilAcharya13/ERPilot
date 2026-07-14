using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public OrdersController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Sales,Accounts")]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _unitOfWork.Orders.GetAllAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Manager,Sales,Accounts")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id);
        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Sales")]
    public async Task<IActionResult> Create([FromBody] Order order)
    {
        if (order == null) return BadRequest();
        
        // Ensure default values if not provided
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            order.OrderNumber = "ORD-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
        }
        
        order.OrderDate = DateTime.UtcNow;

        // Automatically calculate totals from nested items if any
        decimal calculatedTotal = 0;
        if (order.OrderItems != null)
        {
            foreach (var item in order.OrderItems)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductID);
                if (product != null)
                {
                    // Rate is either specified on item or inherited from product
                    if (item.Rate == 0) item.Rate = product.Rate;
                    if (string.IsNullOrEmpty(item.Unit)) item.Unit = product.Unit;
                    
                    // Simple total calculation for Order Amount (Quantity * Rate)
                    // The GST is applied as well:
                    var baseAmount = item.Quantity * item.Rate;
                    var gstAmount = baseAmount * (product.GST / 100m);
                    calculatedTotal += (baseAmount + gstAmount);
                }
            }
        }

        if (order.TotalAmount == 0)
        {
            order.TotalAmount = calculatedTotal;
        }

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.CompleteAsync();
        return CreatedAtAction(nameof(GetById), new { id = order.OrderID }, order);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] Order order)
    {
        if (order == null || order.OrderID != id) return BadRequest();
        var existing = await _unitOfWork.Orders.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.OrderStatus = order.OrderStatus;
        existing.TotalAmount = order.TotalAmount;
        existing.PaidAmount = order.PaidAmount;
        existing.Remarks = order.Remarks;

        _unitOfWork.Orders.Update(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _unitOfWork.Orders.GetByIdAsync(id);
        if (existing == null) return NotFound();

        _unitOfWork.Orders.Delete(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }
}
