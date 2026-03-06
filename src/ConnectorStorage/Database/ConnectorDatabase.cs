// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Migrations;
using CodeCliConnector.SqliteGenerator;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Storage.Database;

/// <summary>
/// 连接器数据库.
/// </summary>
internal sealed class ConnectorDatabase : ISqliteDatabase, IAsyncDisposable
{
    private readonly ILogger<ConnectorDatabase> _logger;
    private SqliteConnection? _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectorDatabase"/> class.
    /// </summary>
    public ConnectorDatabase(ILogger<ConnectorDatabase> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化数据库.
    /// </summary>
    public async Task InitializeAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // 启用 WAL 模式
        await using var walCmd = _connection.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode=WAL;";
        await walCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // 创建表结构
        await using var schemaCmd = _connection.CreateCommand();
        schemaCmd.CommandText = $"{Schema.CreateDevicesTable}\n{Schema.CreateMessagesTable}\n{Schema.CreateAccessTokensTable}";
        await schemaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // 运行迁移
        var options = ConnectorMigrations.GetOptions();
        var migrations = ConnectorMigrations.GetMigrations();
        var runner = new MigrationRunner(_connection, databasePath, options, migrations, _logger);
        await runner.MigrateAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("数据库初始化完成: {Path}", databasePath);
    }

    /// <inheritdoc/>
#pragma warning disable CA2100
    public SqliteCommand CreateCommand(string sql)
    {
        var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }
#pragma warning restore CA2100

    /// <inheritdoc/>
    public async Task<SqliteTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return (SqliteTransaction)await _connection!.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }
}
