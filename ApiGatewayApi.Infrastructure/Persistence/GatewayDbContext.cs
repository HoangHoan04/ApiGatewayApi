using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.Domain.Common;
using ApiGatewayApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiGatewayApi.Infrastructure.Persistence;

public class GatewayDbContext : DbContext, IGatewayDbContext
{
    public GatewayDbContext(DbContextOptions<GatewayDbContext> options) : base(options)
    {
    }

    public DbSet<GatewayService> GatewayServices => Set<GatewayService>();
    public DbSet<GatewayCluster> GatewayClusters => Set<GatewayCluster>();
    public DbSet<GatewayDestination> GatewayDestinations => Set<GatewayDestination>();
    public DbSet<GatewayRoute> GatewayRoutes => Set<GatewayRoute>();
    public DbSet<RateLimitPolicy> RateLimitPolicies => Set<RateLimitPolicy>();
    public DbSet<IpRule> IpRules => Set<IpRule>();
    public DbSet<CorsPolicy> CorsPolicies => Set<CorsPolicy>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
    public DbSet<GatewayAlertRule> AlertRules => Set<GatewayAlertRule>();
    public DbSet<MaintenanceWindow> MaintenanceWindows => Set<MaintenanceWindow>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IHasConcurrency).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IHasConcurrency.RowVersion))
                    .IsRowVersion();
            }
        }

        modelBuilder.Entity<GatewayService>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GatewayCluster>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GatewayDestination>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GatewayRoute>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RateLimitPolicy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<IpRule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CorsPolicy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GatewayAlertRule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<MaintenanceWindow>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<GatewayService>(b =>
        {
            b.ToTable("gateway_services");
            b.HasKey(e => e.Id);
            b.Property(e => e.Code).HasMaxLength(50).IsRequired();
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.Property(e => e.BaseUrl).HasMaxLength(500).IsRequired();
            b.Property(e => e.HealthPath).HasMaxLength(300);
            b.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<GatewayCluster>(b =>
        {
            b.ToTable("gateway_clusters");
            b.HasKey(e => e.Id);
            b.Property(e => e.ClusterId).HasMaxLength(80).IsRequired();
            b.Property(e => e.LoadBalancing).HasConversion<string>().HasMaxLength(40);
            b.HasIndex(e => e.ClusterId).IsUnique();
            b.HasOne(e => e.Service)
                .WithMany(s => s.Clusters)
                .HasForeignKey(e => e.ServiceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GatewayDestination>(b =>
        {
            b.ToTable("gateway_destinations");
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(80).IsRequired();
            b.Property(e => e.Address).HasMaxLength(500).IsRequired();
            b.Property(e => e.HealthStatus).HasConversion<string>().HasMaxLength(30);
            b.HasOne(e => e.Cluster)
                .WithMany(c => c.Destinations)
                .HasForeignKey(e => e.ClusterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GatewayRoute>(b =>
        {
            b.ToTable("gateway_routes");
            b.HasKey(e => e.Id);
            b.Property(e => e.RouteId).HasMaxLength(80).IsRequired();
            b.Property(e => e.PathMatch).HasMaxLength(500).IsRequired();
            b.Property(e => e.AuthorizationPolicy).HasMaxLength(50);
            b.Property(e => e.MethodsJson).HasColumnType("jsonb");
            b.Property(e => e.TransformsJson).HasColumnType("jsonb");
            b.HasIndex(e => e.RouteId).IsUnique();
            b.HasOne(e => e.Cluster)
                .WithMany(c => c.Routes)
                .HasForeignKey(e => e.ClusterId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.RateLimitPolicy)
                .WithMany(p => p.Routes)
                .HasForeignKey(e => e.RateLimitPolicyId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(e => e.CorsPolicy)
                .WithMany(p => p.Routes)
                .HasForeignKey(e => e.CorsPolicyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RateLimitPolicy>(b =>
        {
            b.ToTable("rate_limit_policies");
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(120).IsRequired();
            b.Property(e => e.KeyType).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<IpRule>(b =>
        {
            b.ToTable("ip_rules");
            b.HasKey(e => e.Id);
            b.Property(e => e.Cidr).HasMaxLength(64).IsRequired();
            b.Property(e => e.Action).HasConversion<string>().HasMaxLength(20);
            b.HasOne(e => e.Route)
                .WithMany()
                .HasForeignKey(e => e.RouteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CorsPolicy>(b =>
        {
            b.ToTable("cors_policies");
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(120).IsRequired();
            b.Property(e => e.AllowedOriginsJson).HasColumnType("jsonb");
            b.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<RequestLog>(b =>
        {
            b.ToTable("request_logs");
            b.HasKey(e => e.Id);
            b.Property(e => e.CorrelationId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Method).HasMaxLength(10).IsRequired();
            b.Property(e => e.Path).HasMaxLength(1000).IsRequired();
            b.Property(e => e.TargetCluster).HasMaxLength(80);
            b.Property(e => e.ClientIp).HasMaxLength(45);
            b.HasIndex(e => e.CreatedAt);
            b.HasIndex(e => e.CorrelationId);
            b.HasIndex(e => e.TargetCluster);
        });

        modelBuilder.Entity<GatewayAlertRule>(b =>
        {
            b.ToTable("gateway_alert_rules");
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(150).IsRequired();
            b.Property(e => e.Metric).HasConversion<string>().HasMaxLength(40);
            b.Property(e => e.Threshold).HasPrecision(18, 4);
            b.HasOne(e => e.Service)
                .WithMany()
                .HasForeignKey(e => e.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Route)
                .WithMany()
                .HasForeignKey(e => e.RouteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaintenanceWindow>(b =>
        {
            b.ToTable("maintenance_windows");
            b.HasKey(e => e.Id);
            b.Property(e => e.Message).HasMaxLength(500);
            b.HasIndex(e => e.ServiceId);
            b.HasOne(e => e.Service)
                .WithMany()
                .HasForeignKey(e => e.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
