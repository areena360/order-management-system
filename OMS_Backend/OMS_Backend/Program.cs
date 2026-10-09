using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
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

try
{
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
builder.Services.AddSingleton<OrderTokens>();
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
builder.Services.AddSignalR(options => options.AddFilter<ExceptionHubFilter>());

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
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ExceptionRecorder>();
builder.Services.AddSingleton<IExceptionRecorder>(services => services.GetRequiredService<ExceptionRecorder>());
builder.Services.AddHostedService<ExceptionReplayWorker>();
builder.Services.AddRateLimiter(options => options.AddPolicy("client-errors", context =>
    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ErrorResponseMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Explicit loopback-only development exception; production still redirects to HTTPS.
app.UseWhen(context => !WooCommerceSecurity.TransportAllowed(context.Request, app.Configuration, app.Environment), branch => branch.UseHttpsRedirection());
var fileTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads/orders") || context.Request.Path.StartsWithSegments("/uploads/inventory-bills"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        var tokens = context.RequestServices.GetRequiredService<OrderTokens>();
        var db = context.RequestServices.GetRequiredService<OMSDbContext>();
        if (!await tokens.CanDownloadAsync(db, context.Request.Path.Value!, context.Request.Query["grant"]))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    await next(context);
});
fileTypes.Mappings[".download"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = fileTypes,
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/uploads/inventory-bills") && context.File.Name.EndsWith(".download"))
        {
            var name = context.File.Name[..^9];
            if (name.Length > 33) name = name[33..];
            context.Context.Response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(name)}";
            context.Context.Response.Headers.XContentTypeOptions = "nosniff";
        }
    }
});
app.UseCors("AngularClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SignalR hubs
app.MapHub<ChatHub>("/hubs/chat");

await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    await new ExceptionRecorder(builder.Configuration, builder.Environment)
        .RecordAsync(exception, "Host", operation: "StartupOrRun");
    throw;
}
