// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Core.Models.Constants;

/// <summary>
/// 消息类型.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// 心跳.
    /// </summary>
    Heartbeat = 0,

    /// <summary>
    /// 通知（如权限请求）.
    /// </summary>
    Notification = 1,

    /// <summary>
    /// 命令（如审批结果）.
    /// </summary>
    Command = 2,

    /// <summary>
    /// 响应.
    /// </summary>
    Response = 3,
}
