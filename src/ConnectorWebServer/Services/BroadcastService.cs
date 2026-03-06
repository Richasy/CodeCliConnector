// Copyright (c) Richasy. All rights reserved.

using System.Text.Json.Serialization.Metadata;
using CodeCliConnector.Core.Models;
using CodeCliConnector.Server.WebSocket;

namespace CodeCliConnector.Server.Services;

/// <summary>
/// 广播服务.
/// </summary>
public sealed class BroadcastService
{
    private readonly ConnectionManager _connectionManager;
    private readonly ILogger<BroadcastService> _logger;
    private readonly JsonTypeInfo<WebSocketMessage> _jsonTypeInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastService"/> class.
    /// </summary>
    public BroadcastService(
        ConnectionManager connectionManager,
        ILogger<BroadcastService> logger,
        JsonTypeInfo<WebSocketMessage> jsonTypeInfo)
    {
        _connectionManager = connectionManager;
        _logger = logger;
        _jsonTypeInfo = jsonTypeInfo;
    }

    /// <summary>
    /// 广播消息给所有在线终端（排除来源设备）.
    /// </summary>
    public async Task BroadcastAsync(
        WebSocketMessage message,
        string? excludeDeviceId = null,
        CancellationToken cancellationToken = default)
    {
        var connections = _connectionManager.GetAllConnections();
        var tasks = new List<Task>();

        foreach (var (deviceId, ws) in connections)
        {
            if (string.Equals(deviceId, excludeDeviceId, StringComparison.Ordinal))
            {
                continue;
            }

            tasks.Add(SendSafeAsync(deviceId, ws, message, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogDebug("广播消息 {MessageId} 到 {Count} 个设备", message.MessageId, tasks.Count);
    }

    /// <summary>
    /// 发送消息给指定设备.
    /// </summary>
    public async Task<bool> SendToDeviceAsync(
        string deviceId,
        WebSocketMessage message,
        CancellationToken cancellationToken = default)
    {
        var ws = _connectionManager.GetConnection(deviceId);
        if (ws is null)
        {
            return false;
        }

        await SendSafeAsync(deviceId, ws, message, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 发送"已处理"通知给其余终端.
    /// </summary>
    public async Task NotifyProcessedAsync(
        string correlationId,
        string processedByDeviceId,
        CancellationToken cancellationToken = default)
    {
        var notification = new WebSocketMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Type = Core.Models.Constants.MessageType.Response,
            SourceDeviceId = processedByDeviceId,
            Payload = $"{{\"correlationId\":\"{correlationId}\",\"status\":\"processed_by_other\"}}",
            CorrelationId = correlationId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        await BroadcastAsync(notification, excludeDeviceId: processedByDeviceId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task SendSafeAsync(
        string deviceId,
        System.Net.WebSockets.WebSocket ws,
        WebSocketMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await WebSocketSender.SendAsync(ws, message, _jsonTypeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送消息到设备 {DeviceId} 失败", deviceId);
        }
    }
}
