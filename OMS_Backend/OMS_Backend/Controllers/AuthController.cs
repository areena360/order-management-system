using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // Built-in ASP.NET Core Identity password hasher (PBKDF2 + salt) — no manual hashing
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

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

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var emailExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower() && !u.IsDeleted);
            if (emailExists)
                return Conflict(new { message = "An account with this email already exists." });

            // Default role: Customer (Id = 4, seeded)
            var customerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
            if (customerRole == null)
                return StatusCode(500, new { message = "Default role not configured." });

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                FirstContact = dto.FirstContact,
                RoleId = customerRole.Id,
                IsActive = true,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = 0, // self-registered
            };

            // Built-in Identity hasher — PBKDF2-HMAC-SHA256, 100k+ iterations, random salt
            user.Password = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var (token, expiresAt) = _jwtService.GenerateToken(user, customerRole.Name);

            return Ok(new AuthResponseDto
            {
                Token = token,
                ExpiresAt = expiresAt,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = customerRole.Name,
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower() && !u.IsDeleted);

            if (user == null || !user.IsActive)
                return Unauthorized(new { message = "Invalid email or password." });

            // Built-in Identity verification — handles PBKDF2 comparison + rehash-needed check
            var result = _passwordHasher.VerifyHashedPassword(user, user.Password, dto.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Invalid email or password." });

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.Password = _passwordHasher.HashPassword(user, dto.Password);
                await _db.SaveChangesAsync();
            }

            var (token, expiresAt) = _jwtService.GenerateToken(user, user.Role.Name);

            return Ok(new AuthResponseDto
            {
                Token = token,
                ExpiresAt = dto.RememberMe ? DateTime.UtcNow.AddDays(30) : expiresAt,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role.Name,
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower() && !u.IsDeleted);

            // Always return 200 — don't reveal whether an email exists
            if (user == null)
                return Ok(new { message = "If that email exists, a reset link has been sent." });

            // Built-in cryptographically secure random token generator
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false,
                IsActive = true,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = user.Id,
            };

            _db.PasswordResetTokens.Add(resetToken);
            await _db.SaveChangesAsync();

            var frontendUrl = _config["FrontendSettings:BaseUrl"];
            var resetLink = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";

            await _emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, resetLink);

            return Ok(new { message = "If that email exists, a reset link has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower() && !u.IsDeleted);
            if (user == null)
                return BadRequest(new { message = "Invalid or expired reset link." });

            var resetToken = await _db.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.Token == dto.Token && !t.IsUsed)
                .OrderByDescending(t => t.CreatedDate)
                .FirstOrDefaultAsync();

            if (resetToken == null || resetToken.ExpiryDate < DateTime.UtcNow)
                return BadRequest(new { message = "Invalid or expired reset link." });

            user.Password = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.UpdatedDate = DateTime.UtcNow;
            user.UpdatedBy = user.Id;

            resetToken.IsUsed = true;
            resetToken.UpdatedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Password has been reset successfully. You can now log in." });
        }
    }
}