// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models.Constants;

namespace CodeCliConnector.Core.Models;

/// <summary>
/// WebSocket 消息协议.
/// </summary>
public sealed class WebSocketMessage
{
    /// <summary>
    /// 消息 ID.
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// 消息类型.
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// 来源设备 ID.
    /// </summary>
    public string? SourceDeviceId { get; set; }

    /// <summary>
    /// 目标设备 ID（为空表示广播）.
    /// </summary>
    public string? TargetDeviceId { get; set; }

    /// <summary>
    /// 消息载荷（JSON 字符串）.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// 关联消息 ID.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 时间戳（Unix 时间戳秒）.
    /// </summary>
    public long Timestamp { get; set; }
}
