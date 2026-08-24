using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Super Admin")] // built-in role-based authorization — checked against JWT's role claim
    public class AdminController : ControllerBase
    {
        private readonly OMSDbContext _db;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public AdminController(OMSDbContext db)
        {
            _db = db;
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetAssignableRoles()
        {
            var roles = await _db.Roles
                .Where(r => !r.IsDeleted)
                .Select(r => new { r.Id, r.Name })
                .ToListAsync();

            return Ok(roles);
        }
    }
}