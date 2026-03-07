// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Console.Services;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace CodeCliConnector.Console.Commands;

/// <summary>
/// run 命令：启动连接器主循环.
/// </summary>
internal sealed class RunCommand
{
    private readonly ConfigService _configService;
    private readonly ServerConnectionService _serverConnection;
    private readonly HookListenerService _hookListener;
    private readonly HookConfigurationService _hookConfiguration;
    private readonly ILogger<RunCommand> _logger;

    public RunCommand(
        ConfigService configService,
        ServerConnectionService serverConnection,
        HookListenerService hookListener,
        HookConfigurationService hookConfiguration,
        ILogger<RunCommand> logger)
    {
        _configService = configService;
        _serverConnection = serverConnection;
        _hookListener = hookListener;
        _hookConfiguration = hookConfiguration;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        // 检查配置
        if (!_configService.IsConfigured())
        {
            AnsiConsole.MarkupLine("[red]未找到有效的配置，请先运行 ccc config 进行配置。[/]");
            return 1;
        }

        // 注册或刷新令牌
        if (!_configService.IsRegistered() || _configService.IsTokenExpired())
        {
            AnsiConsole.MarkupLine("[yellow]正在注册设备...[/]");
            if (!await _serverConnection.RegisterDeviceAsync(cancellationToken).ConfigureAwait(false))
            {
                AnsiConsole.MarkupLine("[red]设备注册失败，请检查服务器地址和预共享密钥。[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]设备注册成功。[/]");
        }

        // 安装 hook 配置
        if (!await _hookConfiguration.IsConfiguredAsync(cancellationToken).ConfigureAwait(false))
        {
            AnsiConsole.MarkupLine("[yellow]正在安装 Claude Code Hook 配置...[/]");
            await _hookConfiguration.InstallAsync(cancellationToken).ConfigureAwait(false);
            AnsiConsole.MarkupLine("[green]Hook 配置已安装。[/]");
        }

        // 启动 Hook 监听器
        _hookListener.Start(cancellationToken);
        AnsiConsole.MarkupLine($"[green]Hook 监听器已启动 (端口 {_configService.Settings.HookListenerPort})[/]");

        // 连接状态显示
        _serverConnection.ConnectionStateChanged += connected =>
        {
            if (connected)
            {
                AnsiConsole.MarkupLine($"[green]{DateTime.Now:HH:mm:ss} WebSocket 已连接[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]{DateTime.Now:HH:mm:ss} WebSocket 已断开，等待重连...[/]");
            }
        };

        AnsiConsole.MarkupLine("[green]连接器已启动，按 Ctrl+C 退出。[/]");
        AnsiConsole.MarkupLine("[dim]Device ID: {0}[/]", _configService.Settings.DeviceId ?? "N/A");
        AnsiConsole.MarkupLine("[dim]Server: {0}[/]", _configService.Settings.ServerUrl);
        AnsiConsole.WriteLine();

        // 启动带自动重连的 WebSocket 连接循环
        try
        {
            await _serverConnection.RunWithReconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }

        // 清理
        AnsiConsole.MarkupLine("[yellow]正在停止...[/]");
        await _hookListener.StopAsync().ConfigureAwait(false);
        await _serverConnection.DisconnectAsync().ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]连接器已停止。[/]");
        return 0;
    }
}
