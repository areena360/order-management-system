using System.ComponentModel.DataAnnotations;

namespace OMS_Backend.Models;

public class AuthSession
{
    [Key, MaxLength(64)] public string TokenHash { get; set; } = "";
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [MaxLength(64)] public string PasswordVersion { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool RememberMe { get; set; }
}
