using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.Models;
using OMS_Backend.Services;

namespace OMS_Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class AdminOrderAssignmentsController(OMSDbContext db) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirst("userId")?.Value, out var id) ? id : 0;

    private async Task<bool> CanAssign() => await db.Users.AnyAsync(u => u.Id == UserId
        && u.IsActive && !u.IsDeleted && u.Role != null && u.Role.IsActive
        && (u.Role.Name == "Admin" || u.Role.Name == "Super Admin"))
        && !await OrderVisibility.IsRestrictedAsync(db, UserId);

    [HttpGet("assignment-options")]
    public async Task<IActionResult> Options()
    {
        if (!await CanAssign()) return Forbid();
        var roles = await db.Roles.AsNoTracking().Where(r => r.IsActive && r.Name != "Customer")
            .OrderBy(r => r.Name).Select(r => new
            {
                id = r.Id, name = r.Name,
                users = r.Users.Where(u => u.IsActive && !u.IsDeleted)
                    .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                    .Select(u => new { id = u.Id, name = u.FirstName + " " + u.LastName, email = u.Email }).ToList()
            }).ToListAsync();
        return Ok(roles);
    }

    public class SaveAssignmentsRequest { public List<int> UserIds { get; set; } = new(); }

    private async Task RecordEvent(AdminOrderAssignment assignment, string status)
    {
        var user = await db.Users.SingleAsync(u => u.Id == assignment.UserId);
        var roleName = await db.Roles.IgnoreQueryFilters().Where(r => r.Id == assignment.RoleId)
            .Select(r => r.Name).SingleAsync();
        db.ManufacturingEvents.Add(new ManufacturingEvent {
            OrderId = assignment.OrderId, UserId = user.Id, UserName = user.FirstName + " " + user.LastName,
            RoleId = assignment.RoleId, RoleName = roleName, Status = status, OccurredAt = DateTime.UtcNow
        });
    }

    [HttpGet("{orderId:int}/manufacturing")]
    public async Task<IActionResult> Manufacturing(int orderId)
    {
        if (!await CanAssign()) return Forbid();
        if (!await db.Orders.Where(OrderVisibility.ForUser(db, UserId, false)).AnyAsync(o => o.Id == orderId)) return NotFound();
        var events = await db.ManufacturingEvents.AsNoTracking().Where(e => e.OrderId == orderId)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).ToListAsync();
        return Ok(new { events = events.Select(e => new {
            e.Id, e.UserId, e.UserName, e.RoleId, e.RoleName, e.Status,
            OccurredAt = DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc), e.IsSnapshot
        }) });
    }

    [HttpPut("{orderId:int}/admin-assignments")]
    public async Task<IActionResult> Save(int orderId, SaveAssignmentsRequest request)
    {
        if (!await CanAssign()) return Forbid();
        if (!await db.Orders.Where(OrderVisibility.ForUser(db, UserId, false)).AnyAsync(o => o.Id == orderId))
            return NotFound();
        if (request.UserIds == null || request.UserIds.Count > 1000) return BadRequest(new { message = "Invalid assignees." });
        var ids = request.UserIds.Distinct().ToArray();
        var users = await db.Users.Where(u => ids.Contains(u.Id) && u.IsActive && !u.IsDeleted
            && u.Role != null && u.Role.IsActive && u.Role.Name != "Customer").ToListAsync();
        if (users.Count != ids.Length)
            return BadRequest(new { message = "Select active users with an active non-customer role." });

        var existing = await db.AdminOrderAssignments.Where(a => a.OrderId == orderId).ToListAsync();
        db.AdminOrderAssignments.RemoveRange(existing.Where(a => !ids.Contains(a.UserId)));
        foreach (var removed in existing.Where(a => !ids.Contains(a.UserId)))
            await RecordEvent(removed, "unassigned");
        foreach (var user in users)
        {
            var assignment = existing.SingleOrDefault(a => a.UserId == user.Id);
            if (assignment != null && assignment.RoleId == user.RoleId) continue;
            if (assignment != null) await RecordEvent(assignment, "unassigned");
            if (assignment == null)
            {
                assignment = new AdminOrderAssignment { OrderId = orderId, UserId = user.Id };
                db.AdminOrderAssignments.Add(assignment);
            }
            assignment.RoleId = user.RoleId!.Value;
            assignment.AssignedByUserId = UserId;
            assignment.AssignedAt = DateTime.UtcNow;
            assignment.Status = "assigned";
            await RecordEvent(assignment, "assigned");
        }
        await db.SaveChangesAsync();
        return Ok(new { assignedUserIds = ids });
    }

    public class UpdateAssignmentStatusRequest { public string Status { get; set; } = ""; }

    [HttpPatch("{orderId:int}/assignment-status")]
    public async Task<IActionResult> UpdateStatus(int orderId, UpdateAssignmentStatusRequest request)
    {
        if (request.Status is not ("assigned" or "inprogress" or "done"))
            return BadRequest(new { message = "Choose Assigned, In Progress or Done." });
        if (!await OrderVisibility.IsRestrictedAsync(db, UserId)) return Forbid();
        if (!await db.Orders.Where(OrderVisibility.ForUser(db, UserId, false)).AnyAsync(o => o.Id == orderId))
            return NotFound();
        var assignment = await db.AdminOrderAssignments.SingleAsync(a => a.OrderId == orderId && a.UserId == UserId);
        if (assignment.Status != request.Status)
        {
            assignment.Status = request.Status;
            await RecordEvent(assignment, request.Status);
        }
        await db.SaveChangesAsync();
        return Ok(new { assignmentStatus = assignment.Status });
    }
}
