// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models;
using CodeCliConnector.Storage.Database;

namespace CodeCliConnector.Storage;

/// <summary>
/// 连接器数据服务 - 设备操作.
/// </summary>
public sealed partial class ConnectorDataService
{
    /// <summary>
    /// 注册或更新设备.
    /// </summary>
    public async Task UpsertDeviceAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        var entity = DeviceEntity.FromModel(device);
        await _deviceRepo.UpsertAsync(_database, entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取设备信息.
    /// </summary>
    public async Task<DeviceInfo?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var entity = await _deviceRepo.GetByIdAsync(_database, deviceId, cancellationToken).ConfigureAwait(false);
        return entity?.ToModel();
    }

    /// <summary>
    /// 按设备名称和类型查找设备.
    /// </summary>
    public async Task<DeviceInfo?> GetDeviceByNameAndTypeAsync(string deviceName, int deviceType, CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            $"SELECT {DeviceEntityRepository<ConnectorDatabase>.AllFields} FROM \"Devices\" WHERE \"Name\" = @name AND \"Type\" = @type LIMIT 1");
        cmd.Parameters.AddWithValue("@name", deviceName);
        cmd.Parameters.AddWithValue("@type", deviceType);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return DeviceEntityRepository<ConnectorDatabase>.MapToEntity(reader).ToModel();
        }

        return null;
    }

    /// <summary>
    /// 获取所有设备.
    /// </summary>
    public async Task<IReadOnlyList<DeviceInfo>> GetAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _deviceRepo.GetAllAsync(_database, cancellationToken).ConfigureAwait(false);
        return entities.Select(e => e.ToModel()).ToList();
    }

    /// <summary>
    /// 更新设备心跳时间.
    /// </summary>
    public async Task UpdateHeartbeatAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var cmd = _database.CreateCommand(
            "UPDATE \"Devices\" SET \"LastHeartbeat\" = @heartbeat, \"IsOnline\" = 1 WHERE \"Id\" = @id");
        cmd.Parameters.AddWithValue("@heartbeat", now);
        cmd.Parameters.AddWithValue("@id", deviceId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 标记设备离线.
    /// </summary>
    public async Task SetDeviceOfflineAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            "UPDATE \"Devices\" SET \"IsOnline\" = 0 WHERE \"Id\" = @id");
        cmd.Parameters.AddWithValue("@id", deviceId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 标记心跳超时的设备为离线.
    /// </summary>
    public async Task<int> SetTimeoutDevicesOfflineAsync(int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timeoutSeconds;
        await using var cmd = _database.CreateCommand(
            "UPDATE \"Devices\" SET \"IsOnline\" = 0 WHERE \"IsOnline\" = 1 AND \"LastHeartbeat\" < @cutoff");
        cmd.Parameters.AddWithValue("@cutoff", cutoff);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取所有在线设备.
    /// </summary>
    public async Task<IReadOnlyList<DeviceInfo>> GetOnlineDevicesAsync(CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            $"SELECT {DeviceEntityRepository<ConnectorDatabase>.AllFields} FROM \"Devices\" WHERE \"IsOnline\" = 1");
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<DeviceInfo>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(DeviceEntityRepository<ConnectorDatabase>.MapToEntity(reader).ToModel());
        }

        return results;
    }
}
