using Microsoft.AspNetCore.SignalR;

namespace OMS_Backend.Common.ExceptionHandling;

public sealed class ExceptionHubFilter(IExceptionRecorder recorder) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try { return await next(context); }
        catch (Exception ex)
        {
            await recorder.RecordAsync(ex, "SignalR", context.Context.GetHttpContext(), context.HubMethodName);
            throw;
        }
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            await recorder.RecordAsync(ex, "SignalR", context.Context.GetHttpContext(), "OnConnected");
            throw;
        }
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        if (exception is not null)
            await recorder.RecordAsync(exception, "SignalR", context.Context.GetHttpContext(), "Disconnected");
        try { await next(context, exception); }
        catch (Exception ex)
        {
            await recorder.RecordAsync(ex, "SignalR", context.Context.GetHttpContext(), "OnDisconnected");
            throw;
        }
    }
}
