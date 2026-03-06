// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Server.Configuration;
using CodeCliConnector.Storage;
using Microsoft.Extensions.Options;

namespace CodeCliConnector.Server.Services;

/// <summary>
/// 心跳监控后台服务.
/// </summary>
public sealed class HeartbeatMonitorService : BackgroundService
{
    private readonly ConnectorDataService _dataService;
    private readonly ServerSettings _settings;
    private readonly ILogger<HeartbeatMonitorService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeartbeatMonitorService"/> class.
    /// </summary>
    public HeartbeatMonitorService(
        ConnectorDataService dataService,
        IOptions<ServerSettings> settings,
        ILogger<HeartbeatMonitorService> logger)
    {
        _dataService = dataService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("心跳监控服务已启动，间隔: {Interval}s，超时: {Timeout}s",
            _settings.HeartbeatCheckIntervalSeconds,
            _settings.HeartbeatTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = await _dataService.SetTimeoutDevicesOfflineAsync(
                    _settings.HeartbeatTimeoutSeconds,
                    stoppingToken).ConfigureAwait(false);

                if (count > 0)
                {
                    _logger.LogInformation("已标记 {Count} 个超时设备为离线", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳监控检查失败");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_settings.HeartbeatCheckIntervalSeconds),
                stoppingToken).ConfigureAwait(false);
        }
    }
}
