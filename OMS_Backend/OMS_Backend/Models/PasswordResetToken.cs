namespace OMS_Backend.Models
{
    public class PasswordResetToken : BaseEntity
    {
        public int UserId { get; set; }
        public string Token { get; set; }        // generated via built-in RandomNumberGenerator
        public DateTime ExpiryDate { get; set; }  // 30–60 min validity
        public bool IsUsed { get; set; }

        public User User { get; set; }
    }
}