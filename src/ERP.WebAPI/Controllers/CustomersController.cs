using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public CustomersController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Sales,Accounts")]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _unitOfWork.Customers.GetAllAsync();
        return Ok(customers);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Manager,Sales,Accounts")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(id);
        if (customer == null) return NotFound();
        return Ok(customer);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] Customer customer)
    {
        if (customer == null) return BadRequest();
        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.CompleteAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerID }, customer);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] Customer customer)
    {
        if (customer == null || customer.CustomerID != id) return BadRequest();
        var existing = await _unitOfWork.Customers.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.CompanyName = customer.CompanyName;
        existing.ContactPerson = customer.ContactPerson;
        existing.Phone = customer.Phone;
        existing.Email = customer.Email;

        _unitOfWork.Customers.Update(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _unitOfWork.Customers.GetByIdAsync(id);
        if (existing == null) return NotFound();

        _unitOfWork.Customers.Delete(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }
}
