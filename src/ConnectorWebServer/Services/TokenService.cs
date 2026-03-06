// Copyright (c) Richasy. All rights reserved.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CodeCliConnector.Core.Models;
using CodeCliConnector.Server.Configuration;
using CodeCliConnector.Storage;
using Microsoft.Extensions.Options;

namespace CodeCliConnector.Server.Services;

/// <summary>
/// 令牌服务.
/// </summary>
public sealed class TokenService
{
    private readonly ConnectorDataService _dataService;
    private readonly ServerSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenService"/> class.
    /// </summary>
    public TokenService(ConnectorDataService dataService, IOptions<ServerSettings> settings)
    {
        _dataService = dataService;
        _settings = settings.Value;
    }

    /// <summary>
    /// 生成随机令牌.
    /// </summary>
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// 计算令牌 SHA256 哈希.
    /// </summary>
    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// 创建新的访问令牌.
    /// </summary>
    public async Task<(string RawToken, AccessTokenInfo TokenInfo)> CreateTokenAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateToken();
        var tokenHash = HashToken(rawToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var tokenInfo = new AccessTokenInfo
        {
            TokenId = Guid.NewGuid().ToString("N"),
            TokenHash = tokenHash,
            DeviceId = deviceId,
            CreatedAt = now,
            ExpiresAt = now + (_settings.TokenExpiryDays * 86400),
            IsRevoked = false,
        };

        await _dataService.CreateTokenAsync(tokenInfo, cancellationToken).ConfigureAwait(false);
        return (rawToken, tokenInfo);
    }

    /// <summary>
    /// 验证令牌.
    /// </summary>
    public async Task<AccessTokenInfo?> ValidateTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        var hash = HashToken(rawToken);
        return await _dataService.GetTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 刷新令牌.
    /// </summary>
    public async Task<(string RawToken, AccessTokenInfo TokenInfo)?> RefreshTokenAsync(
        string currentRawToken,
        CancellationToken cancellationToken = default)
    {
        var currentToken = await ValidateTokenAsync(currentRawToken, cancellationToken).ConfigureAwait(false);
        if (currentToken is null)
        {
            return null;
        }

        // 吊销旧令牌
        await _dataService.RevokeTokenAsync(currentToken.TokenId, cancellationToken).ConfigureAwait(false);

        // 创建新令牌
        return await CreateTokenAsync(currentToken.DeviceId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 吊销令牌.
    /// </summary>
    public async Task RevokeTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(rawToken);
        var token = await _dataService.GetTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (token is not null)
        {
            await _dataService.RevokeTokenAsync(token.TokenId, cancellationToken).ConfigureAwait(false);
        }
    }
}
