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
public class PaymentsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Accounts")]
    public async Task<IActionResult> GetAll()
    {
        var payments = await _unitOfWork.Payments.GetAllAsync();
        return Ok(payments);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Manager,Accounts")]
    public async Task<IActionResult> GetById(int id)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(id);
        if (payment == null) return NotFound();
        return Ok(payment);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Sales,Accounts")]
    public async Task<IActionResult> Create([FromBody] Payment payment)
    {
        if (payment == null) return BadRequest();
        
        payment.PaymentDate = payment.PaymentDate == default ? DateTime.UtcNow : payment.PaymentDate;

        await _unitOfWork.Payments.AddAsync(payment);

        // Update corresponding Order's PaidAmount if OrderID is specified
        if (payment.OrderID.HasValue)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(payment.OrderID.Value);
            if (order != null)
            {
                order.PaidAmount += payment.Amount;
                _unitOfWork.Orders.Update(order);
            }
        }

        await _unitOfWork.CompleteAsync();
        return CreatedAtAction(nameof(GetById), new { id = payment.PaymentID }, payment);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] Payment payment)
    {
        if (payment == null || payment.PaymentID != id) return BadRequest();
        var existing = await _unitOfWork.Payments.GetByIdAsync(id);
        if (existing == null) return NotFound();

        // Adjust order paid amount if needed
        if (existing.OrderID.HasValue && existing.OrderID == payment.OrderID)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(existing.OrderID.Value);
            if (order != null)
            {
                order.PaidAmount = order.PaidAmount - existing.Amount + payment.Amount;
                _unitOfWork.Orders.Update(order);
            }
        }

        existing.Amount = payment.Amount;
        existing.PaymentMode = payment.PaymentMode;
        existing.ReferenceNumber = payment.ReferenceNumber;
        existing.Remarks = payment.Remarks;

        _unitOfWork.Payments.Update(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _unitOfWork.Payments.GetByIdAsync(id);
        if (existing == null) return NotFound();

        if (existing.OrderID.HasValue)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(existing.OrderID.Value);
            if (order != null)
            {
                order.PaidAmount -= existing.Amount;
                _unitOfWork.Orders.Update(order);
            }
        }

        _unitOfWork.Payments.Delete(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }
}
