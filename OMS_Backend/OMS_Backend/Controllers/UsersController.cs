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
            .OrderByDescending(u => u.CreatedDate).ThenByDescending(u => u.Id)
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
                Status = u.ApprovalStatus,
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
        var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (emailExists)
            throw new ConflictException($"A user with email '{dto.Email}' already exists.");

        if (dto.RoleId == null || !await _db.Roles.AnyAsync(r => r.Id == dto.RoleId && r.Id != 1 && !r.IsDeleted))
            return BadRequest(new { message = "Select a valid role." });

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

        if (user.IsDeleted) return BadRequest(new { message = "Deleted users cannot be edited." });
        if (user.RoleId != null && (dto.RoleId == null || !await _db.Roles.AnyAsync(r => r.Id == dto.RoleId && !r.IsDeleted)))
            return BadRequest(new { message = "Select a valid role." });
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.FirstContact = dto.FirstContact;
        user.SecondContact = dto.SecondContact;
        user.HomeAddress = dto.HomeAddress;
        user.OfficeAddress = dto.OfficeAddress;
        user.WebsiteUrl = dto.WebsiteUrl;
        if (user.RoleId != null) user.RoleId = dto.RoleId;

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

        user.IsDeleted = true;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] UserStatusDto dto)
    {
        var actorId = User.FindFirst("userId")?.Value;
        var actor = int.TryParse(actorId, out var parsedId)
            ? await _db.Users.FindAsync(parsedId) : null;
        if (actor == null || !actor.IsActive || actor.IsDeleted ||
            (actor.RoleId != 1 && !await _db.RolePermissions.AnyAsync(p =>
                p.RoleId == actor.RoleId && p.ScreenKey == "Manage Users" && p.CanEdit && !p.IsDeleted)))
            return Forbid();

        var user = await _db.Users.FindAsync(id)
            ?? throw new NotFoundException(nameof(User), id);
        if (user.IsDeleted || user.RoleId == 1)
            return BadRequest(new { message = "This user's status cannot be changed." });
        if (dto.Status is not ("Pending" or "Approved" or "Rejected"))
            return BadRequest(new { message = "Select Pending, Approved, or Rejected." });
        if (dto.Status == "Approved")
        {
            if (dto.RoleId == null || !await _db.Roles.AnyAsync(r =>
                r.Id == dto.RoleId && r.Id != 1 && !r.IsDeleted && r.IsActive))
                return BadRequest(new { message = "You must select a valid role before approving this user." });
            user.RoleId = dto.RoleId;
        }
        bool wasInactive = !user.IsActive;
        user.ApprovalStatus = dto.Status;
        user.IsActive = dto.Status == "Approved";
        user.UpdatedDate = DateTime.UtcNow;
        user.UpdatedBy = actor.Id;

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
            user.IsActive,
            Status = user.ApprovalStatus,
            user.RoleId
        });
    }

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
    public int? RoleId { get; set; }
    public string? Password { get; set; }
}
public class UserStatusDto
{
    public string Status { get; set; } = "";
    public int? RoleId { get; set; }
}
