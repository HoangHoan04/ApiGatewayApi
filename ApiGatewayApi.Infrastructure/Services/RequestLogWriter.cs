using System.Threading.Channels;
using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.Domain.Entities;
using ApiGatewayApi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiGatewayApi.Infrastructure.Services;

public sealed class RequestLogWriter : BackgroundService, IRequestLogWriter
{
    private readonly Channel<RequestLogWrite> _channel = Channel.CreateBounded<RequestLogWrite>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RequestLogWriter> _logger;

    public RequestLogWriter(IServiceScopeFactory scopeFactory, ILogger<RequestLogWriter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(RequestLogWrite item) => _channel.Writer.TryWrite(item);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<RequestLogWrite>(100);
        while (!stoppingToken.IsCancellationRequested)
        {
            batch.Clear();
            try
            {
                var first = await _channel.Reader.ReadAsync(stoppingToken);
                batch.Add(first);
                while (batch.Count < 100 && _channel.Reader.TryRead(out var next))
                {
                    batch.Add(next);
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
                db.RequestLogs.AddRange(batch.Select(x => new RequestLog
                {
                    Id = Guid.NewGuid(),
                    CorrelationId = x.CorrelationId,
                    Method = x.Method,
                    Path = x.Path.Length > 1000 ? x.Path[..1000] : x.Path,
                    QueryString = x.QueryString,
                    StatusCode = x.StatusCode,
                    DurationMs = x.DurationMs,
                    TargetCluster = x.TargetCluster,
                    ClientIp = x.ClientIp,
                    UserId = x.UserId,
                    CompanyId = x.CompanyId,
                    RequestSize = x.RequestSize,
                    CreatedAt = DateTimeOffset.UtcNow
                }));
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist request logs.");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
