using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Models;

namespace OMS_Backend.Common.ExceptionHandling;

public interface IExceptionRecorder
{
    Task RecordAsync(Exception exception, string source, HttpContext? context = null, string? operation = null, int? statusCode = null);
    Task<bool> StoreAsync(ExceptionLog entry);
}

// Uses its own SQL connection: a failed request transaction/DbContext must never
// roll back the diagnostic record, or save dirty business entities accidentally.
public sealed class ExceptionRecorder(IConfiguration configuration, IHostEnvironment environment, IHttpContextAccessor? accessor = null) : IExceptionRecorder
{
    private readonly ConditionalWeakTable<Exception, Marker> recorded = new();
    private sealed class Marker { public int Claimed; }
    private readonly SemaphoreSlim spoolLock = new(1, 1);
    private long databaseRetryAt;
    public string SpoolDirectory { get; } = Path.GetFullPath(configuration["ExceptionLogging:SpoolDirectory"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "exception-spool"));

    public async Task RecordAsync(Exception exception, string source, HttpContext? context = null, string? operation = null, int? statusCode = null)
    {
        context ??= accessor?.HttpContext;
        if (exception is OperationCanceledException && context?.RequestAborted.IsCancellationRequested == true) return;
        var marker = recorded.GetValue(exception, _ => new Marker());
        if (Interlocked.Exchange(ref marker.Claimed, 1) != 0) return;
        var app = exception as AppException;
        var entry = new ExceptionLog
        {
            Source = Limit(source, 100),
            TraceId = Limit(Activity.Current?.Id ?? context?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), 100),
            UserId = int.TryParse(context?.User.FindFirst("userId")?.Value, out var userId) ? userId : null,
            StatusCode = statusCode ?? app?.StatusCode ?? 500,
            ErrorCode = Limit(app?.ErrorCode ?? "UNEXPECTED_EXCEPTION", 100),
            Operation = Limit(operation ?? (context?.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "", 1000),
            ExceptionType = Limit(exception.GetType().FullName ?? "Exception", 500),
            // Exception messages can contain SQL values, SMTP credentials, tokens,
            // personal data and arbitrary upstream payloads. Do not persist them.
            Message = app is null ? $"Operation failed (HResult {exception.HResult}). See exception types and code locations." : $"Application error: {app.ErrorCode}",
            StackTrace = SafeStack(exception)
        };
        if (!await StoreAsync(entry)) Interlocked.Exchange(ref marker.Claimed, 0);
        if (context is not null) context.Items["ExceptionRecorded"] = true;
    }

    public static string SafeStack(Exception exception)
    {
        var parts = new List<string>();
        var pending = new Queue<Exception>();
        pending.Enqueue(exception);
        for (var depth = 0; pending.Count > 0 && depth < 8; depth++)
        {
            var current = pending.Dequeue();
            parts.Add(current.GetType().FullName ?? "Exception");
            foreach (var frame in new StackTrace(current, true).GetFrames().Take(60))
            {
                var method = frame.GetMethod();
                parts.Add($"  at {method?.DeclaringType?.FullName}.{method?.Name} (line {frame.GetFileLineNumber()})");
            }
            if (current is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions.Take(8)) pending.Enqueue(inner);
            else if (current.InnerException is { } inner) pending.Enqueue(inner);
        }
        return Limit(string.Join(Environment.NewLine, parts), 16000);
    }

    public async Task<bool> StoreAsync(ExceptionLog entry)
    {
        if (DateTime.UtcNow.Ticks >= Volatile.Read(ref databaseRetryAt) && await TryInsertAsync(entry)) return true;
        await spoolLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(SpoolDirectory);
            if (Directory.EnumerateFiles(SpoolDirectory, "*.json").Take(10000).Count() >= 10000)
                throw new IOException("Exception spool capacity reached.");
            var destination = Path.Combine(SpoolDirectory, $"{entry.Id:N}.json");
            var temporary = destination + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(entry));
            File.Move(temporary, destination, true);
            Console.Error.WriteLine($"Exception log {entry.Id}: database unavailable; queued for replay.");
            return true;
        }
        catch (Exception failure)
        {
            // Never recurse into ILogger or throw from the diagnostic path.
            Console.Error.WriteLine($"Exception logging unavailable ({failure.GetType().Name}). Safe event: {JsonSerializer.Serialize(entry)}");
            return false;
        }
        finally { spoolLock.Release(); }
    }

    public async Task<bool> TryInsertAsync(ExceptionLog entry)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync(timeout.Token);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 3;
            command.CommandText = """
                INSERT INTO ExceptionLogs (Id, OccurredAtUtc, Source, TraceId, UserId, StatusCode, ErrorCode, Operation, ExceptionType, Message, StackTrace)
                VALUES (@id,@at,@source,@trace,@user,@status,@code,@operation,@type,@message,@stack)
                """;
            command.Parameters.AddWithValue("@id", entry.Id);
            command.Parameters.AddWithValue("@at", entry.OccurredAtUtc);
            command.Parameters.AddWithValue("@source", Limit(entry.Source,100));
            command.Parameters.AddWithValue("@trace", Limit(entry.TraceId,100));
            command.Parameters.AddWithValue("@user", (object?)entry.UserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", (object?)entry.StatusCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@code", Limit(entry.ErrorCode,100));
            command.Parameters.AddWithValue("@operation", Limit(entry.Operation,1000));
            command.Parameters.AddWithValue("@type", Limit(entry.ExceptionType,500));
            command.Parameters.AddWithValue("@message", Limit(entry.Message,1000));
            command.Parameters.AddWithValue("@stack", Limit(entry.StackTrace,16000));
            await command.ExecuteNonQueryAsync(timeout.Token);
            Interlocked.Exchange(ref databaseRetryAt, 0);
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return true; } // replay after an ambiguous commit
        catch (Exception)
        {
            Interlocked.Exchange(ref databaseRetryAt, DateTime.UtcNow.AddSeconds(15).Ticks);
            return false;
        }
    }

    public async Task PruneAsync(CancellationToken cancellationToken)
    {
        var days = configuration.GetValue("ExceptionLogging:RetentionDays", 90);
        if (days <= 0) return; // Zero disables automatic retention.
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await connection.OpenAsync(timeout.Token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 3;
        command.CommandText = "DELETE TOP (1000) FROM ExceptionLogs WHERE OccurredAtUtc < @cutoff";
        command.Parameters.AddWithValue("@cutoff", DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 36500)));
        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    public static string Limit(string value, int length) => value.Length <= length ? value : value[..length];
}
