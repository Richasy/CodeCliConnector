// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Console.Services;
using Spectre.Console;

namespace CodeCliConnector.Console.Commands;

/// <summary>
/// reconnect 命令：重新注册设备获取新令牌.
/// </summary>
internal sealed class ReconnectCommand
{
    private readonly ConfigService _configService;
    private readonly ServerConnectionService _serverConnection;

    public ReconnectCommand(ConfigService configService, ServerConnectionService serverConnection)
    {
        _configService = configService;
        _serverConnection = serverConnection;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_configService.IsConfigured())
        {
            AnsiConsole.MarkupLine("[red]未找到有效的配置，请先运行 ccc config 进行配置。[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[yellow]正在重新注册设备...[/]");
        if (await _serverConnection.RegisterDeviceAsync(cancellationToken).ConfigureAwait(false))
        {
            AnsiConsole.MarkupLine("[green]设备注册成功。[/]");
            AnsiConsole.MarkupLine("[dim]Device ID: {0}[/]", _configService.Settings.DeviceId ?? "N/A");
            return 0;
        }

        AnsiConsole.MarkupLine("[red]设备注册失败，请检查服务器地址和预共享密钥。[/]");
        return 1;
    }
}
