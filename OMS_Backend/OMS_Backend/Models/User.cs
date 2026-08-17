public class User : BaseEntity
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FirstContact { get; set; }
    public string? SecondContact { get; set; }
    public string Email { get; set; }
    public string? HomeAddress { get; set; }
    public string? OfficeAddress { get; set; }
    public string Password { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; }
}