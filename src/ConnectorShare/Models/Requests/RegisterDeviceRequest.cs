// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models.Constants;

namespace CodeCliConnector.Core.Models.Requests;

/// <summary>
/// 设备注册请求.
/// </summary>
public sealed class RegisterDeviceRequest
{
    /// <summary>
    /// 设备名称.
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// 设备类型.
    /// </summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>
    /// 预共享密钥.
    /// </summary>
    public required string PreSharedKey { get; set; }
}
