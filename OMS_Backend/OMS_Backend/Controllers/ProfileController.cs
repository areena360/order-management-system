using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // any authenticated user — NOT restricted to Admin/Super Admin
    public class ProfileController : ControllerBase
    {
        private readonly OMSDbContext _db;

        public ProfileController(OMSDbContext db)
        {
            _db = db;
        }

        // GET api/profile/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid token." });

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

            if (user == null) return NotFound(new { message = "User not found." });

            return Ok(user);
        }
    }
}