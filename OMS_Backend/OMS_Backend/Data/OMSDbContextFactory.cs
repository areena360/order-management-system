using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OMS_Backend.Data;

// Migration commands must not start workers or trigger application startup logging.
public sealed class OMSDbContextFactory : IDesignTimeDbContextFactory<OMSDbContext>
{
    public OMSDbContext CreateDbContext(string[] args)
    {
        var directory = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(directory, "appsettings.json")))
            directory = Path.Combine(directory, "OMS_Backend", "OMS_Backend");
        var configuration = new ConfigurationBuilder().SetBasePath(directory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        return new OMSDbContext(new DbContextOptionsBuilder<OMSDbContext>()
            .UseSqlServer(configuration.GetConnectionString("DefaultConnection")).Options);
    }
}
