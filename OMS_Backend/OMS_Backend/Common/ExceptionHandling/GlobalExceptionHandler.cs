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
        private readonly IExceptionRecorder _recorder;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env, IExceptionRecorder recorder)
        {
            _logger = logger;
            _env = env;
            _recorder = recorder;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
                return true;
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            var (statusCode, title, errorCode, errors) = exception switch
            {
                AppException appEx => (appEx.StatusCode, appEx.Message, appEx.ErrorCode, appEx.Errors),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized.", "UNAUTHORIZED", null),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict.", "CONFLICT", null),
                DbUpdateException => (StatusCodes.Status400BadRequest, "Database update failed. Check related data or constraints.", "DB_UPDATE_ERROR", null),
                BadHttpRequestException badRequest => (badRequest.StatusCode, "Invalid HTTP request.", "BAD_HTTP_REQUEST", null),
                HttpRequestException => (StatusCodes.Status502BadGateway, "An upstream service is unavailable.", "UPSTREAM_ERROR", null),
                TimeoutException or OperationCanceledException => (StatusCodes.Status504GatewayTimeout, "The operation timed out.", "TIMEOUT", null),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "INTERNAL_ERROR", null)
            };

            await _recorder.RecordAsync(exception, "Api", httpContext, statusCode: statusCode);
            httpContext.Items["ExceptionRecorded"] = true;
            // Keep operational console signals free of raw exception messages.
            if (statusCode >= 500)
                _logger.LogError("Unhandled exception. TraceId: {TraceId}", traceId);
            else
                _logger.LogWarning("Handled exception ({ErrorCode}). TraceId: {TraceId}", errorCode, traceId);

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
