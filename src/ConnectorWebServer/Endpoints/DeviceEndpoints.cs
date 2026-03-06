// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models.Responses;
using CodeCliConnector.Storage;

namespace CodeCliConnector.Server.Endpoints;

/// <summary>
/// 设备端点.
/// </summary>
public static class DeviceEndpoints
{
    /// <summary>
    /// 映射设备端点.
    /// </summary>
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices");

        group.MapGet("/status", GetStatusAsync)
            .WithName("GetDeviceStatus");
    }

    private static async Task<IResult> GetStatusAsync(
        ConnectorDataService dataService,
        CancellationToken cancellationToken)
    {
        var devices = await dataService.GetAllDevicesAsync(cancellationToken).ConfigureAwait(false);

        var response = devices.Select(d => new DeviceStatusResponse
        {
            DeviceId = d.DeviceId,
            DeviceName = d.DeviceName,
            DeviceType = d.DeviceType,
            IsOnline = d.IsOnline,
            LastHeartbeat = d.LastHeartbeat,
        }).ToList();

        return Results.Ok(response);
    }
}
