public class RolePermission : BaseEntity
{
    public int RoleId { get; set; }
    public Role Role { get; set; }
    public string ScreenKey { get; set; }
    public bool CanView { get; set; }
    public bool CanAdd { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}