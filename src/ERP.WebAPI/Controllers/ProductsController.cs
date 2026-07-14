using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Sales,Accounts")]
    public async Task<IActionResult> GetAll()
    {
        var products = await _unitOfWork.Products.GetAllAsync();
        return Ok(products);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Manager,Sales,Accounts")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        if (product == null) return BadRequest();
        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.CompleteAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.ProductID }, product);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] Product product)
    {
        if (product == null || product.ProductID != id) return BadRequest();
        var existing = await _unitOfWork.Products.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.ProductName = product.ProductName;
        existing.Category = product.Category;
        existing.Unit = product.Unit;
        existing.Rate = product.Rate;
        existing.GST = product.GST;
        existing.Status = product.Status;

        _unitOfWork.Products.Update(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _unitOfWork.Products.GetByIdAsync(id);
        if (existing == null) return NotFound();

        _unitOfWork.Products.Delete(existing);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }
}
