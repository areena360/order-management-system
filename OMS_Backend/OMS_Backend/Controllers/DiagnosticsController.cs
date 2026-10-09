using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OMS_Backend.Common.ExceptionHandling;
using OMS_Backend.Models;

namespace OMS_Backend.Controllers;

[ApiController, Route("api/diagnostics/client-errors")]
public sealed class DiagnosticsController(IExceptionRecorder recorder) : ControllerBase
{
    public sealed class ClientError
    {
        [Required, RegularExpression("^(Runtime|Promise|Bootstrap|Network|SignalR|Handled)$")]
        public string Kind { get; set; } = "Runtime";
        [MaxLength(4000)] public string Locations { get; set; } = "";
    }

    [HttpPost, AllowAnonymous, EnableRateLimiting("client-errors"), RequestSizeLimit(8192)]
    public async Task<IActionResult> Report(ClientError input)
    {
        // Treat client reports as untrusted. Only retain asset line/column locations;
        // never persist arbitrary messages, URLs, page content or browser stacks.
        var locations = Regex.Matches(input.Locations ?? "", @"(?m)^(?:main|polyfills|scripts|chunk)(?:-[A-Za-z0-9]+)?\.js:\d{1,8}:\d{1,8}$")
            .Select(x => x.Value).Take(20);
        var saved = await recorder.StoreAsync(new ExceptionLog
        {
            Source = "Frontend", ErrorCode = "CLIENT_" + input.Kind.ToUpperInvariant(),
            ExceptionType = "ClientReportedError", Message = "Client-reported " + input.Kind + " failure.",
            TraceId = HttpContext.TraceIdentifier,
            UserId = int.TryParse(User.FindFirst("userId")?.Value, out var id) ? id : null,
            StackTrace = string.Join('\n', locations)
        });
        return saved ? Accepted() : StatusCode(503);
    }
}
