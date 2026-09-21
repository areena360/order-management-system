using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OMS_Backend.Common.ExceptionHandling;
using OMS_Backend.Data;
using OMS_Backend.Hubs;
using OMS_Backend.Services;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Private app credentials remain outside tracked appsettings files.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false).AddEnvironmentVariables();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// DbContext
builder.Services.AddDbContext<OMSDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<WooCommerceIntegrationService>();
builder.Services.AddDataProtection();
builder.Services.AddHttpClient("shopify", c => c.Timeout = TimeSpan.FromSeconds(25))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ShopifyApi>();
builder.Services.AddScoped<ShopifySyncService>();
builder.Services.AddHostedService<ShopifyWorker>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("woo", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 180, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});

// Chat services
builder.Services.AddScoped<IChatService, ChatService>();

// SignalR
builder.Services.AddSignalR();

// JWT Bearer authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.Zero,
    };

    // SignalR WebSocket cannot send Authorization header.
    // Read JWT from query string for /hubs paths.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthentication().AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, WooCommerceAuthenticationHandler>(WooCommerceSecurity.Scheme, _ => { });
builder.Services.AddAuthorization();

// CORS for Angular dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
        policy.WithOrigins(builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("WooCommerce:AllowLocalHttp")
                ? new[] { "http://localhost:4200", "http://localhost:4201" }
                : new[] { "http://localhost:4200" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Global exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

// Explicit loopback-only development exception; production still redirects to HTTPS.
app.UseWhen(context => !WooCommerceSecurity.TransportAllowed(context.Request, app.Configuration, app.Environment), branch => branch.UseHttpsRedirection());
app.UseStaticFiles();
app.UseCors("AngularClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SignalR hubs
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
