using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Services;

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var auditor = new Role { Id = 27, Name = "Auditor" };
var users = Enumerable.Range(1, 30).Select(id => new User
{
    Id = id, FirstName = "User", LastName = id.ToString(), FirstContact = "123",
    Email = $"user{id}@example.test", RoleId = id == 1 ? 1 : 27, Role = auditor,
    IsDeleted = id == 30, CreatedDate = new DateTime(2026, 1, 1).AddDays(id)
}).AsQueryable();
var request = new UserQueryDto();
var filtered = UserListQuery.Filter(users, request);
Check(filtered.Count() == 28, "Default filter must exclude deleted users and Super Admin.");
var secondPage = UserListQuery.Sort(filtered, request).Skip(8).Take(8).ToArray();
Check(secondPage.Length == 8 && secondPage[0].Id == 21, "Second page must contain only its own ordered rows.");
Check(UserListQuery.Filter(users, new() { Search = "user25@" }).Single().Id == 25, "Search must work across all pages.");
Check(UserListQuery.Filter(users, new() { Status = "deleted", Role = "Auditor" }).Single().Id == 30, "Combined role/deleted filter failed.");
Check(UserListQuery.Filter(users, new() { Status = "all" }).Count() == 29, "All filter must include deleted users.");

// SQL generation verifies EF translation without opening a database connection.
var options = new DbContextOptionsBuilder<OMSDbContext>()
    .UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=true;TrustServerCertificate=true").Options;
using var db = new OMSDbContext(options);
foreach (var sort in new[] { "fullName", "firstContact", "secondContact", "email", "homeAddress", "officeAddress", "websiteUrl", "role", "status", "isDeleted", "createdDate", "createdBy", "updatedDate", "updatedBy" })
{
    var q = new UserQueryDto { Search = "auditor", Role = "Auditor", SortBy = sort };
    var sql = UserListQuery.Sort(UserListQuery.Filter(db.Users.AsNoTracking(), q), q)
        .Skip(8).Take(8).Select(u => new { u.Id, u.FirstName }).ToQueryString();
    Check(sql.Contains("OFFSET") && sql.Contains("FETCH NEXT"), $"Missing SQL pagination for {sort}.");
}
Console.WriteLine("User pagination checks passed: filters, page boundaries, global search, and all 14 SQL sort translations.");
