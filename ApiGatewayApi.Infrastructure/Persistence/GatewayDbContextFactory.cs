using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApiGatewayApi.Infrastructure.Persistence;

public class GatewayDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=GatewayDb;Username=postgres;Password=root;Encoding=UTF8";

        var optionsBuilder = new DbContextOptionsBuilder<GatewayDbContext>();
        optionsBuilder.UseNpgsql(connectionString, b =>
            b.MigrationsAssembly(typeof(GatewayDbContext).Assembly.FullName));
        optionsBuilder.ConfigureWarnings(w => w.Ignore(
            RelationalEventId.PendingModelChangesWarning,
            CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));

        return new GatewayDbContext(optionsBuilder.Options);
    }
}
