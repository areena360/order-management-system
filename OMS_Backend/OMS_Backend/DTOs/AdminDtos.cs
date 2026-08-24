using System.ComponentModel.DataAnnotations;

namespace OMS_Backend.DTOs
{
    public class CreateUserByAdminDto
    {
        [Required, StringLength(50, MinimumLength = 2)]
        public string FirstName { get; set; }

        [Required, StringLength(50, MinimumLength = 2)]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, Phone]
        public string FirstContact { get; set; }

        [Required]
        public int RoleId { get; set; } // Super Admin picks from Role dropdown (Admin, Finance, Staff, Sales)

        [Required]
        [StringLength(32, MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*(),.?"":{}|<>_\-]).+$",
            ErrorMessage = "Password must contain uppercase, lowercase, a number, and a special character.")]
        public string Password { get; set; }

        [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class UserListItemDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string FirstContact { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}