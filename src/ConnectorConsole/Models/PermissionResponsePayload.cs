// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Console.Models;

/// <summary>
/// 权限响应消息载荷（从终端设备返回的决策结果）.
/// </summary>
internal sealed class PermissionResponsePayload
{
    /// <summary>
    /// 请求 ID.
    /// </summary>
    public required string RequestId { get; set; }

    /// <summary>
    /// 决策行为（allow 或 deny）.
    /// </summary>
    public string Behavior { get; set; } = "deny";

    /// <summary>
    /// 附加消息.
    /// </summary>
    public string? Message { get; set; }
}
