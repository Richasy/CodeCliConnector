// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Console.Models;

/// <summary>
/// 连接器配置.
/// </summary>
internal sealed class ConnectorSettings
{
    /// <summary>
    /// 服务器地址（如 http://example.com:5000）.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// 预共享密钥.
    /// </summary>
    public string PreSharedKey { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称.
    /// </summary>
    public string DeviceName { get; set; } = Environment.MachineName;

    /// <summary>
    /// 本地 Hook 监听端口.
    /// </summary>
    public int HookListenerPort { get; set; } = 12341;

    /// <summary>
    /// 已注册的设备 ID.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// 访问令牌.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// 令牌过期时间（Unix 时间戳秒）.
    /// </summary>
    public long TokenExpiresAt { get; set; }

    /// <summary>
    /// 权限请求转发延迟秒数（等待用户本地操作，0 表示立即转发）.
    /// </summary>
    public int PermissionForwardDelaySeconds { get; set; } = 15;

    /// <summary>
    /// 是否注册为 Windows 服务.
    /// </summary>
    public bool RunAsWindowsService { get; set; }
}
