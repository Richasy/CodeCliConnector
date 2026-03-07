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

    /// <summary>
    /// 存储 requestId → correlationId（WebSocket 消息的 messageId）的映射，
    /// 用于在 PostToolUse 到来时发送 locally_handled 通知.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _requestCorrelationMap = new();

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
                case "/tool-completed":
                    await HandleToolCompletedAsync(context, cancellationToken).ConfigureAwait(false);
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

        // 延迟转发：等待用户在本地 Claude Code 终端操作
        var delaySeconds = _configService.Settings.PermissionForwardDelaySeconds;
        if (delaySeconds > 0)
        {
            _logger.LogInformation("等待 {DelaySeconds}s，若用户在终端操作则跳过远程转发: {RequestId}",
                delaySeconds, requestId);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // 延迟结束后检测客户端是否仍在等待
            if (!IsClientConnected(context))
            {
                _logger.LogInformation("用户已在终端处理权限请求，跳过远程转发: {RequestId}", requestId);
                return;
            }

            _logger.LogInformation("延迟已过，用户未在终端操作，转发到远程设备: {RequestId}", requestId);
        }

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

        var messageId = Guid.NewGuid().ToString();
        var wsMessage = new WebSocketMessage
        {
            MessageId = messageId,
            Type = MessageType.Notification,
            SourceDeviceId = _configService.Settings.DeviceId,
            Payload = JsonSerializer.Serialize(permissionPayload, ConsoleJsonContext.Default.PermissionRequestPayload),
            CorrelationId = messageId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        // 创建待处理请求
        var tcs = _requestTracker.Create(requestId, payload.SessionId, payload.ToolName);

        // 记录 requestId → correlationId 映射
        _requestCorrelationMap[requestId] = messageId;

        // 发送到服务器
        var sent = await _serverConnection.SendMessageAsync(wsMessage, cancellationToken).ConfigureAwait(false);
        if (!sent)
        {
            _requestTracker.Remove(requestId);
            _requestCorrelationMap.TryRemove(requestId, out _);
            _logger.LogWarning("权限请求发送失败，默认拒绝: {RequestId}", requestId);
            var denyResponse = CreateHookResponse("deny", "Failed to send permission request to server");
            await WriteJsonResponseAsync(context.Response, 200, denyResponse).ConfigureAwait(false);
            return;
        }

        // 等待远程响应（同时定期检测客户端是否断开）
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(PermissionTimeoutSeconds));

            while (!timeoutCts.Token.IsCancellationRequested)
            {
                // 每秒检查一次：远程响应是否到达，或客户端是否断开
                var delayTask = Task.Delay(1000, timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);

                if (completedTask == tcs.Task)
                {
                    // 收到远程响应（或 PostToolUse 触发的 locally_handled）
                    var response = await tcs.Task.ConfigureAwait(false);
                    _requestCorrelationMap.TryRemove(requestId, out _);
                    _logger.LogInformation("权限请求已响应: RequestId={RequestId}, Behavior={Behavior}", requestId, response.Behavior);

                    // locally_handled 意味着工具已在本地执行完毕，映射为 allow
                    var behavior = response.Behavior == "locally_handled" ? "allow" : response.Behavior;
                    var hookResponse = CreateHookResponse(behavior, response.Message);
                    try
                    {
                        await WriteJsonResponseAsync(context.Response, 200, hookResponse).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        _logger.LogWarning(ex, "HTTP 响应流已关闭（Claude Code 可能已超时），但权限响应已收到: {RequestId}", requestId);
                    }

                    return;
                }

                // 检测客户端是否仍在等待
                if (!IsClientConnected(context))
                {
                    _requestTracker.Remove(requestId);
                    _requestCorrelationMap.TryRemove(requestId, out _);
                    _logger.LogInformation("等待远程响应期间用户已在终端操作，取消远程请求: {RequestId}", requestId);

                    // 通知服务器此请求已在本地处理，让其他终端更新状态
                    await SendLocallyHandledResponseAsync(requestId, messageId, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _requestTracker.Remove(requestId);
            _requestCorrelationMap.TryRemove(requestId, out _);
            _logger.LogWarning("权限请求超时，默认拒绝: {RequestId}", requestId);
            var denyResponse = CreateHookResponse("deny", "Request timed out");
            await WriteJsonResponseAsync(context.Response, 200, denyResponse).ConfigureAwait(false);
        }
    }

    private async Task HandleToolCompletedAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(context.Request, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            await WriteResponseAsync(context.Response, 400, "Invalid payload").ConfigureAwait(false);
            return;
        }

        // PostToolUse 是 async fire-and-forget hook，先立即返回
        await WriteResponseAsync(context.Response, 200, string.Empty).ConfigureAwait(false);

        if (string.IsNullOrEmpty(payload.SessionId) || string.IsNullOrEmpty(payload.ToolName))
        {
            return;
        }

        // 通过 session_id + tool_name 查找匹配的 pending permission request
        var requestId = _requestTracker.TryCompleteByToolKey(payload.SessionId, payload.ToolName);
        if (requestId is null)
        {
            return;
        }

        _logger.LogInformation("PostToolUse 触发本地处理: SessionId={SessionId}, Tool={ToolName}, RequestId={RequestId}",
            payload.SessionId, payload.ToolName, requestId);

        // 通知服务器此请求已在本地处理
        if (_requestCorrelationMap.TryRemove(requestId, out var correlationId))
        {
            await SendLocallyHandledResponseAsync(requestId, correlationId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 检测 HTTP 客户端是否仍然连接.
    /// 通过尝试 flush 输出流来探测，如果客户端已断开则抛出异常.
    /// </summary>
    private static bool IsClientConnected(HttpListenerContext context)
    {
        try
        {
            context.Response.OutputStream.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 向服务器发送"本地已处理"响应，触发服务器通知其他终端更新状态.
    /// </summary>
    private async Task SendLocallyHandledResponseAsync(string requestId, string correlationId, CancellationToken cancellationToken)
    {
        var responsePayload = new PermissionResponsePayload
        {
            RequestId = requestId,
            Behavior = "locally_handled",
            Message = "用户已在终端本地处理",
        };

        var wsMessage = new WebSocketMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Type = MessageType.Response,
            SourceDeviceId = _configService.Settings.DeviceId,
            Payload = JsonSerializer.Serialize(responsePayload, ConsoleJsonContext.Default.PermissionResponsePayload),
            CorrelationId = correlationId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        var sent = await _serverConnection.SendMessageAsync(wsMessage, cancellationToken).ConfigureAwait(false);
        if (sent)
        {
            _logger.LogInformation("已通知服务器权限请求在本地处理: {RequestId}", requestId);
        }
        else
        {
            _logger.LogWarning("通知服务器本地处理失败: {RequestId}", requestId);
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
