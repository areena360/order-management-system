namespace OMS_Backend.DTOs
{
    public class UpdateProfileDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? WebsiteUrl { get; set; }
        public string FirstContact { get; set; } = string.Empty;
        public string? SecondContact { get; set; }
        public string? HomeAddress { get; set; }
        public string? OfficeAddress { get; set; }
    }
}