using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;

// Track test entities so FindAsync resolves them without any database connection.
using var db = new OMSDbContext(new DbContextOptionsBuilder<OMSDbContext>()
    .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
var admin = new User { Id = 10, RoleId = 1, IsActive = true };
var pending = new User { Id = 20, RoleId = null, ApprovalStatus = "Pending", IsActive = false };
db.AttachRange(admin, pending);
var controller = new UsersController(db, null!, null!) {
    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", "10") }, "test"))
    }}
};
async Task ExpectBad(UserStatusDto dto, string test) {
    if (await controller.SetStatus(20, dto) is not BadRequestObjectResult)
        throw new Exception(test);
    if (pending.IsActive || pending.RoleId != null || pending.ApprovalStatus != "Pending")
        throw new Exception("Rejected request mutated user: " + test);
}
await ExpectBad(new() { Status = "Approved" }, "Role is mandatory at API boundary");
await ExpectBad(new() { Status = "Unknown" }, "Invalid status rejected");
pending.IsDeleted = true;
await ExpectBad(new() { Status = "Approved", RoleId = 4 }, "Deleted user cannot be approved");
pending.IsDeleted = false;
if (await controller.SetStatus(10, new() { Status = "Rejected" }) is not BadRequestObjectResult)
    throw new Exception("Super Admin must be protected");
admin.IsActive = false;
if (await controller.SetStatus(20, new() { Status = "Approved", RoleId = 4 }) is not ForbidResult)
    throw new Exception("Inactive approver must be forbidden");
Console.WriteLine("PASS: API requires role, rejects invalid status, protects deleted/Super Admin users, denies inactive approvers; no database writes.");
