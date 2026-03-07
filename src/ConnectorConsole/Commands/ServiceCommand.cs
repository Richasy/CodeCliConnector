// Copyright (c) Richasy. All rights reserved.

using System.Diagnostics;
using System.Security.Principal;
using CodeCliConnector.Console.Services;
using Spectre.Console;

namespace CodeCliConnector.Console.Commands;

/// <summary>
/// service 命令：注册或卸载 Windows 服务.
/// </summary>
internal sealed class ServiceCommand
{
    private const string ServiceName = "CodeCliConnector";
    private const string ServiceDisplayName = "CodeCliConnector (ccc)";
    private const string ServiceDescription = "Claude Code 远程连接器服务，转发 Hook 通知和权限请求";

    private readonly ConfigService _configService;

    public ServiceCommand(ConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// 安装 Windows 服务.
    /// </summary>
    public async Task<int> InstallAsync(CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
        {
            AnsiConsole.MarkupLine("[red]需要管理员权限，请以管理员身份运行。[/]");
            return 1;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            AnsiConsole.MarkupLine("[red]无法获取当前可执行文件路径。[/]");
            return 1;
        }

        // 先停止并删除旧服务（如果存在）
        await RunScAsync($"stop {ServiceName}", cancellationToken).ConfigureAwait(false);
        await RunScAsync($"delete {ServiceName}", cancellationToken).ConfigureAwait(false);

        // 创建服务，binPath 指向当前可执行文件 + run 命令
        var binPath = $"\"{exePath}\" run";
        var createResult = await RunScAsync(
            $"create {ServiceName} binPath= \"{binPath}\" start= auto DisplayName= \"{ServiceDisplayName}\"",
            cancellationToken).ConfigureAwait(false);

        if (createResult != 0)
        {
            AnsiConsole.MarkupLine("[red]创建服务失败。[/]");
            return 1;
        }

        // 设置描述
        await RunScAsync($"description {ServiceName} \"{ServiceDescription}\"", cancellationToken).ConfigureAwait(false);

        // 配置服务恢复策略：失败后自动重启
        await RunScAsync($"failure {ServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000", cancellationToken).ConfigureAwait(false);

        // 启动服务
        var startResult = await RunScAsync($"start {ServiceName}", cancellationToken).ConfigureAwait(false);
        if (startResult != 0)
        {
            AnsiConsole.MarkupLine("[yellow]服务已创建，但启动失败。请确保已运行 ccc config 完成配置。[/]");
            return 1;
        }

        await _configService.UpdateAsync(s => s.RunAsWindowsService = true, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]服务已安装并启动。[/]");
        return 0;
    }

    /// <summary>
    /// 卸载 Windows 服务.
    /// </summary>
    public async Task<int> UninstallAsync(CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
        {
            AnsiConsole.MarkupLine("[red]需要管理员权限，请以管理员身份运行。[/]");
            return 1;
        }

        await RunScAsync($"stop {ServiceName}", cancellationToken).ConfigureAwait(false);
        var deleteResult = await RunScAsync($"delete {ServiceName}", cancellationToken).ConfigureAwait(false);

        if (deleteResult != 0)
        {
            AnsiConsole.MarkupLine("[red]删除服务失败。[/]");
            return 1;
        }

        await _configService.UpdateAsync(s => s.RunAsWindowsService = false, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]服务已卸载。[/]");
        return 0;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task<int> RunScAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}
