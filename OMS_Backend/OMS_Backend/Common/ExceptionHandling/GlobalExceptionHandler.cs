using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using System.Diagnostics;

namespace OMS_Backend.Common.ExceptionHandling
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _env;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            var (statusCode, title, errorCode, errors) = exception switch
            {
                AppException appEx => (appEx.StatusCode, appEx.Message, appEx.ErrorCode, appEx.Errors),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized.", "UNAUTHORIZED", null),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict.", "CONFLICT", null),
                DbUpdateException => (StatusCodes.Status400BadRequest, "Database update failed. Check related data or constraints.", "DB_UPDATE_ERROR", null),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "INTERNAL_ERROR", null)
            };

            // Log: full detail always server-side (5xx as Error, 4xx as Warning)
            if (statusCode >= 500)
                _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
            else
                _logger.LogWarning(exception, "Handled exception ({ErrorCode}). TraceId: {TraceId}", errorCode, traceId);

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Type = $"https://httpstatuses.io/{statusCode}",
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = traceId,
                    ["errorCode"] = errorCode,
                    ["errors"] = errors,
                    // Stack trace only in Development — never leak in Production
                    ["detail"] = _env.IsDevelopment() ? exception.ToString() : null
                }
            };

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true; // exception handled, don't propagate
        }
    }
}