// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using CodeCliConnector.Storage.Database;

namespace CodeCliConnector.Storage;

/// <summary>
/// 连接器数据服务 - 消息操作.
/// </summary>
public sealed partial class ConnectorDataService
{
    /// <summary>
    /// 创建消息.
    /// </summary>
    public async Task CreateMessageAsync(MessageInfo message, CancellationToken cancellationToken = default)
    {
        var entity = MessageEntity.FromModel(message);
        await _messageRepo.UpsertAsync(_database, entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取设备的待投递消息.
    /// </summary>
    public async Task<IReadOnlyList<MessageInfo>> GetPendingMessagesAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            $"SELECT {MessageEntityRepository<ConnectorDatabase>.AllFields} FROM \"Messages\" WHERE \"TargetDeviceId\" = @deviceId AND \"Status\" = @status ORDER BY \"CreatedAt\" ASC");
        cmd.Parameters.AddWithValue("@deviceId", deviceId);
        cmd.Parameters.AddWithValue("@status", (int)MessageStatus.Pending);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<MessageInfo>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MessageEntityRepository<ConnectorDatabase>.MapToEntity(reader).ToModel());
        }

        return results;
    }

    /// <summary>
    /// 标记消息为已投递.
    /// </summary>
    public async Task MarkMessageDeliveredAsync(string messageId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            "UPDATE \"Messages\" SET \"Status\" = @status WHERE \"Id\" = @id");
        cmd.Parameters.AddWithValue("@status", (int)MessageStatus.Delivered);
        cmd.Parameters.AddWithValue("@id", messageId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 批量标记消息为已投递.
    /// </summary>
    public async Task MarkMessagesDeliveredAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in messageIds)
        {
            await MarkMessageDeliveredAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 标记消息为已处理.
    /// </summary>
    public async Task MarkMessageProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            "UPDATE \"Messages\" SET \"Status\" = @status WHERE \"Id\" = @id");
        cmd.Parameters.AddWithValue("@status", (int)MessageStatus.Processed);
        cmd.Parameters.AddWithValue("@id", messageId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 标记过期消息.
    /// </summary>
    public async Task<int> ExpireMessagesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var cmd = _database.CreateCommand(
            "UPDATE \"Messages\" SET \"Status\" = @expiredStatus WHERE \"Status\" IN (@pending, @delivered) AND \"ExpiresAt\" > 0 AND \"ExpiresAt\" < @now");
        cmd.Parameters.AddWithValue("@expiredStatus", (int)MessageStatus.Expired);
        cmd.Parameters.AddWithValue("@pending", (int)MessageStatus.Pending);
        cmd.Parameters.AddWithValue("@delivered", (int)MessageStatus.Delivered);
        cmd.Parameters.AddWithValue("@now", now);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除已处理或已过期的旧消息.
    /// </summary>
    public async Task<int> CleanupOldMessagesAsync(int maxAgeDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-maxAgeDays).ToUnixTimeSeconds();
        await using var cmd = _database.CreateCommand(
            "DELETE FROM \"Messages\" WHERE \"Status\" IN (@processed, @expired) AND \"CreatedAt\" < @cutoff");
        cmd.Parameters.AddWithValue("@processed", (int)MessageStatus.Processed);
        cmd.Parameters.AddWithValue("@expired", (int)MessageStatus.Expired);
        cmd.Parameters.AddWithValue("@cutoff", cutoff);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取消息.
    /// </summary>
    public async Task<MessageInfo?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var entity = await _messageRepo.GetByIdAsync(_database, messageId, cancellationToken).ConfigureAwait(false);
        return entity?.ToModel();
    }
}
