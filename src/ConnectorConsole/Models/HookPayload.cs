// Copyright (c) Richasy. All rights reserved.

using System.Text.Json.Serialization;

namespace CodeCliConnector.Console.Models;

/// <summary>
/// Claude Code Hook 通用输入载荷.
/// </summary>
internal sealed class HookPayload
{
    /// <summary>
    /// 会话 ID.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    /// <summary>
    /// 转录文件路径.
    /// </summary>
    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }

    /// <summary>
    /// 工作目录.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    /// <summary>
    /// 权限模式.
    /// </summary>
    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; set; }

    /// <summary>
    /// Hook 事件名称.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; set; }

    /// <summary>
    /// 工具名称（PermissionRequest 事件所需）.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public string? ToolName { get; set; }

    /// <summary>
    /// 工具输入（JSON 字符串化后保存）.
    /// </summary>
    [JsonPropertyName("tool_input")]
    [JsonConverter(typeof(RawJsonConverter))]
    public string? ToolInput { get; set; }

    /// <summary>
    /// 消息文本（Notification 事件所需）.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// 标题（Notification 事件所需）.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 通知类型（Notification 事件所需）.
    /// </summary>
    [JsonPropertyName("notification_type")]
    public string? NotificationType { get; set; }
}
