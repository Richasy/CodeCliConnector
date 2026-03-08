// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Console.Models;

/// <summary>
/// 通知消息载荷（作为 WebSocket 消息的 Payload 传递）.
/// </summary>
internal sealed class NotificationPayload
{
    /// <summary>
    /// Hook 事件类型（如 "notification"、"stop"），用于区分不同来源的通知.
    /// </summary>
    public string? HookEvent { get; set; }

    /// <summary>
    /// 会话 ID.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 工作目录.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// 通知标题.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 通知消息.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 通知类型.
    /// </summary>
    public string? NotificationType { get; set; }
}
