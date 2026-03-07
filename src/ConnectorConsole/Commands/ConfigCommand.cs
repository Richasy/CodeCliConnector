// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Console.Services;
using Spectre.Console;

namespace CodeCliConnector.Console.Commands;

/// <summary>
/// config 命令：交互式配置.
/// </summary>
internal sealed class ConfigCommand
{
    private readonly ConfigService _configService;

    public ConfigCommand(ConfigService configService)
    {
        _configService = configService;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold]连接器配置[/]");
        AnsiConsole.WriteLine();

        var currentSettings = _configService.Settings;

        var serverUrl = AnsiConsole.Prompt(
            new TextPrompt<string>("服务器地址:")
                .DefaultValue(currentSettings.ServerUrl)
                .AllowEmpty());

        var psk = AnsiConsole.Prompt(
            new TextPrompt<string>("预共享密钥:")
                .DefaultValue(currentSettings.PreSharedKey)
                .Secret());

        var deviceName = AnsiConsole.Prompt(
            new TextPrompt<string>("设备名称:")
                .DefaultValue(currentSettings.DeviceName)
                .AllowEmpty());

        var port = AnsiConsole.Prompt(
            new TextPrompt<int>("Hook 监听端口:")
                .DefaultValue(currentSettings.HookListenerPort));

        var runAsService = await AnsiConsole.ConfirmAsync("注册为 Windows 服务（开机自启）?", currentSettings.RunAsWindowsService, cancellationToken).ConfigureAwait(false);

        await _configService.UpdateAsync(s =>
        {
            s.ServerUrl = string.IsNullOrWhiteSpace(serverUrl) ? currentSettings.ServerUrl : serverUrl;
            s.PreSharedKey = string.IsNullOrWhiteSpace(psk) ? currentSettings.PreSharedKey : psk;
            s.DeviceName = string.IsNullOrWhiteSpace(deviceName) ? currentSettings.DeviceName : deviceName;
            s.HookListenerPort = port;
            s.RunAsWindowsService = runAsService;
        }, cancellationToken).ConfigureAwait(false);

        AnsiConsole.MarkupLine("[green]配置已保存。[/]");
        return 0;
    }
}
