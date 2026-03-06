// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models;
using CodeCliConnector.Storage.Database;

namespace CodeCliConnector.Storage;

/// <summary>
/// 连接器数据服务 - 令牌操作.
/// </summary>
public sealed partial class ConnectorDataService
{
    /// <summary>
    /// 创建访问令牌.
    /// </summary>
    public async Task CreateTokenAsync(AccessTokenInfo token, CancellationToken cancellationToken = default)
    {
        var entity = AccessTokenEntity.FromModel(token);
        await _tokenRepo.UpsertAsync(_database, entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按哈希查找有效令牌.
    /// </summary>
    public async Task<AccessTokenInfo?> GetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var cmd = _database.CreateCommand(
            $"SELECT {AccessTokenEntityRepository<ConnectorDatabase>.AllFields} FROM \"AccessTokens\" WHERE \"TokenHash\" = @hash AND \"IsRevoked\" = 0 AND \"ExpiresAt\" > @now");
        cmd.Parameters.AddWithValue("@hash", tokenHash);
        cmd.Parameters.AddWithValue("@now", now);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return AccessTokenEntityRepository<ConnectorDatabase>.MapToEntity(reader).ToModel();
        }

        return null;
    }

    /// <summary>
    /// 吊销令牌.
    /// </summary>
    public async Task RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            "UPDATE \"AccessTokens\" SET \"IsRevoked\" = 1 WHERE \"Id\" = @id");
        cmd.Parameters.AddWithValue("@id", tokenId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 吊销设备的所有令牌.
    /// </summary>
    public async Task RevokeAllTokensForDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _database.CreateCommand(
            "UPDATE \"AccessTokens\" SET \"IsRevoked\" = 1 WHERE \"DeviceId\" = @deviceId");
        cmd.Parameters.AddWithValue("@deviceId", deviceId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 清理过期令牌.
    /// </summary>
    public async Task<int> CleanupExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var cmd = _database.CreateCommand(
            "DELETE FROM \"AccessTokens\" WHERE \"ExpiresAt\" < @now OR \"IsRevoked\" = 1");
        cmd.Parameters.AddWithValue("@now", now);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
