// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Core.Migrations;

/// <summary>
/// 迁移配置.
/// </summary>
public sealed class MigrationOptions
{
    /// <summary>
    /// 当前 Schema 版本.
    /// </summary>
    public required int CurrentVersion { get; init; }

    /// <summary>
    /// 最低支持版本.
    /// </summary>
    public int MinSupportedVersion { get; init; } = 1;

    /// <summary>
    /// 数据库名称（用于日志和错误消息）.
    /// </summary>
    public required string DatabaseName { get; init; }

    /// <summary>
    /// 用于检测是否为全新数据库的表名.
    /// </summary>
    public required string PrimaryTableName { get; init; }

    /// <summary>
    /// 是否在迁移前创建备份.
    /// </summary>
    public bool BackupBeforeMigration { get; init; } = true;
}
