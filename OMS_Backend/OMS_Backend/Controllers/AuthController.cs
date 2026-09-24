using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;
using OMS_Backend.Services;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly OMSDbContext _db;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private const string SessionCookie = "oms_refresh";

        private readonly PasswordHasher<User> _passwordHasher =
            new PasswordHasher<User>();

        public AuthController(
            OMSDbContext db,
            IJwtService jwtService,
            IEmailService emailService,
            IConfiguration config)
        {
            _db = db;
            _jwtService = jwtService;
            _emailService = emailService;
            _config = config;
        }

        // ============================================================
        // REGISTER
        // ============================================================

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var emailExists = await _db.Users.AnyAsync(
                u => u.Email.ToLower() == dto.Email.ToLower()
                     && !u.IsDeleted);

            if (emailExists)
                throw new ConflictException("An account with this email already exists.");

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                FirstContact = dto.FirstContact,
                SecondContact = dto.SecondContact,
                HomeAddress = dto.HomeAddress,
                OfficeAddress = dto.OfficeAddress,
                WebsiteUrl = dto.WebsiteUrl,
                RoleId = null,
                ApprovalStatus = "Pending",
                IsActive = false,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = 0
            };

            user.Password = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            await StartSession(user, rememberMe: false);
            var (token, expiresAt) = _jwtService.GenerateToken(user, "No Role");

            return Ok(new AuthResponseDto
            {
                Token = token,
                ExpiresAt = expiresAt,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = "No Role",
                IsActive = user.IsActive
            });
        }

        // ============================================================
        // LOGIN
        // ============================================================

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(
                    u => u.Email.ToLower() == dto.Email.ToLower()
                         && !u.IsDeleted
                );

            if (user == null)
                throw new UnauthorizedAppException("Invalid email or password.");

            var result = _passwordHasher.VerifyHashedPassword(
                user, user.Password, dto.Password);

            if (result == PasswordVerificationResult.Failed)
                throw new UnauthorizedAppException("Invalid email or password.");

            if (user.ApprovalStatus == "Rejected")
                throw new UnauthorizedAppException("Your account registration has been rejected. Please contact an administrator.");

            // Pending users are allowed to login.
            // Their IsActive status is returned to frontend
            // so profile/dashboard can show verification status.

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.Password = _passwordHasher.HashPassword(user, dto.Password);
                await _db.SaveChangesAsync();
            }

            await StartSession(user, dto.RememberMe);
            var (token, expiresAt) = _jwtService.GenerateToken(user, user.Role?.Name ?? "No Role");

            return Ok(new AuthResponseDto
            {
                Token = token,
                ExpiresAt = expiresAt,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role?.Name ?? "No Role",
                IsActive = user.IsActive
            });
        }

        // ============================================================
        // FORGOT PASSWORD
        // ============================================================

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(
                u => u.Email.ToLower() == dto.Email.ToLower()
                     && !u.IsDeleted
            );

            // Always return 200. Do not reveal whether email exists.
            if (user == null)
            {
                return Ok(new
                {
                    message = "If that email exists, a reset link has been sent."
                });
            }

            var tokenBytes = RandomNumberGenerator.GetBytes(32);

            var token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false,
                IsActive = true,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = user.Id
            };

            _db.PasswordResetTokens.Add(resetToken);
            await _db.SaveChangesAsync();

            var frontendUrl = _config["FrontendSettings:BaseUrl"];

            var resetLink =
                $"{frontendUrl}/reset-password" +
                $"?token={Uri.EscapeDataString(token)}" +
                $"&email={Uri.EscapeDataString(user.Email)}";

            await _emailService.SendPasswordResetEmailAsync(
                user.Email, user.FirstName, resetLink);

            return Ok(new
            {
                message = "If that email exists, a reset link has been sent."
            });
        }

        // ============================================================
        // RESET PASSWORD
        // ============================================================

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(
                u => u.Email.ToLower() == dto.Email.ToLower()
                     && !u.IsDeleted
            );

            if (user == null)
                throw new BadRequestAppException("Invalid or expired reset link.");

            var resetToken = await _db.PasswordResetTokens
                .Where(t =>
                    t.UserId == user.Id &&
                    t.Token == dto.Token &&
                    !t.IsUsed
                )
                .OrderByDescending(t => t.CreatedDate)
                .FirstOrDefaultAsync();

            if (resetToken == null || resetToken.ExpiryDate < DateTime.UtcNow)
                throw new BadRequestAppException("Invalid or expired reset link.");

            user.Password = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.UpdatedDate = DateTime.UtcNow;
            user.UpdatedBy = user.Id;

            resetToken.IsUsed = true;
            resetToken.UpdatedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Password has been reset successfully. You can now log in."
            });
        }

        // ============================================================
        // CHANGE PASSWORD
        // ============================================================

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirst("userId")?.Value;

            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAppException("Invalid token.");

            var user = await _db.Users.FirstOrDefaultAsync(
                u => u.Id == userId && !u.IsDeleted);

            if (user == null)
                throw new UnauthorizedAppException("Invalid token.");

            var result = _passwordHasher.VerifyHashedPassword(
                user, user.Password, dto.OldPassword);

            if (result == PasswordVerificationResult.Failed)
                throw new BadRequestAppException("Current password is incorrect.");

            user.Password = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.UpdatedDate = DateTime.UtcNow;
            user.UpdatedBy = user.Id;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Password changed successfully."
            });
        }

        private async Task StartSession(User user, bool rememberMe)
        {
            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            var passwordVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.Password ?? "")));
            var expiresAt = DateTime.UtcNow.AddDays(rememberMe ? 30 : 1);

            try
            {
                _db.AuthSessions.Add(new AuthSession
                {
                    TokenHash = tokenHash,
                    UserId = user.Id,
                    PasswordVersion = passwordVersion,
                    ExpiresAt = expiresAt,
                    RememberMe = rememberMe
                });
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                foreach (var entry in _db.ChangeTracker.Entries<AuthSession>().ToList())
                    entry.State = EntityState.Detached;
            }

            Response.Cookies.Append(SessionCookie, raw, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = expiresAt,
                Path = "/"
            });
        }
    }
}
