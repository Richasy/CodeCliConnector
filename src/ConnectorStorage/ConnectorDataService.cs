// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Storage.Database;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Storage;

/// <summary>
/// 连接器数据服务.
/// </summary>
public sealed partial class ConnectorDataService : IAsyncDisposable
{
    private readonly ConnectorDatabase _database;
    private readonly DeviceEntityRepository<ConnectorDatabase> _deviceRepo = new();
    private readonly MessageEntityRepository<ConnectorDatabase> _messageRepo = new();
    private readonly AccessTokenEntityRepository<ConnectorDatabase> _tokenRepo = new();
    private readonly ILogger<ConnectorDataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectorDataService"/> class.
    /// </summary>
    public ConnectorDataService(ILogger<ConnectorDataService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _database = new ConnectorDatabase(loggerFactory.CreateLogger<ConnectorDatabase>());
    }

    /// <summary>
    /// 初始化数据服务.
    /// </summary>
    public async Task InitializeAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("数据服务初始化完成");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync().ConfigureAwait(false);
    }
}
