// Copyright (c) Richasy. All rights reserved.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Core.Migrations;

/// <summary>
/// 数据库迁移执行器.
/// </summary>
public sealed class MigrationRunner
{
    private readonly SqliteConnection _connection;
    private readonly string _databasePath;
    private readonly MigrationOptions _options;
    private readonly IReadOnlyList<IMigration> _migrations;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationRunner"/> class.
    /// </summary>
    public MigrationRunner(
        SqliteConnection connection,
        string databasePath,
        MigrationOptions options,
        IEnumerable<IMigration> migrations,
        ILogger? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _migrations = migrations.OrderBy(m => m.Version).ToList();
        _logger = logger;

        ValidateMigrations();
    }

    /// <summary>
    /// 执行数据库迁移.
    /// </summary>
    /// <returns>是否执行了迁移.</returns>
    public async Task<bool> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = await GetDatabaseVersionAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation(
            "[{DatabaseName}] 当前版本: v{CurrentVersion}, 目标版本: v{TargetVersion}",
            _options.DatabaseName,
            currentVersion,
            _options.CurrentVersion);

        if (currentVersion >= _options.CurrentVersion)
        {
            _logger?.LogDebug("[{DatabaseName}] 数据库已是最新版本，无需迁移", _options.DatabaseName);
            return false;
        }

        if (currentVersion < _options.MinSupportedVersion)
        {
            throw new DatabaseTooOldException(currentVersion, _options.MinSupportedVersion, _options.DatabaseName);
        }

        var pendingMigrations = _migrations
            .Where(m => m.Version > currentVersion && m.Version <= _options.CurrentVersion)
            .ToList();

        if (pendingMigrations.Count == 0)
        {
            _logger?.LogDebug("[{DatabaseName}] 没有待执行的迁移", _options.DatabaseName);
            await SetDatabaseVersionAsync(_options.CurrentVersion, cancellationToken).ConfigureAwait(false);
            return false;
        }

        string? backupPath = null;
        if (_options.BackupBeforeMigration && File.Exists(_databasePath))
        {
            backupPath = await CreateBackupAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            foreach (var migration in pendingMigrations)
            {
                _logger?.LogInformation(
                    "[{DatabaseName}] 执行迁移: v{Version} - {Description}",
                    _options.DatabaseName,
                    migration.Version,
                    migration.Description);

                try
                {
                    await migration.ExecuteAsync(_connection, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new MigrationException(migration.Version, migration.Description, ex);
                }
            }

            await SetDatabaseVersionAsync(_options.CurrentVersion, cancellationToken).ConfigureAwait(false);

            if (backupPath is not null && File.Exists(backupPath))
            {
                File.Delete(backupPath);
                _logger?.LogDebug("[{DatabaseName}] 已删除迁移备份", _options.DatabaseName);
            }

            _logger?.LogInformation(
                "[{DatabaseName}] 迁移完成，当前版本: v{Version}",
                _options.DatabaseName,
                _options.CurrentVersion);

            return true;
        }
        catch
        {
            if (backupPath is not null && File.Exists(backupPath))
            {
                _logger?.LogWarning("[{DatabaseName}] 迁移失败，保留备份文件: {BackupPath}", _options.DatabaseName, backupPath);
            }

            throw;
        }
    }

#pragma warning disable CA2100
    private async Task<int> GetDatabaseVersionAsync(CancellationToken cancellationToken)
    {
        await using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='_migrations';";
        var result = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            await using var checkPrimaryCmd = _connection.CreateCommand();
            checkPrimaryCmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{_options.PrimaryTableName}';";
            var primaryResult = await checkPrimaryCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            await using var createCmd = _connection.CreateCommand();
            if (primaryResult is null)
            {
                createCmd.CommandText = $"""
                    CREATE TABLE _migrations (
                        Key TEXT PRIMARY KEY NOT NULL,
                        Value TEXT NOT NULL
                    );
                    INSERT INTO _migrations (Key, Value) VALUES ('version', '{_options.CurrentVersion}');
                    """;
                await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return _options.CurrentVersion;
            }
            else
            {
                createCmd.CommandText = """
                    CREATE TABLE _migrations (
                        Key TEXT PRIMARY KEY NOT NULL,
                        Value TEXT NOT NULL
                    );
                    INSERT INTO _migrations (Key, Value) VALUES ('version', '1');
                    """;
                await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return 1;
            }
        }

        await using var versionCmd = _connection.CreateCommand();
        versionCmd.CommandText = "SELECT Value FROM _migrations WHERE Key = 'version';";
        var versionResult = await versionCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return versionResult is string versionStr && int.TryParse(versionStr, out var version) ? version : 1;
    }
#pragma warning restore CA2100

    private async Task SetDatabaseVersionAsync(int version, CancellationToken cancellationToken)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE _migrations SET Value = @version WHERE Key = 'version';";
        cmd.Parameters.AddWithValue("@version", version.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> CreateBackupAsync(CancellationToken cancellationToken)
    {
        var backupPath = $"{_databasePath}.backup-{DateTime.Now:yyyyMMddHHmmss}";
        await Task.Run(() => File.Copy(_databasePath, backupPath, overwrite: false), cancellationToken).ConfigureAwait(false);
        _logger?.LogInformation("[{DatabaseName}] 已创建迁移备份: {BackupPath}", _options.DatabaseName, backupPath);
        return backupPath;
    }

    private void ValidateMigrations()
    {
        var versions = _migrations.Select(m => m.Version).ToList();
        var duplicates = versions.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (duplicates.Count > 0)
        {
            throw new ArgumentException($"存在重复的迁移版本: {string.Join(", ", duplicates)}");
        }
    }
}
