// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models.Constants;

namespace CodeCliConnector.Core.Models;

/// <summary>
/// 设备信息.
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>
    /// 设备唯一标识.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// 设备名称.
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// 设备类型.
    /// </summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>
    /// 是否在线.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// 最后心跳时间（Unix 时间戳秒）.
    /// </summary>
    public long LastHeartbeat { get; set; }

    /// <summary>
    /// 注册时间（Unix 时间戳秒）.
    /// </summary>
    public long RegisteredAt { get; set; }
}
