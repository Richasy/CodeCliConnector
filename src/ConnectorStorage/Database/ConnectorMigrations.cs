// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Migrations;

namespace CodeCliConnector.Storage.Database;

/// <summary>
/// 连接器迁移配置.
/// </summary>
internal static class ConnectorMigrations
{
    /// <summary>
    /// 获取迁移配置.
    /// </summary>
    public static MigrationOptions GetOptions()
    {
        return new MigrationOptions
        {
            CurrentVersion = 1,
            MinSupportedVersion = 1,
            DatabaseName = "ConnectorStorage",
            PrimaryTableName = "Devices",
        };
    }

    /// <summary>
    /// 获取所有迁移.
    /// </summary>
    public static IReadOnlyList<IMigration> GetMigrations()
    {
        // v1 是初始版本，没有迁移脚本
        return [];
    }
}
