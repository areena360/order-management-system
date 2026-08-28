using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RolePermissionsController : ControllerBase
    {
        private static readonly string[] Screens =
        {
            "Dashboard", "Manage Users", "Manage Roles"
        };

        private readonly OMSDbContext _db;
        public RolePermissionsController(OMSDbContext db) => _db = db;

        [HttpGet("{roleId}")]
        public async Task<IActionResult> GetByRole(int roleId)
        {
            var saved = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
                .ToListAsync();

            // Merge with full screen list so new screens always appear
            var result = Screens.Select(s =>
            {
                var match = saved.FirstOrDefault(p => p.ScreenKey == s);
                return new RolePermissionDto
                {
                    ScreenKey = s,
                    CanView = match?.CanView ?? false,
                    CanAdd = match?.CanAdd ?? false,
                    CanEdit = match?.CanEdit ?? false,
                    CanDelete = match?.CanDelete ?? false
                };
            });

            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> Save([FromBody] SaveRolePermissionsDto dto)
        {
            var existing = await _db.RolePermissions
                .Where(rp => rp.RoleId == dto.RoleId)
                .ToListAsync();

            foreach (var perm in dto.Permissions)
            {
                var row = existing.FirstOrDefault(e => e.ScreenKey == perm.ScreenKey);
                if (row == null)
                {
                    _db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = dto.RoleId,
                        ScreenKey = perm.ScreenKey,
                        CanView = perm.CanView,
                        CanAdd = perm.CanAdd,
                        CanEdit = perm.CanEdit,
                        CanDelete = perm.CanDelete,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    });
                }
                else
                {
                    row.CanView = perm.CanView;
                    row.CanAdd = perm.CanAdd;
                    row.CanEdit = perm.CanEdit;
                    row.CanDelete = perm.CanDelete;
                    row.UpdatedDate = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Permissions saved successfully." });
        }
    }
}