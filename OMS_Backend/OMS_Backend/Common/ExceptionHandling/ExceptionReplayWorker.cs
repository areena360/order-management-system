using System.Text.Json;
using OMS_Backend.Models;

namespace OMS_Backend.Common.ExceptionHandling;

public sealed class ExceptionReplayWorker(ExceptionRecorder recorder) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextPrune = DateTime.UtcNow.AddMinutes(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(recorder.SpoolDirectory))
                {
                    foreach (var path in Directory.EnumerateFiles(recorder.SpoolDirectory, "*.json").Take(100))
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        try
                        {
                            var entry = JsonSerializer.Deserialize<ExceptionLog>(await File.ReadAllTextAsync(path, stoppingToken));
                            if (entry is null) continue;
                            if (!await recorder.TryInsertAsync(entry)) break;
                            File.Delete(path);
                        }
                        catch (JsonException) { File.Move(path, path + ".invalid", true); }
                        catch (FileNotFoundException) { /* Another instance replayed it. */ }
                    }
                }
                if (DateTime.UtcNow >= nextPrune)
                {
                    nextPrune = DateTime.UtcNow.AddMinutes(10);
                    await recorder.PruneAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { Console.Error.WriteLine($"Exception replay unavailable ({ex.GetType().Name}). Pending files retained."); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
