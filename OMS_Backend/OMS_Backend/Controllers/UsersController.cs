using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using System;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly OMSDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UsersController(OMSDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.FirstContact,
                u.SecondContact,
                u.HomeAddress,
                u.OfficeAddress,
                u.RoleId,
                Role = u.Role != null ? u.Role.Name : "No Role",
                u.IsActive,
                u.IsDeleted,
                u.CreatedDate,
                CreatedBy = u.CreatedBy.ToString(),
                u.UpdatedDate,
                UpdatedBy = u.UpdatedBy != null ? u.UpdatedBy.ToString() : null
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserDto dto)
    {
<<<<<<< Updated upstream
=======
        if (!await HasPermissionAsync("add"))
            return Forbid();
        var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (emailExists)
            throw new ConflictException($"A user with email '{dto.Email}' already exists.");

>>>>>>> Stashed changes
        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            FirstContact = dto.FirstContact,
            RoleId = dto.RoleId,
            IsActive = true // admin-created users are active immediately
        };
        user.Password = _passwordHasher.HashPassword(user, dto.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { user.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserDto dto)
    {
<<<<<<< Updated upstream
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
=======
        if (!await HasPermissionAsync("edit"))
            return Forbid();
        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);
>>>>>>> Stashed changes

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.FirstContact = dto.FirstContact;
        user.RoleId = dto.RoleId;

        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.Password = _passwordHasher.HashPassword(user, dto.Password);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDelete(int id)
    {
<<<<<<< Updated upstream
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
=======
        if (!await HasPermissionAsync("delete"))
            return Forbid();
        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);

>>>>>>> Stashed changes
        user.IsDeleted = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
<<<<<<< Updated upstream
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
=======
        if (!await HasPermissionAsync("delete"))
            return Forbid();
        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);

>>>>>>> Stashed changes
        user.IsDeleted = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Admin/Super Admin verifies (or revokes) a user's account
    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { user.IsActive });
    }

<<<<<<< Updated upstream
public class UserDto
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string FirstContact { get; set; }
    public int RoleId { get; set; }
    public string? Password { get; set; }
=======
    private async Task<bool> HasPermissionAsync(string action)
    {
        var roleName = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (roleName == "Super Admin") return true;

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role == null) return false;

        var perm = await _db.RolePermissions
            .FirstOrDefaultAsync(p => p.RoleId == role.Id && p.ScreenKey == "Manage Users" && !p.IsDeleted);

        if (perm == null) return false;

        return action switch
        {
            "view" => perm.CanView,
            "add" => perm.CanAdd,
            "edit" => perm.CanEdit,
            "delete" => perm.CanDelete,
            _ => false
        };
    }

    public class UserDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string FirstContact { get; set; }
        public string? SecondContact { get; set; }
        public string? HomeAddress { get; set; }
        public string? OfficeAddress { get; set; }
        public string? WebsiteUrl { get; set; }
        public int RoleId { get; set; }
        public string? Password { get; set; }
    }
>>>>>>> Stashed changes
}