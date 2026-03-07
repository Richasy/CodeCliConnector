// Copyright (c) Richasy. All rights reserved.

using System.Net;
using System.Text;
using System.Text.Json;
using CodeCliConnector.Console.Models;
using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Console.Services;

/// <summary>
/// 本地 HTTP 监听服务，接收 Claude Code 的 hook 回调.
/// </summary>
internal sealed class HookListenerService : IAsyncDisposable
{
    private const int PermissionTimeoutSeconds = 21600;

    private readonly ConfigService _configService;
    private readonly ServerConnectionService _serverConnection;
    private readonly PendingRequestTracker _requestTracker;
    private readonly ILogger<HookListenerService> _logger;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HookListenerService"/> class.
    /// </summary>
    public HookListenerService(
        ConfigService configService,
        ServerConnectionService serverConnection,
        PendingRequestTracker requestTracker,
        ILogger<HookListenerService> logger)
    {
        _configService = configService;
        _serverConnection = serverConnection;
        _requestTracker = requestTracker;
        _logger = logger;
    }

    /// <summary>
    /// 启动监听.
    /// </summary>
    public void Start(CancellationToken cancellationToken = default)
    {
        var port = _configService.Settings.HookListenerPort;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.Start();
        _logger.LogInformation("Hook 监听器已启动: http://localhost:{Port}/", port);

        _listenTask = ListenLoopAsync(_cts.Token);
    }

    /// <summary>
    /// 停止监听.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        _listener?.Stop();

        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        _logger.LogInformation("Hook 监听器已停止");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
        (_listener as IDisposable)?.Dispose();
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener!.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            // 每个请求在独立任务中处理，不阻塞监听循环
            _ = HandleRequestAsync(context, cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var path = context.Request.Url?.AbsolutePath ?? string.Empty;
        _logger.LogDebug("收到 Hook 请求: {Method} {Path}", context.Request.HttpMethod, path);

        try
        {
            if (context.Request.HttpMethod != "POST")
            {
                await WriteResponseAsync(context.Response, 405, "Method Not Allowed").ConfigureAwait(false);
                return;
            }

            switch (path)
            {
                case "/notification":
                    await HandleNotificationAsync(context, cancellationToken).ConfigureAwait(false);
                    break;
                case "/permission":
                    await HandlePermissionRequestAsync(context, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    await WriteResponseAsync(context.Response, 404, "Not Found").ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 Hook 请求失败: {Path}", path);
            try
            {
                await WriteResponseAsync(context.Response, 500, "Internal Server Error").ConfigureAwait(false);
            }
            catch
            {
                // 响应可能已经发送了
            }
        }
    }

    private async Task HandleNotificationAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(context.Request, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            await WriteResponseAsync(context.Response, 400, "Invalid payload").ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("收到通知: Type={NotificationType}, Title={Title}, Message={Message}",
            payload.NotificationType, payload.Title, payload.Message);

        // 构造通知消息载荷
        var notificationPayload = new NotificationPayload
        {
            SessionId = payload.SessionId,
            Cwd = payload.Cwd,
            Title = payload.Title,
            Message = payload.Message,
            NotificationType = payload.NotificationType,
        };

        var wsMessage = new WebSocketMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Type = MessageType.Notification,
            SourceDeviceId = _configService.Settings.DeviceId,
            Payload = JsonSerializer.Serialize(notificationPayload, ConsoleJsonContext.Default.NotificationPayload),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        await _serverConnection.SendMessageAsync(wsMessage, cancellationToken).ConfigureAwait(false);

        // Notification 是 fire-and-forget，立即返回
        await WriteResponseAsync(context.Response, 200, string.Empty).ConfigureAwait(false);
    }

    private async Task HandlePermissionRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(context.Request, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            await WriteResponseAsync(context.Response, 400, "Invalid payload").ConfigureAwait(false);
            return;
        }

        var requestId = Guid.NewGuid().ToString();
        _logger.LogInformation("收到权限请求: RequestId={RequestId}, Tool={ToolName}, Session={SessionId}",
            requestId, payload.ToolName, payload.SessionId);

        // 构造权限请求载荷
        var permissionPayload = new PermissionRequestPayload
        {
            RequestId = requestId,
            SessionId = payload.SessionId,
            Cwd = payload.Cwd,
            PermissionMode = payload.PermissionMode,
            ToolName = payload.ToolName,
            ToolInput = payload.ToolInput,
            ReceivedTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var wsMessage = new WebSocketMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Type = MessageType.Notification,
            SourceDeviceId = _configService.Settings.DeviceId,
            Payload = JsonSerializer.Serialize(permissionPayload, ConsoleJsonContext.Default.PermissionRequestPayload),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        // 创建待处理请求
        var tcs = _requestTracker.Create(requestId);

        // 发送到服务器
        var sent = await _serverConnection.SendMessageAsync(wsMessage, cancellationToken).ConfigureAwait(false);
        if (!sent)
        {
            _requestTracker.Remove(requestId);
            _logger.LogWarning("权限请求发送失败，默认拒绝: {RequestId}", requestId);
            var denyResponse = CreateHookResponse("deny", "Failed to send permission request to server");
            await WriteJsonResponseAsync(context.Response, 200, denyResponse).ConfigureAwait(false);
            return;
        }

        // 等待远程响应
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(PermissionTimeoutSeconds));

            var response = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            _logger.LogInformation("权限请求已响应: RequestId={RequestId}, Behavior={Behavior}", requestId, response.Behavior);

            var hookResponse = CreateHookResponse(response.Behavior, response.Message);
            await WriteJsonResponseAsync(context.Response, 200, hookResponse).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _requestTracker.Remove(requestId);
            _logger.LogWarning("权限请求超时，默认拒绝: {RequestId}", requestId);
            var denyResponse = CreateHookResponse("deny", "Request timed out");
            await WriteJsonResponseAsync(context.Response, 200, denyResponse).ConfigureAwait(false);
        }
    }

    private static PermissionRequestHookResponse CreateHookResponse(string behavior, string? message)
    {
        return new PermissionRequestHookResponse
        {
            HookSpecificOutput = new PermissionRequestHookOutput
            {
                HookEventName = "PermissionRequest",
                Decision = new PermissionDecisionDetail
                {
                    Behavior = behavior,
                    Message = message,
                },
            },
        };
    }

    private async Task<HookPayload?> ReadPayloadAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(
                request.InputStream, ConsoleJsonContext.Default.HookPayload, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Hook payload 解析失败");
            return null;
        }
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, int statusCode, string body)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/plain";
        var bytes = Encoding.UTF8.GetBytes(body);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, int statusCode, PermissionRequestHookResponse hookResponse)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        var json = JsonSerializer.SerializeToUtf8Bytes(hookResponse, ConsoleJsonContext.Default.PermissionRequestHookResponse);
        response.ContentLength64 = json.Length;
        await response.OutputStream.WriteAsync(json).ConfigureAwait(false);
        response.Close();
    }
}
