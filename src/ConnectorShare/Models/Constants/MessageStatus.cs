// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Core.Models.Constants;

/// <summary>
/// 消息状态.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// 待投递.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 已投递.
    /// </summary>
    Delivered = 1,

    /// <summary>
    /// 已处理.
    /// </summary>
    Processed = 2,

    /// <summary>
    /// 已过期.
    /// </summary>
    Expired = 3,
}
