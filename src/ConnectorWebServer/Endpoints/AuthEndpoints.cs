// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Requests;
using CodeCliConnector.Core.Models.Responses;
using CodeCliConnector.Server.Configuration;
using CodeCliConnector.Server.Services;
using CodeCliConnector.Storage;
using Microsoft.Extensions.Options;

namespace CodeCliConnector.Server.Endpoints;

/// <summary>
/// 认证端点.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// 映射认证端点.
    /// </summary>
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("RegisterDevice");

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken");
    }

    private static async Task<IResult> RegisterAsync(
        RegisterDeviceRequest request,
        TokenService tokenService,
        ConnectorDataService dataService,
        IOptions<ServerSettings> settings,
        CancellationToken cancellationToken)
    {
        // 验证预共享密钥
        if (!string.Equals(request.PreSharedKey, settings.Value.PreSharedKey, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        // 查找是否已有相同 名称+类型 的设备
        var existing = await dataService.GetDeviceByNameAndTypeAsync(
            request.DeviceName, (int)request.DeviceType, cancellationToken).ConfigureAwait(false);

        string deviceId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (existing is not null)
        {
            // 复用已有设备，吊销旧令牌
            deviceId = existing.DeviceId;
            await dataService.RevokeAllTokensForDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);

            // 更新设备信息
            var updatedDevice = new DeviceInfo
            {
                DeviceId = deviceId,
                DeviceName = request.DeviceName,
                DeviceType = request.DeviceType,
                IsOnline = false,
                LastHeartbeat = now,
                RegisteredAt = existing.RegisteredAt,
            };
            await dataService.UpsertDeviceAsync(updatedDevice, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // 创建新设备
            deviceId = Guid.NewGuid().ToString("N");
            var device = new DeviceInfo
            {
                DeviceId = deviceId,
                DeviceName = request.DeviceName,
                DeviceType = request.DeviceType,
                IsOnline = false,
                LastHeartbeat = now,
                RegisteredAt = now,
            };
            await dataService.UpsertDeviceAsync(device, cancellationToken).ConfigureAwait(false);
        }

        // 创建新的访问令牌
        var (rawToken, tokenInfo) = await tokenService.CreateTokenAsync(deviceId, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new AuthResponse
        {
            AccessToken = rawToken,
            DeviceId = deviceId,
            ExpiresAt = tokenInfo.ExpiresAt,
        });
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
        var result = await tokenService.RefreshTokenAsync(request.AccessToken, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return Results.Unauthorized();
        }

        var (rawToken, tokenInfo) = result.Value;
        return Results.Ok(new AuthResponse
        {
            AccessToken = rawToken,
            DeviceId = tokenInfo.DeviceId,
            ExpiresAt = tokenInfo.ExpiresAt,
        });
    }
}
