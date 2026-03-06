// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Server.Configuration;

/// <summary>
/// 服务器配置.
/// </summary>
public sealed class ServerSettings
{
    /// <summary>
    /// 配置节名称.
    /// </summary>
    public const string SectionName = "ServerSettings";

    /// <summary>
    /// 预共享密钥.
    /// </summary>
    public string PreSharedKey { get; set; } = "change-me-in-production";

    /// <summary>
    /// 令牌过期天数.
    /// </summary>
    public int TokenExpiryDays { get; set; } = 3;

    /// <summary>
    /// 心跳超时秒数.
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 数据库文件路径.
    /// </summary>
    public string DatabasePath { get; set; } = "connector.db";

    /// <summary>
    /// 消息过期秒数（默认 5 分钟）.
    /// </summary>
    public int MessageExpirySeconds { get; set; } = 300;

    /// <summary>
    /// 旧消息清理天数.
    /// </summary>
    public int MessageCleanupDays { get; set; } = 7;

    /// <summary>
    /// 心跳检测间隔秒数.
    /// </summary>
    public int HeartbeatCheckIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// 消息清理间隔秒数.
    /// </summary>
    public int MessageCleanupIntervalSeconds { get; set; } = 60;
}
