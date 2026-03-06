// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Core.Models.Requests;

/// <summary>
/// 刷新令牌请求.
/// </summary>
public sealed class RefreshTokenRequest
{
    /// <summary>
    /// 当前有效的访问令牌.
    /// </summary>
    public required string AccessToken { get; set; }
}
