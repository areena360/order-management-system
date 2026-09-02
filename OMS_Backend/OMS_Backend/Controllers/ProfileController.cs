using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using System.Linq;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly OMSDbContext _db;
        public ProfileController(OMSDbContext db)
        {
            _db = db;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAppException("Invalid token.");

            var user = await _db.Users
                .Include(u => u.Role)
                .Where(u => u.Id == userId && !u.IsDeleted)
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
                    Role = u.Role != null ? u.Role.Name : "No Role",
                    u.IsActive,
                    u.CreatedDate,
                    u.UpdatedDate
                })
                .FirstOrDefaultAsync();

            if (user == null)
                throw new NotFoundException("User", userId);

            return Ok(user);
        }

        // PUT api/profile/me
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileDto dto)
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAppException("Invalid token.");

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
                throw new NotFoundException("User", userId);

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.WebsiteUrl = dto.WebsiteUrl;
            user.FirstContact = dto.FirstContact;
            user.SecondContact = dto.SecondContact;
            user.HomeAddress = dto.HomeAddress;
            user.OfficeAddress = dto.OfficeAddress;
            user.UpdatedDate = DateTime.UtcNow;
            user.UpdatedBy = user.Id;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.FirstContact,
                user.SecondContact,
                user.HomeAddress,
                user.OfficeAddress,
                user.WebsiteUrl,
                Role = user.Role != null ? user.Role.Name : "No Role",
                user.IsActive,
                user.CreatedDate,
                user.UpdatedDate
            });
        }

        [HttpGet("permissions")]
        public async Task<IActionResult> GetMyPermissions()
        {
            var roleName = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value;

            var screens = new[]
            {
                "Dashboard", "Manage Users", "Manage Roles",
            };

            // Super Admin: full access always, no DB lookup needed
            if (roleName == "Super Admin")
            {
                return Ok(screens.Select(s => new
                {
                    screenKey = s,
                    canView = true,
                    canAdd = true,
                    canEdit = true,
                    canDelete = true
                }));
            }

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null) return Ok(Array.Empty<object>());

            var saved = await _db.RolePermissions
                .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
                .ToListAsync();

            var result = screens.Select(s =>
            {
                var match = saved.FirstOrDefault(p => p.ScreenKey == s);
                return new
                {
                    screenKey = s,
                    canView = match?.CanView ?? false,
                    canAdd = match?.CanAdd ?? false,
                    canEdit = match?.CanEdit ?? false,
                    canDelete = match?.CanDelete ?? false
                };
            });

            return Ok(result);
        }
    }
}