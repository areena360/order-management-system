using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;
using OMS_Backend.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Super Admin,Admin")]
    public class RolePermissionsController : ControllerBase
    {
        private static readonly string[] Screens =
        {
            "Dashboard", "Manage Users", "Manage Roles", "Orders", "Order Customer Chat", "Order Group Chat",
        };

        private readonly OMSDbContext _db;
        private readonly IHubContext<ChatHub> _hub;
        public RolePermissionsController(OMSDbContext db, IHubContext<ChatHub> hub) { _db = db; _hub = hub; }

        private async Task<bool> CanManage()
        {
            if (!int.TryParse(User.FindFirst("userId")?.Value, out var id)) return false;
            return await _db.Users.AnyAsync(u => u.Id == id && u.IsActive && !u.IsDeleted
                && u.Role != null && (u.Role.Name == "Admin" || u.Role.Name == "Super Admin"));
        }

        [HttpGet("{roleId}")]
        public async Task<IActionResult> GetByRole(int roleId)
        {
            if (!await CanManage()) return Forbid();
            var saved = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            // Merge with full screen list so new screens always appear
            var result = Screens.Select(s =>
            {
                var match = saved.FirstOrDefault(p => p.ScreenKey == s);
                return new RolePermissionDto
                {
                    ScreenKey = s,
                    CanView = match == null ? s == "Order Customer Chat" : !match.IsDeleted && match.IsActive && match.CanView,
                    CanAdd = match == null ? s == "Order Customer Chat" : !match.IsDeleted && match.IsActive && match.CanView && match.CanAdd,
                    CanEdit = match?.CanEdit ?? false,
                    CanDelete = match?.CanDelete ?? false
                };
            });

            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> Save([FromBody] SaveRolePermissionsDto dto)
        {
            if (!await CanManage()) return Forbid();
            var targetRole = await _db.Roles.FindAsync(dto.RoleId);
            if (targetRole == null || targetRole.Name == "Super Admin") return BadRequest("This role cannot be changed.");
            if (dto.Permissions == null || dto.Permissions.Any(p => !Screens.Contains(p.ScreenKey))
                || dto.Permissions.Select(p => p.ScreenKey).Distinct().Count() != dto.Permissions.Count)
                return BadRequest("Invalid permissions.");
            foreach (var permission in dto.Permissions.Where(p => p.ScreenKey is "Order Customer Chat" or "Order Group Chat"))
            {
                if (targetRole.Name == "Customer" && permission.ScreenKey == "Order Group Chat") permission.CanView = false;
                permission.CanAdd = permission.CanView && permission.CanAdd;
                permission.CanEdit = permission.CanDelete = false;
            }
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
                    row.IsActive = true;
                    row.IsDeleted = false;
                    row.CanView = perm.CanView;
                    row.CanAdd = perm.CanAdd;
                    row.CanEdit = perm.CanEdit;
                    row.CanDelete = perm.CanDelete;
                    row.UpdatedDate = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            var userGroups = await _db.Users.Where(u => u.RoleId == dto.RoleId && !u.IsDeleted).Select(u => "user_" + u.Id).ToListAsync();
            await _hub.Clients.Groups(userGroups).SendAsync("PermissionsChanged");
            return Ok(new { message = "Permissions saved successfully." });
        }
    }
}
