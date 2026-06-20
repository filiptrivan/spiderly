using Backend.Data;
using Backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

// Oracle (known-good) CRUD for Supplier. [ApiController] auto-returns 400 on an invalid model
// (Name/Email), so an invalid create/update never persists. [Authorize] requires authentication.
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SuppliersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetAll()
        => await db.Suppliers.ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Supplier>> GetById(int id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        return supplier is null ? NotFound() : supplier;
    }

    [HttpPost]
    public async Task<ActionResult<Supplier>> Create(Supplier supplier)
    {
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Supplier input)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        supplier.Name = input.Name;
        supplier.Email = input.Email;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
