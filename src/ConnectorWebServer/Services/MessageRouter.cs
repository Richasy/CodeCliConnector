// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using CodeCliConnector.Server.Configuration;
using CodeCliConnector.Server.WebSocket;
using CodeCliConnector.Storage;
using Microsoft.Extensions.Options;

namespace CodeCliConnector.Server.Services;

/// <summary>
/// 消息路由器.
/// </summary>
public sealed class MessageRouter
{
    private readonly BroadcastService _broadcastService;
    private readonly ResponseTracker _responseTracker;
    private readonly ConnectorDataService _dataService;
    private readonly ConnectionManager _connectionManager;
    private readonly ServerSettings _settings;
    private readonly ILogger<MessageRouter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRouter"/> class.
    /// </summary>
    public MessageRouter(
        BroadcastService broadcastService,
        ResponseTracker responseTracker,
        ConnectorDataService dataService,
        ConnectionManager connectionManager,
        IOptions<ServerSettings> settings,
        ILogger<MessageRouter> logger)
    {
        _broadcastService = broadcastService;
        _responseTracker = responseTracker;
        _dataService = dataService;
        _connectionManager = connectionManager;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// 路由消息.
    /// </summary>
    public async Task RouteAsync(WebSocketMessage message, CancellationToken cancellationToken = default)
    {
        switch (message.Type)
        {
            case MessageType.Notification:
                await RouteNotificationAsync(message, cancellationToken).ConfigureAwait(false);
                break;

            case MessageType.Command:
                await RouteCommandAsync(message, cancellationToken).ConfigureAwait(false);
                break;

            case MessageType.Response:
                await RouteResponseAsync(message, cancellationToken).ConfigureAwait(false);
                break;

            default:
                _logger.LogWarning("未知消息类型: {Type}", message.Type);
                break;
        }
    }

    private async Task RouteNotificationAsync(WebSocketMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("路由通知消息 {MessageId} 来自 {Source}", message.MessageId, message.SourceDeviceId);

        // 广播给所有在线终端（排除来源）
        await _broadcastService.BroadcastAsync(message, message.SourceDeviceId, cancellationToken).ConfigureAwait(false);

        // 保存到数据库供离线设备读取
        await SaveForOfflineDevicesAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task RouteCommandAsync(WebSocketMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("路由命令消息 {MessageId} 来自 {Source} 到 {Target}", message.MessageId, message.SourceDeviceId, message.TargetDeviceId);

        if (!string.IsNullOrEmpty(message.TargetDeviceId))
        {
            // 定向发送
            var sent = await _broadcastService.SendToDeviceAsync(message.TargetDeviceId, message, cancellationToken).ConfigureAwait(false);
            if (!sent)
            {
                // 目标离线，存储待投递消息
                await SavePendingMessageAsync(message, message.TargetDeviceId, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // 广播命令
            await _broadcastService.BroadcastAsync(message, message.SourceDeviceId, cancellationToken).ConfigureAwait(false);
            await SaveForOfflineDevicesAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RouteResponseAsync(WebSocketMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.CorrelationId))
        {
            _logger.LogWarning("响应消息 {MessageId} 缺少 CorrelationId", message.MessageId);
            return;
        }

        // 尝试认领响应
        if (!_responseTracker.TryClaimResponse(message.CorrelationId))
        {
            _logger.LogDebug("响应 {CorrelationId} 已被其他设备认领", message.CorrelationId);
            return;
        }

        _logger.LogInformation("设备 {DeviceId} 认领响应 {CorrelationId}", message.SourceDeviceId, message.CorrelationId);

        // 标记原始消息及所有相关 pending 消息为已处理（避免离线设备上线后收到过时请求）
        await _dataService.MarkMessageProcessedAsync(message.CorrelationId, cancellationToken).ConfigureAwait(false);
        await _dataService.MarkPendingMessagesByCorrelationIdAsync(message.CorrelationId, cancellationToken).ConfigureAwait(false);

        // 将响应转发给目标设备（通常是 ClaudeCode 实例）
        if (!string.IsNullOrEmpty(message.TargetDeviceId))
        {
            await _broadcastService.SendToDeviceAsync(message.TargetDeviceId, message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // 广播响应
            await _broadcastService.BroadcastAsync(message, message.SourceDeviceId, cancellationToken).ConfigureAwait(false);
        }

        // 通知其余终端此消息已被处理
        await _broadcastService.NotifyProcessedAsync(message.CorrelationId, message.SourceDeviceId ?? string.Empty, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveForOfflineDevicesAsync(WebSocketMessage message, CancellationToken cancellationToken)
    {
        var allDevices = await _dataService.GetAllDevicesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var device in allDevices)
        {
            if (string.Equals(device.DeviceId, message.SourceDeviceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (_connectionManager.IsOnline(device.DeviceId))
            {
                continue;
            }

            await SavePendingMessageAsync(message, device.DeviceId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SavePendingMessageAsync(WebSocketMessage message, string targetDeviceId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var msgInfo = new MessageInfo
        {
            MessageId = $"{message.MessageId}_{targetDeviceId}",
            SourceDeviceId = message.SourceDeviceId ?? string.Empty,
            TargetDeviceId = targetDeviceId,
            MessageType = message.Type,
            Status = MessageStatus.Pending,
            Payload = message.Payload,
            CreatedAt = now,
            ExpiresAt = now + _settings.MessageExpirySeconds,
            CorrelationId = message.CorrelationId,
        };

        await _dataService.CreateMessageAsync(msgInfo, cancellationToken).ConfigureAwait(false);
    }
}
