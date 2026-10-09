using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OMS_Backend.Common.ExceptionHandling;
using OMS_Backend.Controllers;
using OMS_Backend.Models;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
var directory = Path.Combine(Path.GetTempPath(), "oms-exception-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var env = new TestEnvironment { ContentRootPath = directory };
var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = "", // fail immediately; no external DB dependency
    ["ExceptionLogging:SpoolDirectory"] = directory
}).Build();
var recorder = new ExceptionRecorder(settings, env);
Exception error;
try { throw new InvalidOperationException("password=secret; customer@example.com", new Exception("Bearer private-token")); }
catch (Exception ex) { error = ex; }
var context = new DefaultHttpContext();
context.TraceIdentifier = "exception-smoke-" + Guid.NewGuid().ToString("N");
context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "17")], "test"));
context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("api/orders/{id}"), 0, EndpointMetadataCollection.Empty, "orders"));
context.Request.QueryString = new QueryString("?token=private-token");
context.Request.Path = "/api/orders/customer@example.com";
try
{
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => recorder.RecordAsync(error, "Test", context)));
    var files = Directory.GetFiles(directory, "*.json");
    Check(files.Length == 1, "concurrent reports of the same exception produce one durable event");
    var json = await File.ReadAllTextAsync(files[0]);
    var entry = JsonSerializer.Deserialize<ExceptionLog>(json)!;
    Check(!json.Contains("private-token") && !json.Contains("customer@example.com") && !json.Contains("password=secret"), "messages, inner messages, URL values and query tokens excluded");
    Check(entry.UserId == 17 && entry.TraceId == context.TraceIdentifier && entry.Operation == "api/orders/{id}", "user, trace and route template captured");
    Check(entry.StackTrace.Contains("InvalidOperationException") && entry.StackTrace.Contains("System.Exception"), "outer and inner exception code locations preserved");

    var cancelled = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
    await recorder.RecordAsync(new OperationCanceledException(), "Test", cancelled);
    Check(Directory.GetFiles(directory, "*.json").Length == 1, "expected request cancellation is not an error");

    var capture = new CapturingRecorder();
    var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, env, capture);
    var response = new DefaultHttpContext(); response.Response.Body = new MemoryStream();
    await handler.TryHandleAsync(response, error, CancellationToken.None);
    response.Response.Body.Position = 0;
    var body = await new StreamReader(response.Response.Body).ReadToEndAsync();
    Check(response.Response.StatusCode == 500 && !body.Contains("private-token") && !body.Contains("password=secret"), "production response does not disclose exception details");
    Check(capture.Exceptions == 1 && response.Items.ContainsKey("ExceptionRecorded"), "global handler records before marking the response");
    await new ErrorResponseMiddleware(_ => Task.CompletedTask).InvokeAsync(response, capture);
    Check(capture.Entries.Count == 0, "status middleware does not duplicate global exceptions");
    var forbidden = new DefaultHttpContext();
    await new ErrorResponseMiddleware(c => { c.Response.StatusCode = 403; return Task.CompletedTask; }).InvokeAsync(forbidden, capture);
    Check(capture.Entries.Single().StatusCode == 403, "returned authorization errors are captured");

    var diagnostics = new DiagnosticsController(capture) { ControllerContext = new ControllerContext { HttpContext = context } };
    await diagnostics.Report(new DiagnosticsController.ClientError { Kind = "Runtime", Locations = "Bearer private-token\nmain-ABC.js:123:45\nhttps://private.example/token\ncustomer@example.com" });
    Check(capture.Entries.Last().StackTrace == "main-ABC.js:123:45", "untrusted browser report only retains allowed asset positions");

    var caller = new TestCaller();
    var hubContext = new HubInvocationContext(caller, null!, new TestHub(), typeof(TestHub).GetMethod(nameof(TestHub.Fail))!, []);
    try { await new ExceptionHubFilter(capture).InvokeMethodAsync(hubContext, _ => throw error); }
    catch (InvalidOperationException) { }
    Check(capture.Exceptions == 2, "SignalR invocation failures recorded and propagated");
    try { await new ErrorResponseMiddleware(_ => throw error).InvokeAsync(new DefaultHttpContext(), capture); }
    catch (InvalidOperationException) { }
    Check(capture.Exceptions == 3, "pipeline failures outside normal exception responses are captured and propagated");
    var timedOut = new DefaultHttpContext(); timedOut.Response.Body = new MemoryStream();
    await handler.TryHandleAsync(timedOut, new TimeoutException("private-token"), CancellationToken.None);
    Check(timedOut.Response.StatusCode == 504, "timeouts return a gateway timeout rather than leaking details");

    // A broken spool must not replace the application's original error.
    var blockedPath = Path.Combine(directory, "not-a-directory");
    await File.WriteAllTextAsync(blockedPath, "test");
    var blockedSettings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    { ["ExceptionLogging:SpoolDirectory"] = blockedPath }).Build();
    Check(!await new ExceptionRecorder(blockedSettings, env).StoreAsync(new ExceptionLog { Source = "Test" }), "DB plus disk failure reports failure without throwing");
    File.Delete(blockedPath);

    if (args.Contains("--database"))
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../OMS_Backend/OMS_Backend"));
        var liveConfig = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Local.json", true).AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ExceptionLogging:SpoolDirectory"] = directory }).Build();
        var live = new ExceptionRecorder(liveConfig, env);
        await using var connection = new SqlConnection(liveConfig.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();
        // Leave an unrelated business transaction open; diagnostics use a different connection.
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            Check(await live.TryInsertAsync(entry), "database insert succeeds independently of caller transaction");
            await transaction.RollbackAsync();
        }
        Check(await live.TryInsertAsync(entry), "replayed event ID is idempotent");
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM ExceptionLogs WHERE Id=@id AND TraceId=@trace";
            command.Parameters.AddWithValue("@id", entry.Id); command.Parameters.AddWithValue("@trace", entry.TraceId);
            Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "one durable SQL row remains after caller rollback and duplicate retry");
        }
        using (var worker = new ExceptionReplayWorker(live))
        {
            await worker.StartAsync(CancellationToken.None);
            for (var i = 0; i < 50 && File.Exists(files[0]); i++) await Task.Delay(100);
            await worker.StopAsync(CancellationToken.None);
            Check(!File.Exists(files[0]), "replay worker removes spool file only after SQL accepts it");
        }
        // Only delete the synthetic event inserted by this run.
        using var cleanup = connection.CreateCommand();
        cleanup.CommandText = "DELETE FROM ExceptionLogs WHERE Id=@id AND TraceId=@trace";
        cleanup.Parameters.AddWithValue("@id", entry.Id); cleanup.Parameters.AddWithValue("@trace", entry.TraceId);
        await cleanup.ExecuteNonQueryAsync();
    }
    Console.WriteLine($"{passed} exception logging checks passed.");
}
finally
{
    // The directory is created by this run under the OS temp root, with an unguessable ID.
    if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Unexpected test directory");
    foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
    Directory.Delete(directory);
}

sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "ExceptionTests";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
sealed class CapturingRecorder : IExceptionRecorder
{
    public int Exceptions;
    public List<ExceptionLog> Entries { get; } = [];
    public Task RecordAsync(Exception exception, string source, HttpContext? context = null, string? operation = null, int? statusCode = null)
    { Exceptions++; return Task.CompletedTask; }
    public Task<bool> StoreAsync(ExceptionLog entry) { Entries.Add(entry); return Task.FromResult(true); }
}
sealed class TestHub : Hub { public void Fail() => throw new InvalidOperationException(); }
sealed class TestCaller : HubCallerContext
{
    public override string ConnectionId => "test";
    public override string? UserIdentifier => null;
    public override ClaimsPrincipal? User => null;
    public override IDictionary<object,object?> Items { get; } = new Dictionary<object,object?>();
    public override Microsoft.AspNetCore.Http.Features.IFeatureCollection Features { get; } = new Microsoft.AspNetCore.Http.Features.FeatureCollection();
    public override CancellationToken ConnectionAborted => CancellationToken.None;
    public override void Abort() { }
}
