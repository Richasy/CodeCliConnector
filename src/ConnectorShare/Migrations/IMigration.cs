// Copyright (c) Richasy. All rights reserved.

using Microsoft.Data.Sqlite;

namespace CodeCliConnector.Core.Migrations;

/// <summary>
/// 数据库迁移接口.
/// </summary>
public interface IMigration
{
    /// <summary>
    /// 目标版本号.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// 迁移描述.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 执行迁移.
    /// </summary>
    /// <param name="connection">数据库连接.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task ExecuteAsync(SqliteConnection connection, CancellationToken cancellationToken = default);
}
