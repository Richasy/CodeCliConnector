// Copyright (c) Richasy. All rights reserved.

using System.Text.Json.Serialization;

namespace CodeCliConnector.Console.Models;

/// <summary>
/// Claude Code settings.json 中的结构.
/// </summary>
internal sealed class ClaudeSettings
{
    /// <summary>
    /// Hooks 配置.
    /// </summary>
    [JsonPropertyName("hooks")]
    public Dictionary<string, List<ClaudeHookGroup>>? Hooks { get; set; }
}

/// <summary>
/// Hook 匹配器组.
/// </summary>
internal sealed class ClaudeHookGroup
{
    /// <summary>
    /// 匹配器模式.
    /// </summary>
    [JsonPropertyName("matcher")]
    public string Matcher { get; set; } = string.Empty;

    /// <summary>
    /// Hook 处理程序列表.
    /// </summary>
    [JsonPropertyName("hooks")]
    public List<ClaudeHookHandler>? Hooks { get; set; }
}

/// <summary>
/// Hook 处理程序.
/// </summary>
internal sealed class ClaudeHookHandler
{
    /// <summary>
    /// 类型（command / http）.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "command";

    /// <summary>
    /// 命令.
    /// </summary>
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    /// <summary>
    /// 超时秒数.
    /// </summary>
    [JsonPropertyName("timeout")]
    public int? Timeout { get; set; }
}
