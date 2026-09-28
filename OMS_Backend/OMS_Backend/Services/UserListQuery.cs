using System.Linq.Expressions;
using OMS_Backend.DTOs;

namespace OMS_Backend.Services;

public static class UserListQuery
{
    public static IQueryable<User> Filter(IQueryable<User> users, UserQueryDto q)
    {
        users = users.Where(u => u.RoleId == null || u.RoleId != 1);
        users = q.Status switch
        {
            "deleted" => users.Where(u => u.IsDeleted),
            "all" => users,
            _ => users.Where(u => !u.IsDeleted)
        };
        if (!string.IsNullOrWhiteSpace(q.Role) && q.Role != "All")
            users = users.Where(u => (u.Role != null ? u.Role.Name : "No Role") == q.Role);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLower();
            users = users.Where(u =>
                u.Id.ToString().Contains(term) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                u.FirstContact.Contains(term) || (u.SecondContact != null && u.SecondContact.Contains(term)) ||
                u.Email.ToLower().Contains(term) ||
                (u.HomeAddress != null && u.HomeAddress.ToLower().Contains(term)) ||
                (u.OfficeAddress != null && u.OfficeAddress.ToLower().Contains(term)) ||
                (u.WebsiteUrl != null && u.WebsiteUrl.ToLower().Contains(term)) ||
                (u.Role != null ? u.Role.Name : "No Role").ToLower().Contains(term) ||
                (u.RoleId != null && u.RoleId.ToString()!.Contains(term)) ||
                u.ApprovalStatus.ToLower().Contains(term) ||
                (u.IsDeleted ? "deleted" : "not deleted").Contains(term) ||
                u.CreatedDate.ToString().Contains(term) || u.CreatedBy.ToString().Contains(term) ||
                (u.UpdatedDate != null && u.UpdatedDate.ToString()!.Contains(term)) ||
                (u.UpdatedBy != null && u.UpdatedBy.ToString()!.Contains(term)));
        }
        return users;
    }

    public static IOrderedQueryable<User> Sort(IQueryable<User> users, UserQueryDto q)
    {
        var desc = q.SortDirection != "asc";
        IOrderedQueryable<User> Order<T>(Expression<Func<User, T>> key) =>
            desc ? users.OrderByDescending(key) : users.OrderBy(key);
        var sorted = q.SortBy switch
        {
            "fullName" => Order(u => u.FirstName + " " + u.LastName),
            "firstContact" => Order(u => u.FirstContact),
            "secondContact" => Order(u => u.SecondContact),
            "email" => Order(u => u.Email),
            "homeAddress" => Order(u => u.HomeAddress),
            "officeAddress" => Order(u => u.OfficeAddress),
            "websiteUrl" => Order(u => u.WebsiteUrl),
            "role" => Order(u => u.Role != null ? u.Role.Name : "No Role"),
            "status" => Order(u => u.ApprovalStatus),
            "isDeleted" => Order(u => u.IsDeleted),
            "createdBy" => Order(u => u.CreatedBy),
            "updatedDate" => Order(u => u.UpdatedDate),
            "updatedBy" => Order(u => u.UpdatedBy),
            _ => Order(u => u.CreatedDate)
        };
        return sorted.ThenByDescending(u => u.Id);
    }
}
