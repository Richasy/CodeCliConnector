// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Core.Migrations;

/// <summary>
/// 数据库版本过旧异常.
/// </summary>
public sealed class DatabaseTooOldException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseTooOldException"/> class.
    /// </summary>
    public DatabaseTooOldException(int currentVersion, int minSupportedVersion, string databaseName)
        : base($"数据库 {databaseName} 版本过旧 (v{currentVersion})，最低支持版本为 v{minSupportedVersion}，请备份数据后删除数据库文件重新创建。")
    {
        CurrentVersion = currentVersion;
        MinSupportedVersion = minSupportedVersion;
        DatabaseName = databaseName;
    }

    /// <summary>
    /// 当前数据库版本.
    /// </summary>
    public int CurrentVersion { get; }

    /// <summary>
    /// 最低支持版本.
    /// </summary>
    public int MinSupportedVersion { get; }

    /// <summary>
    /// 数据库名称.
    /// </summary>
    public string DatabaseName { get; }
}

/// <summary>
/// 迁移执行异常.
/// </summary>
public sealed class MigrationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationException"/> class.
    /// </summary>
    public MigrationException(int version, string description, Exception innerException)
        : base($"迁移到 v{version} ({description}) 失败: {innerException.Message}", innerException)
    {
        Version = version;
        Description = description;
    }

    /// <summary>
    /// 失败的迁移版本.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// 迁移描述.
    /// </summary>
    public string Description { get; }
}
