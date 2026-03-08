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

    /// <summary>
    /// 要应用的权限规则更新（JSON 字符串，等同于用户选择"总是允许"选项）.
    /// </summary>
    public string? UpdatedPermissions { get; set; }

    /// <summary>
    /// 是否中断 Claude（仅 deny 时有效，为 true 时直接停止 Claude）.
    /// </summary>
    public bool? Interrupt { get; set; }
}
