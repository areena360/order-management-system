using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.Services;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly OMSDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEmailService _emailService;

    public UsersController(
        OMSDbContext db,
        IPasswordHasher<User> passwordHasher,
        IEmailService emailService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
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
                u.WebsiteUrl,
                u.RoleId,
                Role = u.Role != null ? u.Role.Name : "No Role",
                u.IsActive,
                u.IsDeleted,
                u.CreatedDate,
                CreatedBy = u.CreatedBy.ToString(),
                u.UpdatedDate,
                UpdatedBy = u.UpdatedBy != null
                    ? u.UpdatedBy.ToString()
                    : null
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserDto dto)
    {

        if (!await HasPermissionAsync("add"))
            return Forbid();

        var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (emailExists)
            throw new ConflictException($"A user with email '{dto.Email}' already exists.");


        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            FirstContact = dto.FirstContact,
            SecondContact = dto.SecondContact,
            HomeAddress = dto.HomeAddress,
            OfficeAddress = dto.OfficeAddress,
            WebsiteUrl = dto.WebsiteUrl,
            RoleId = dto.RoleId,
            IsActive = true
        };

        user.Password = _passwordHasher.HashPassword(
            user,
            dto.Password);

        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        return Ok(new { user.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UserDto dto)
    {

        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (!await HasPermissionAsync("edit"))
            return Forbid();
        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);


        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.FirstContact = dto.FirstContact;
        user.SecondContact = dto.SecondContact;
        user.HomeAddress = dto.HomeAddress;
        user.OfficeAddress = dto.OfficeAddress;
        user.WebsiteUrl = dto.WebsiteUrl;
        user.RoleId = dto.RoleId;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.Password = _passwordHasher.HashPassword(
                user,
                dto.Password);
        }

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDelete(int id)
    {

        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);


        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (!await HasPermissionAsync("delete"))
            return Forbid();
        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);


        user.IsDeleted = true;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> Restore(int id)
    {

        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);


        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (!await HasPermissionAsync("delete"))
            return Forbid();
        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);


        user.IsDeleted = false;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);

        // Check the previous state
        bool wasInactive = !user.IsActive;

        // Toggle active status
        user.IsActive = !user.IsActive;

        await _db.SaveChangesAsync();

        // Send activation email only when:
        // Inactive -> Active
        if (wasInactive && user.IsActive)
        {
            try
            {
                await _emailService.SendAccountActivationEmailAsync(
                    user.Email,
                    user.FirstName);
            }
            catch (Exception ex)
            {
                // Account is already activated.
                // Email failure should not undo activation.
                // Intentional swallow — logging via ILogger recommended here
                // instead of Console.WriteLine, but scope kept out of this
                // controller since it's not part of global handling.
                Console.WriteLine(
                    $"Activation email failed for {user.Email}: {ex.Message}");
            }
        }

        return Ok(new
        {
            user.IsActive
        });
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

}