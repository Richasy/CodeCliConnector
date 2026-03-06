// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models.Constants;

namespace CodeCliConnector.Core.Models.Responses;

/// <summary>
/// 设备状态响应.
/// </summary>
public sealed class DeviceStatusResponse
{
    /// <summary>
    /// 设备 ID.
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
}
