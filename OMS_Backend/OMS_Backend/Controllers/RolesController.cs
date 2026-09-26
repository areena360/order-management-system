using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;        // ✅ OMSDbContext yahan hai
using OMS_Backend.DTOs;        // ✅ CreateRoleDto yahan hoga
using OMS_Backend.Models;      // ✅ Role entity yahan hai

namespace OMS_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly OMSDbContext _db;   // ✅ OMSDbContext (not ApplicationDbContext)
    public RolesController(OMSDbContext db) => _db = db;

    // GET: api/roles
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        return Ok(roles);
    }

    // POST: api/roles
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Role name is required." });

        var name = dto.Name.Trim();

        var exists = await _db.Roles
            .AnyAsync(r => r.Name.ToLower() == name.ToLower());

        if (exists)
            return Conflict(new { message = $"Role '{name}' already exists." });

        var role = new Role { Name = name };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return Ok(new { role.Id, role.Name });
    }

    // DELETE: api/roles/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null) return NotFound();

        var hasUsers = await _db.Users.AnyAsync(u => u.RoleId == id);
        if (hasUsers)
            return Conflict(new { message = "Cannot delete a role that is assigned to users." });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}