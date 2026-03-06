// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Server.Configuration;
using CodeCliConnector.Storage;
using Microsoft.Extensions.Options;

namespace CodeCliConnector.Server.Services;

/// <summary>
/// 消息清理后台服务.
/// </summary>
public sealed class MessageCleanupService : BackgroundService
{
    private readonly ConnectorDataService _dataService;
    private readonly ServerSettings _settings;
    private readonly ILogger<MessageCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageCleanupService"/> class.
    /// </summary>
    public MessageCleanupService(
        ConnectorDataService dataService,
        IOptions<ServerSettings> settings,
        ILogger<MessageCleanupService> logger)
    {
        _dataService = dataService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("消息清理服务已启动，间隔: {Interval}s", _settings.MessageCleanupIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 标记过期消息
                var expired = await _dataService.ExpireMessagesAsync(stoppingToken).ConfigureAwait(false);
                if (expired > 0)
                {
                    _logger.LogInformation("已标记 {Count} 条过期消息", expired);
                }

                // 清理旧消息
                var cleaned = await _dataService.CleanupOldMessagesAsync(
                    _settings.MessageCleanupDays,
                    stoppingToken).ConfigureAwait(false);

                if (cleaned > 0)
                {
                    _logger.LogInformation("已清理 {Count} 条旧消息", cleaned);
                }

                // 清理过期令牌
                var tokens = await _dataService.CleanupExpiredTokensAsync(stoppingToken).ConfigureAwait(false);
                if (tokens > 0)
                {
                    _logger.LogInformation("已清理 {Count} 个过期令牌", tokens);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "消息清理失败");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_settings.MessageCleanupIntervalSeconds),
                stoppingToken).ConfigureAwait(false);
        }
    }
}
