// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Console.Services;
using Spectre.Console;

namespace CodeCliConnector.Console.Commands;

/// <summary>
/// list-devices 命令：列出所有设备及状态.
/// </summary>
internal sealed class ListDevicesCommand
{
    private readonly ConfigService _configService;
    private readonly ServerConnectionService _serverConnection;

    public ListDevicesCommand(ConfigService configService, ServerConnectionService serverConnection)
    {
        _configService = configService;
        _serverConnection = serverConnection;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_configService.IsConfigured() || !_configService.IsRegistered())
        {
            AnsiConsole.MarkupLine("[red]未注册，请先运行 ccc run 或 ccc reconnect。[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[yellow]正在查询设备列表...[/]");
        var devices = await _serverConnection.GetDeviceStatusAsync(cancellationToken).ConfigureAwait(false);
        if (devices is null || devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]没有已注册的设备。[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("设备 ID");
        table.AddColumn("名称");
        table.AddColumn("类型");
        table.AddColumn("状态");
        table.AddColumn("最后心跳");

        foreach (var device in devices)
        {
            var status = device.IsOnline ? "[green]在线[/]" : "[red]离线[/]";
            var heartbeat = device.LastHeartbeat > 0
                ? DateTimeOffset.FromUnixTimeSeconds(device.LastHeartbeat).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";
            table.AddRow(
                Markup.Escape(device.DeviceId),
                Markup.Escape(device.DeviceName),
                device.DeviceType.ToString(),
                status,
                heartbeat);
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
