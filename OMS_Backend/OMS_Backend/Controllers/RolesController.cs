using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;
using OMS_Backend.Common.ExceptionHandling;

namespace OMS_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly OMSDbContext _db;
    public RolesController(OMSDbContext db) => _db = db;

    // GET: api/roles
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.IsActive, CanDelete = r.Name != "Super Admin" })
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

        var exists = await _db.Roles.AnyAsync(r => r.Name.ToLower() == name.ToLower());
        if (exists)
            return Conflict(new { message = $"Role '{name}' already exists." });

        var role = new Role { Name = name, IsActive = true };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return Ok(new { role.Id, role.Name, role.IsActive, CanDelete = true });
    }

    // PUT: api/roles/{id}   → rename
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Super Admin,Admin")]
    public async Task<IActionResult> Rename(int id, [FromBody] CreateRoleDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Role name is required." });

        var name = dto.Name.Trim();

        var role = await _db.Roles.FindAsync(id);
        if (role is null) return NotFound();

        // Only Super Admin is protected (it's the system owner role).
        if (role.Name == "Super Admin")
            return Conflict(new { message = "Super Admin cannot be renamed." });

        if (role.Name == name)
            return Ok(new { role.Id, role.Name, role.IsActive, CanDelete = true });

        var exists = await _db.Roles.AnyAsync(r => r.Id != id && r.Name.ToLower() == name.ToLower());
        if (exists)
            return Conflict(new { message = $"Role '{name}' already exists." });

        role.Name = name;
        role.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { role.Id, role.Name, role.IsActive, CanDelete = true });
    }

    // PATCH: api/roles/{id}/activate
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

        // Only Super Admin remains protected — every other role (including seed roles) can be deleted.
        if (role.Name == "Super Admin")
            return Conflict(new { message = "Super Admin cannot be deleted." });

        var hasUsers = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.RoleId == id);
        if (hasUsers)
            return Conflict(new { message = "Cannot delete a role that is assigned to users." });

        _db.Roles.Remove(role);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            await HttpContext.RequestServices.GetRequiredService<IExceptionRecorder>()
                .RecordAsync(ex, "Api", HttpContext, "DeleteRole", 409);
            HttpContext.Items["ExceptionRecorded"] = true;
            return Conflict(new { message = "This role is in use and cannot be deleted. Refresh and try again." });
        }
        return NoContent();
    }
}
