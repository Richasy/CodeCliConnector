// Copyright (c) Richasy. All rights reserved.

using Microsoft.Data.Sqlite;

namespace CodeCliConnector.Core.Migrations;

/// <summary>
/// 迁移辅助方法.
/// </summary>
#pragma warning disable CA2100
public static class MigrationHelpers
{
    /// <summary>
    /// 检查表是否存在.
    /// </summary>
    public static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    /// <summary>
    /// 检查列是否存在.
    /// </summary>
    public static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 添加列（如果不存在）.
    /// </summary>
    public static async Task<bool> AddColumnIfNotExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken = default)
    {
        if (await ColumnExistsAsync(connection, tableName, columnName, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 创建索引（如果不存在）.
    /// </summary>
    public static async Task CreateIndexIfNotExistsAsync(
        SqliteConnection connection,
        string indexName,
        string tableName,
        string columns,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE INDEX IF NOT EXISTS {indexName} ON {tableName}({columns});";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除表（如果存在）.
    /// </summary>
    public static async Task DropTableIfExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS {tableName};";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除索引（如果存在）.
    /// </summary>
    public static async Task DropIndexIfExistsAsync(
        SqliteConnection connection,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DROP INDEX IF EXISTS {indexName};";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行 SQL 语句.
    /// </summary>
    public static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取表的所有列名.
    /// </summary>
    public static async Task<IReadOnlyList<string>> GetTableColumnsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var columns = new List<string>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    /// <summary>
    /// 校验表是否包含所有必需的列.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ValidateColumnsAsync(
        SqliteConnection connection,
        string tableName,
        IEnumerable<string> expectedColumns,
        CancellationToken cancellationToken = default)
    {
        var actualColumns = await GetTableColumnsAsync(connection, tableName, cancellationToken).ConfigureAwait(false);
        var actualSet = new HashSet<string>(actualColumns, StringComparer.OrdinalIgnoreCase);
        return expectedColumns.Where(c => !actualSet.Contains(c)).ToList();
    }
}
#pragma warning restore CA2100
