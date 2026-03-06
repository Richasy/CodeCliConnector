// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Core.Models;

/// <summary>
/// 访问令牌信息.
/// </summary>
public sealed class AccessTokenInfo
{
    /// <summary>
    /// 令牌 ID.
    /// </summary>
    public required string TokenId { get; set; }

    /// <summary>
    /// 令牌 SHA256 哈希.
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// 关联设备 ID.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// 创建时间（Unix 时间戳秒）.
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// 过期时间（Unix 时间戳秒）.
    /// </summary>
    public long ExpiresAt { get; set; }

    /// <summary>
    /// 是否已吊销.
    /// </summary>
    public bool IsRevoked { get; set; }
}
