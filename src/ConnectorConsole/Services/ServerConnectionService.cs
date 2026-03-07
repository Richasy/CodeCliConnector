// Copyright (c) Richasy. All rights reserved.

using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using CodeCliConnector.Core.Models.Requests;
using CodeCliConnector.Core.Models.Responses;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Console.Services;

/// <summary>
/// 服务器连接服务，管理 WebSocket 连接、心跳和消息收发.
/// </summary>
internal sealed class ServerConnectionService : IAsyncDisposable
{
    private const int HeartbeatIntervalSeconds = 10;
    private const int MaxReconnectDelaySeconds = 60;
    private const int ReceiveBufferSize = 8192;

    private readonly ConfigService _configService;
    private readonly PendingRequestTracker _requestTracker;
    private readonly ILogger<ServerConnectionService> _logger;
    private readonly HttpClient _httpClient;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerConnectionService"/> class.
    /// </summary>
    public ServerConnectionService(
        ConfigService configService,
        PendingRequestTracker requestTracker,
        ILogger<ServerConnectionService> logger)
    {
        _configService = configService;
        _requestTracker = requestTracker;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// 连接状态变更事件.
    /// </summary>
    public event Action<bool>? ConnectionStateChanged;

    /// <summary>
    /// 当前是否已连接.
    /// </summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    /// <summary>
    /// 注册设备并获取令牌.
    /// </summary>
    public async Task<bool> RegisterDeviceAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configService.Settings;
        var request = new RegisterDeviceRequest
        {
            DeviceName = settings.DeviceName,
            DeviceType = DeviceType.ClaudeCode,
            PreSharedKey = settings.PreSharedKey,
        };

        try
        {
            var url = new Uri($"{settings.ServerUrl.TrimEnd('/')}/api/auth/register");
            using var content = JsonContent.Create(request, ConsoleJsonContext.Default.RegisterDeviceRequest);
            using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("设备注册失败: {StatusCode} {Body}", response.StatusCode, body);
                return false;
            }

            var authResponse = await response.Content.ReadFromJsonAsync(ConsoleJsonContext.Default.AuthResponse, cancellationToken).ConfigureAwait(false);
            if (authResponse is null)
            {
                _logger.LogError("设备注册响应解析失败");
                return false;
            }

            await _configService.UpdateAsync(s =>
            {
                s.DeviceId = authResponse.DeviceId;
                s.AccessToken = authResponse.AccessToken;
                s.TokenExpiresAt = authResponse.ExpiresAt;
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("设备注册成功: DeviceId={DeviceId}", authResponse.DeviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设备注册异常");
            return false;
        }
    }

    /// <summary>
    /// 刷新令牌.
    /// </summary>
    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configService.Settings;
        if (string.IsNullOrEmpty(settings.AccessToken))
        {
            return false;
        }

        try
        {
            var url = $"{settings.ServerUrl.TrimEnd('/')}/api/auth/refresh";
            var request = new RefreshTokenRequest { AccessToken = settings.AccessToken };
            using var content = JsonContent.Create(request, ConsoleJsonContext.Default.RefreshTokenRequest);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.AccessToken);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("令牌刷新失败: {StatusCode}", response.StatusCode);
                return false;
            }

            var authResponse = await response.Content.ReadFromJsonAsync(ConsoleJsonContext.Default.AuthResponse, cancellationToken).ConfigureAwait(false);
            if (authResponse is null)
            {
                return false;
            }

            await _configService.UpdateAsync(s =>
            {
                s.AccessToken = authResponse.AccessToken;
                s.TokenExpiresAt = authResponse.ExpiresAt;
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("令牌刷新成功，过期时间: {ExpiresAt}", DateTimeOffset.FromUnixTimeSeconds(authResponse.ExpiresAt));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "令牌刷新异常");
            return false;
        }
    }

