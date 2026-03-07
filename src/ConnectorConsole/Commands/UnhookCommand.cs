// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Console.Services;
using Spectre.Console;

namespace CodeCliConnector.Console.Commands;

/// <summary>
/// unhook 命令：清除 Claude Code 中的 hook 配置.
/// </summary>
internal sealed class UnhookCommand
{
    private readonly HookConfigurationService _hookConfiguration;

    public UnhookCommand(HookConfigurationService hookConfiguration)
    {
        _hookConfiguration = hookConfiguration;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]正在卸载 Hook 配置...[/]");
        await _hookConfiguration.UninstallAsync(cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]Hook 配置已卸载。[/]");
        return 0;
    }
}
