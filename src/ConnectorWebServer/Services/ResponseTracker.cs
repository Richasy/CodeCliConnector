// Copyright (c) Richasy. All rights reserved.

using System.Collections.Concurrent;

namespace CodeCliConnector.Server.Services;

/// <summary>
/// 响应跟踪器，确保"第一个回复生效"语义.
/// </summary>
public sealed class ResponseTracker
{
    private readonly ConcurrentDictionary<string, byte> _claimedResponses = new();

    /// <summary>
    /// 尝试认领响应权（原子操作）.
    /// </summary>
    /// <param name="correlationId">关联的消息 ID.</param>
    /// <returns>是否成功认领（首个调用者返回 true）.</returns>
    public bool TryClaimResponse(string correlationId)
    {
        return _claimedResponses.TryAdd(correlationId, 0);
    }

    /// <summary>
    /// 清除跟踪记录.
    /// </summary>
    public void Remove(string correlationId)
    {
        _claimedResponses.TryRemove(correlationId, out _);
    }

    /// <summary>
    /// 检查消息是否已被认领.
    /// </summary>
    public bool IsClaimed(string correlationId)
    {
        return _claimedResponses.ContainsKey(correlationId);
    }
}
