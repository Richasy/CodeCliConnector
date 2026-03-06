// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Core.Models.Responses;

/// <summary>
/// 认证响应.
/// </summary>
public sealed class AuthResponse
{
    /// <summary>
    /// 访问令牌.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// 设备 ID.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// 过期时间（Unix 时间戳秒）.
    /// </summary>
    public long ExpiresAt { get; set; }
}
