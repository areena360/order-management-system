using System.Diagnostics;
using OMS_Backend.Models;

namespace OMS_Backend.Common.ExceptionHandling;

// Returned error results (including auth/model-validation failures) are not thrown
// exceptions. Record status metadata without inspecting response/request bodies.
public sealed class ErrorResponseMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IExceptionRecorder recorder)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            // Includes failures after response headers were sent, for which the
            // framework cannot run its normal exception response handler.
            await recorder.RecordAsync(exception, "HttpPipeline", context);
            throw;
        }
        if (context.Response.StatusCode < 400 || context.Items.ContainsKey("ExceptionRecorded") ||
            context.RequestAborted.IsCancellationRequested || context.Request.Path.StartsWithSegments("/api/diagnostics")) return;
        await recorder.StoreAsync(new ExceptionLog
        {
            Source = "HttpResponse",
            TraceId = ExceptionRecorder.Limit(Activity.Current?.Id ?? context.TraceIdentifier, 100),
            UserId = int.TryParse(context.User.FindFirst("userId")?.Value, out var id) ? id : null,
            StatusCode = context.Response.StatusCode,
            ErrorCode = $"HTTP_{context.Response.StatusCode}",
            Operation = ExceptionRecorder.Limit((context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "Unmatched route", 1000),
            Message = "Request returned an error response."
        });
    }
}