    /// <summary>
    /// 查询设备状态列表.
    /// </summary>
    public async Task<List<DeviceStatusResponse>?> GetDeviceStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configService.Settings;
        try
        {
            var url = $"{settings.ServerUrl.TrimEnd('/')}/api/devices/status";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.AccessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("查询设备状态失败: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync(ConsoleJsonContext.Default.ListDeviceStatusResponse, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询设备状态异常");
            return null;
        }
    }

    /// <summary>
    /// 建立 WebSocket 连接并启动心跳和接收循环.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configService.Settings;
        if (string.IsNullOrEmpty(settings.AccessToken))
        {
            _logger.LogError("未找到令牌，无法连接");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var wsUrl = settings.ServerUrl.TrimEnd('/').Replace("http://", "ws://").Replace("https://", "wss://");
        wsUrl = $"{wsUrl}/ws/connect?token={settings.AccessToken}";

        _webSocket = new ClientWebSocket();
        try
        {
            await _webSocket.ConnectAsync(new Uri(wsUrl), _cts.Token).ConfigureAwait(false);
            _logger.LogInformation("WebSocket 已连接到服务器");
            ConnectionStateChanged?.Invoke(true);

            _heartbeatTask = HeartbeatLoopAsync(_cts.Token);
            _receiveTask = ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket 连接失败");
            ConnectionStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// 发送 WebSocket 消息.
    /// </summary>
    public async Task<bool> SendMessageAsync(WebSocketMessage message, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            _logger.LogWarning("WebSocket 未连接，消息发送失败");
            return false;
        }

        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(message, ConsoleJsonContext.Default.WebSocketMessage);
            await _webSocket.SendAsync(json, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("消息已发送: Type={Type}, MessageId={MessageId}", message.Type, message.MessageId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息失败");
            return false;
        }
    }

    /// <summary>
    /// 启动自动重连循环.
    /// </summary>
    public async Task RunWithReconnectAsync(CancellationToken cancellationToken)
    {
        var delay = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 确保 token 有效
                if (_configService.IsTokenExpired())
                {
                    _logger.LogInformation("令牌已过期，尝试刷新...");
                    if (!await RefreshTokenAsync(cancellationToken).ConfigureAwait(false))
                    {
                        _logger.LogInformation("刷新失败，重新注册设备...");
                        if (!await RegisterDeviceAsync(cancellationToken).ConfigureAwait(false))
                        {
                            _logger.LogError("重新注册失败，{Delay}秒后重试", delay);
                            await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken).ConfigureAwait(false);
                            delay = Math.Min(delay * 2, MaxReconnectDelaySeconds);
                            continue;
                        }
                    }
                }

                await ConnectAsync(cancellationToken).ConfigureAwait(false);
                delay = 1;

                // 等待接收循环结束（连接断开）
                if (_receiveTask is not null)
                {
                    await _receiveTask.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "连接异常，{Delay}秒后重连", delay);
            }

            ConnectionStateChanged?.Invoke(false);

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken).ConfigureAwait(false);
                delay = Math.Min(delay * 2, MaxReconnectDelaySeconds);
            }
        }
    }

    /// <summary>
    /// 断开连接.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "关闭 WebSocket 时出错");
            }
        }

        ConnectionStateChanged?.Invoke(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        _cts?.Dispose();
        _webSocket?.Dispose();
        _httpClient.Dispose();
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), cancellationToken).ConfigureAwait(false);
                var heartbeat = new WebSocketMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = MessageType.Heartbeat,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };
                await SendMessageAsync(heartbeat, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "心跳发送失败");
                break;
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var stream = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                stream.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("服务器关闭了连接");
                        return;
                    }

                    await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
                }
                while (!result.EndOfMessage);

                stream.Position = 0;
                var message = await JsonSerializer.DeserializeAsync(
                    stream, ConsoleJsonContext.Default.WebSocketMessage, cancellationToken).ConfigureAwait(false);

                if (message is not null)
                {
                    await HandleReceivedMessageAsync(message, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket 接收异常");
                break;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "消息反序列化失败");
            }
        }
    }

    private Task HandleReceivedMessageAsync(WebSocketMessage message, CancellationToken cancellationToken)
    {
        _logger.LogDebug("收到消息: Type={Type}, MessageId={MessageId}, CorrelationId={CorrelationId}",
            message.Type, message.MessageId, message.CorrelationId);

        if (message.Type == MessageType.Response && !string.IsNullOrEmpty(message.CorrelationId))
        {
            try
            {
                var response = JsonSerializer.Deserialize(message.Payload, ConsoleJsonContext.Default.PermissionResponsePayload);
                if (response is not null)
                {
                    _requestTracker.TryComplete(response.RequestId, response);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析权限响应失败: {Payload}", message.Payload);
            }
        }

        return Task.CompletedTask;
    }
}
