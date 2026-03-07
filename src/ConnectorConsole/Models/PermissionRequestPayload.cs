// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Console.Models;

/// <summary>
/// 权限请求消息载荷（作为 WebSocket 消息的 Payload 传递）.
/// </summary>
internal sealed class PermissionRequestPayload
{
    /// <summary>
    /// 请求唯一 ID.
    /// </summary>
    public required string RequestId { get; set; }

    /// <summary>
    /// 会话 ID.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 工作目录.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// 权限模式.
    /// </summary>
    public string? PermissionMode { get; set; }

    /// <summary>
    /// 工具名称.
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// 工具输入（JSON 字符串）.
    /// </summary>
    public string? ToolInput { get; set; }

    /// <summary>
    /// 请求时间戳（Unix 毫秒）.
    /// </summary>
    public long ReceivedTimestampMs { get; set; }
}
