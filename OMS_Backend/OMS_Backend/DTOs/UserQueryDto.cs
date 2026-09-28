namespace OMS_Backend.DTOs;

public class UserQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string Status { get; set; } = "active";
    public string SortBy { get; set; } = "createdDate";
    public string SortDirection { get; set; } = "desc";
}
