using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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
            .Select(r => new { r.Id, r.Name, r.IsActive, CanDelete = r.Id > 6 && r.Name != "Super Admin" })
            .ToListAsync();

        return Ok(roles);
    }

    // POST: api/roles
    [HttpPost]
    [Authorize(Roles = "Super Admin,Admin")]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Role name is required." });

        var name = dto.Name.Trim();

        var exists = await _db.Roles
            .AnyAsync(r => r.Name.ToLower() == name.ToLower());

        if (exists)
            return Conflict(new { message = $"Role '{name}' already exists." });

        var role = new Role { Name = name, IsActive = true };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return Ok(new { role.Id, role.Name, role.IsActive, CanDelete = role.Id > 6 && role.Name != "Super Admin" });
    }

    [HttpPatch("{id:int}/activate")]
    [Authorize(Roles = "Super Admin,Admin")]
    public async Task<IActionResult> Activate(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return NotFound();
        role.IsActive = true;
        role.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/roles/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Super Admin,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!int.TryParse(User.FindFirst("userId")?.Value, out var actorId) ||
            !await _db.Users.AnyAsync(u => u.Id == actorId && u.IsActive && !u.IsDeleted &&
                u.Role != null && u.Role.IsActive && (u.Role.Name == "Admin" || u.Role.Name == "Super Admin")))
            return Forbid();
        var role = await _db.Roles.FindAsync(id);
        if (role is null) return NotFound();

        // Seeded roles are referenced by account and integration business rules.
        if (role.Id <= 6 || role.Name == "Super Admin")
            return Conflict(new { message = "Built-in roles cannot be deleted." });

        var hasUsers = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.RoleId == id);
        if (hasUsers)
            return Conflict(new { message = "Cannot delete a role that is assigned to users." });

        _db.Roles.Remove(role);
        try
        {
            // RolePermissions cascade on delete; user references restrict deletion.
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            return Conflict(new { message = "This role is in use and cannot be deleted. Refresh and try again." });
        }
        return NoContent();
    }
}
