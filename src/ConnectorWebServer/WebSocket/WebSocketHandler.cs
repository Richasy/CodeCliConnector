// Copyright (c) Richasy. All rights reserved.

using System.Text.Json.Serialization.Metadata;
using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using CodeCliConnector.Server.Services;
using CodeCliConnector.Storage;

namespace CodeCliConnector.Server.WebSocket;

/// <summary>
/// WebSocket 连接处理器.
/// </summary>
public sealed class WebSocketHandler
{
    private readonly ConnectionManager _connectionManager;
    private readonly ConnectorDataService _dataService;
    private readonly MessageRouter _messageRouter;
    private readonly ResponseTracker _responseTracker;
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly JsonTypeInfo<WebSocketMessage> _jsonTypeInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketHandler"/> class.
    /// </summary>
    public WebSocketHandler(
        ConnectionManager connectionManager,
        ConnectorDataService dataService,
        MessageRouter messageRouter,
        ResponseTracker responseTracker,
        ILogger<WebSocketHandler> logger,
        JsonTypeInfo<WebSocketMessage> jsonTypeInfo)
    {
        _connectionManager = connectionManager;
        _dataService = dataService;
        _messageRouter = messageRouter;
        _responseTracker = responseTracker;
        _logger = logger;
        _jsonTypeInfo = jsonTypeInfo;
    }

    /// <summary>
    /// 处理 WebSocket 连接.
    /// </summary>
    public async Task HandleAsync(HttpContext context)
    {
        var deviceId = context.Items["DeviceId"]?.ToString();
        if (string.IsNullOrEmpty(deviceId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

        _connectionManager.AddConnection(deviceId, ws);
        await _dataService.UpdateHeartbeatAsync(deviceId, context.RequestAborted).ConfigureAwait(false);
        _logger.LogInformation("设备已连接: {DeviceId}", deviceId);

        // 投递离线期间的待处理消息
        await DeliverPendingMessagesAsync(deviceId, ws, context.RequestAborted).ConfigureAwait(false);

        try
        {
            await ReceiveLoopAsync(deviceId, ws, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            _connectionManager.RemoveConnection(deviceId);
            await _dataService.SetDeviceOfflineAsync(deviceId).ConfigureAwait(false);
            _logger.LogInformation("设备已断开: {DeviceId}", deviceId);
        }
    }

    private async Task ReceiveLoopAsync(
        string deviceId,
        System.Net.WebSockets.WebSocket ws,
        CancellationToken cancellationToken)
    {
        while (ws.State == System.Net.WebSockets.WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            WebSocketMessage? message;
            try
            {
                message = await WebSocketSender.ReceiveAsync(ws, _jsonTypeInfo, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "设备 {DeviceId} 发送了无效的 JSON 消息", deviceId);
                continue;
            }

            if (message is null)
            {
                break;
            }

            message.SourceDeviceId = deviceId;
            message.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (message.Type == MessageType.Heartbeat)
            {
                await _dataService.UpdateHeartbeatAsync(deviceId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await _messageRouter.RouteAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DeliverPendingMessagesAsync(
        string deviceId,
        System.Net.WebSockets.WebSocket ws,
        CancellationToken cancellationToken)
    {
        var pendingMessages = await _dataService.GetPendingMessagesAsync(deviceId, cancellationToken).ConfigureAwait(false);

        foreach (var msg in pendingMessages)
        {
            // 跳过已被其他设备处理（认领响应）的消息，避免投递过时的权限请求
            if (!string.IsNullOrEmpty(msg.CorrelationId) && _responseTracker.IsClaimed(msg.CorrelationId))
            {
                await _dataService.MarkMessageProcessedAsync(msg.MessageId, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("跳过已处理的离线消息: {MessageId}, CorrelationId={CorrelationId}", msg.MessageId, msg.CorrelationId);
                continue;
            }

            var wsMessage = new WebSocketMessage
            {
                MessageId = msg.MessageId,
                Type = msg.MessageType,
                SourceDeviceId = msg.SourceDeviceId,
                TargetDeviceId = msg.TargetDeviceId,
                Payload = msg.Payload,
                CorrelationId = msg.CorrelationId,
                Timestamp = msg.CreatedAt,
            };

            await WebSocketSender.SendAsync(ws, wsMessage, _jsonTypeInfo, cancellationToken).ConfigureAwait(false);
            await _dataService.MarkMessageDeliveredAsync(msg.MessageId, cancellationToken).ConfigureAwait(false);
        }

        if (pendingMessages.Count > 0)
        {
            _logger.LogInformation("已向设备 {DeviceId} 投递 {Count} 条离线消息", deviceId, pendingMessages.Count);
        }
    }
}
