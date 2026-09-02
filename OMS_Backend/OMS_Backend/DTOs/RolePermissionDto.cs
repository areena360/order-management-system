namespace OMS_Backend.DTOs
{
    public class RolePermissionDto
    {
        public string ScreenKey { get; set; }
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class SaveRolePermissionsDto
    {
        public int RoleId { get; set; }
        public List<RolePermissionDto> Permissions { get; set; } = new();
    }
}