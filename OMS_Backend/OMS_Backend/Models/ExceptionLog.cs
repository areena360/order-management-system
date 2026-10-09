using System.ComponentModel.DataAnnotations;

namespace OMS_Backend.Models;

public sealed class ExceptionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string Source { get; set; } = "";
    [MaxLength(100)] public string TraceId { get; set; } = "";
    public int? UserId { get; set; }
    public int? StatusCode { get; set; }
    [MaxLength(100)] public string ErrorCode { get; set; } = "";
    [MaxLength(1000)] public string Operation { get; set; } = "";
    [MaxLength(500)] public string ExceptionType { get; set; } = "";
    [MaxLength(1000)] public string Message { get; set; } = "";
    [MaxLength(16000)] public string StackTrace { get; set; } = "";
}
