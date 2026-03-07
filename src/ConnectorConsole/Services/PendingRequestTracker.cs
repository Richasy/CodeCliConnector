// Copyright (c) Richasy. All rights reserved.

using System.Collections.Concurrent;
using CodeCliConnector.Console.Models;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Console.Services;

/// <summary>
/// 待处理权限请求追踪器.
/// </summary>
internal sealed class PendingRequestTracker : IDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PermissionResponsePayload>> _pending = new();
    private readonly ConcurrentDictionary<string, string> _toolKeyToRequestId = new();
    private readonly ILogger<PendingRequestTracker> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingRequestTracker"/> class.
    /// </summary>
    public PendingRequestTracker(ILogger<PendingRequestTracker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 创建一个待处理的权限请求.
    /// </summary>
    public TaskCompletionSource<PermissionResponsePayload> Create(string requestId, string? sessionId = null, string? toolName = null)
    {
        var tcs = new TaskCompletionSource<PermissionResponsePayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;

        if (!string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(toolName))
        {
            var toolKey = $"{sessionId}:{toolName}";
            _toolKeyToRequestId[toolKey] = requestId;
        }

        _logger.LogDebug("创建待处理请求: {RequestId}", requestId);
        return tcs;
    }

    /// <summary>
    /// 完成一个待处理的权限请求.
    /// </summary>
    public bool TryComplete(string requestId, PermissionResponsePayload response)
    {
        if (_pending.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(response);
            _logger.LogInformation("请求已完成: {RequestId}, 决策: {Behavior}", requestId, response.Behavior);
            return true;
        }

        _logger.LogWarning("未找到待处理请求: {RequestId}", requestId);
        return false;
    }

    /// <summary>
    /// 通过 session_id + tool_name 查找并标记为本地已处理.
    /// </summary>
    public string? TryCompleteByToolKey(string sessionId, string toolName)
    {
        var toolKey = $"{sessionId}:{toolName}";
        if (_toolKeyToRequestId.TryRemove(toolKey, out var requestId) && _pending.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(new PermissionResponsePayload { RequestId = requestId, Behavior = "locally_handled" });
            _logger.LogInformation("工具完成，本地处理: ToolKey={ToolKey}, RequestId={RequestId}", toolKey, requestId);
            return requestId;
        }

        return null;
    }

    /// <summary>
    /// 移除一个待处理请求（超时等情况）.
    /// </summary>
    public void Remove(string requestId)
    {
        if (_pending.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetCanceled();
            _logger.LogDebug("移除待处理请求: {RequestId}", requestId);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetCanceled();
        }

        _pending.Clear();
        _toolKeyToRequestId.Clear();
    }
}
