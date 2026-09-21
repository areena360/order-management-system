using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.Models;
namespace OMS_Backend.Services;
public class ShopifyWorker(IServiceScopeFactory scopes,IConfiguration config,ILogger<ShopifyWorker> logger):BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stop) {
        while(!stop.IsCancellationRequested) {
            if(config.GetValue<bool>("Shopify:Enabled")) try {await Tick();} catch(Exception ex){logger.LogWarning("Shopify worker paused: {Type}. Check configuration/migration and store logs.",ex.GetType().Name);}
            try{await Task.Delay(TimeSpan.FromSeconds(15),stop);}catch(OperationCanceledException){break;}
        }
    }
    private async Task Tick() {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<OMSDbContext>();var sync=scope.ServiceProvider.GetRequiredService<ShopifySyncService>();
        await db.Database.OpenConnectionAsync();
        try {
            // A session lock prevents overlapping workers without long database transactions.
            var acquired=await db.Database.SqlQueryRaw<int>("DECLARE @r int; EXEC @r=sp_getapplock @Resource='oms-shopify-worker',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r AS Value").ToListAsync();
            if(acquired[0]<0)return;
            try {
                var jobs=await db.Set<ShopifyJob>().Where(x=>x.CompletedAt==null&&x.DueAt<=DateTime.UtcNow).OrderBy(x=>x.Id).Select(x=>x.Id).Take(20).ToListAsync();
                foreach(var id in jobs) {
                    db.ChangeTracker.Clear();
                    var job=await db.Set<ShopifyJob>().Include(x=>x.Store).ThenInclude(x=>x.Connection).SingleAsync(x=>x.Id==id);
                    try {await sync.Process(job);job.CompletedAt=DateTime.UtcNow;job.Error="";}catch(Exception ex){
                        // Never persist entities left dirty by a rolled-back import.
                        db.ChangeTracker.Clear();job=await db.Set<ShopifyJob>().SingleAsync(x=>x.Id==id);
                        job.Attempts++;job.Error=SafeError(ex);job.DueAt=DateTime.UtcNow.AddSeconds(Math.Min(3600,30*Math.Pow(2,Math.Min(job.Attempts,7))));}
                    await db.SaveChangesAsync();
                }
                var storeIds=await db.Set<ShopifyStore>().Where(x=>x.Connection.IsActive&&x.NextPollAt<=DateTime.UtcNow).Select(x=>x.Id).ToListAsync();
                foreach(var id in storeIds) {
                    db.ChangeTracker.Clear();var store=await db.Set<ShopifyStore>().Include(x=>x.Connection).SingleAsync(x=>x.Id==id);
                    try{await sync.Scan(store);await sync.Fulfill(store);store.LastError="";}catch(Exception ex){db.ChangeTracker.Clear();store=await db.Set<ShopifyStore>().SingleAsync(x=>x.Id==id);store.LastError=SafeError(ex);store.NextPollAt=DateTime.UtcNow.AddMinutes(2);}
                    await db.SaveChangesAsync();
                }
            } finally {await db.Database.ExecuteSqlRawAsync("EXEC sp_releaseapplock @Resource='oms-shopify-worker',@LockOwner='Session'");}
        } finally {await db.Database.CloseConnectionAsync();}
    }
    private static string SafeError(Exception ex) => ex is InvalidOperationException && ex.Message.StartsWith("Shopify") ? ex.Message[..Math.Min(ex.Message.Length,300)] : ex is OMS_Backend.Common.Exceptions.AppException ? ex.Message[..Math.Min(ex.Message.Length,300)] : "Sync failed. Verify store permissions, protected customer data access and connection; automatic retry scheduled.";
}
