// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models.Constants;

namespace CodeCliConnector.Core.Models;

/// <summary>
/// 消息信息.
/// </summary>
public sealed class MessageInfo
{
    /// <summary>
    /// 消息唯一标识.
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// 来源设备 ID.
    /// </summary>
    public required string SourceDeviceId { get; set; }

    /// <summary>
    /// 目标设备 ID（为空表示广播）.
    /// </summary>
    public string? TargetDeviceId { get; set; }

    /// <summary>
    /// 消息类型.
    /// </summary>
    public MessageType MessageType { get; set; }

    /// <summary>
    /// 消息状态.
    /// </summary>
    public MessageStatus Status { get; set; }

    /// <summary>
    /// 消息载荷（JSON 字符串）.
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// 创建时间（Unix 时间戳秒）.
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// 过期时间（Unix 时间戳秒）.
    /// </summary>
    public long ExpiresAt { get; set; }

    /// <summary>
    /// 关联消息 ID（用于响应关联请求）.
    /// </summary>
    public string? CorrelationId { get; set; }
}
