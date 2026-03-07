// Copyright (c) Richasy. All rights reserved.

using System.Text.Json.Serialization;

namespace CodeCliConnector.Console.Models;

/// <summary>
/// 返回给 Claude Code 的 Hook 响应（PermissionRequest 事件）.
/// </summary>
internal sealed class PermissionRequestHookResponse
{
    /// <summary>
    /// Hook 特定输出.
    /// </summary>
    [JsonPropertyName("hookSpecificOutput")]
    public PermissionRequestHookOutput? HookSpecificOutput { get; set; }
}

/// <summary>
/// PermissionRequest Hook 输出.
/// </summary>
internal sealed class PermissionRequestHookOutput
{
    /// <summary>
    /// Hook 事件名称.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public string HookEventName { get; set; } = "PermissionRequest";

    /// <summary>
    /// 决策详情.
    /// </summary>
    [JsonPropertyName("decision")]
    public PermissionDecisionDetail? Decision { get; set; }
}

/// <summary>
/// 权限决策详情.
/// </summary>
internal sealed class PermissionDecisionDetail
{
    /// <summary>
    /// 行为（allow 或 deny）.
    /// </summary>
    [JsonPropertyName("behavior")]
    public string Behavior { get; set; } = "deny";

    /// <summary>
    /// 消息.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
