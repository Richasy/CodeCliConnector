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
            return RelaunchAsAdmin("service-install");
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            AnsiConsole.MarkupLine("[red]无法获取当前可执行文件路径。[/]");
            WaitForKey();
            return 1;
        }

        // 先停止并删除旧服务（如果存在）
        await RunScAsync($"stop {ServiceName}", cancellationToken).ConfigureAwait(false);
        await RunScAsync($"delete {ServiceName}", cancellationToken).ConfigureAwait(false);

        // 以 LocalSystem 运行，通过参数传入当前用户的配置目录和主目录
        var configDir = ConfigService.ConfigDir;
        var userHome = ConfigService.UserHome;
        var binPath = $"\"{exePath}\" run --config-dir \"{configDir}\" --user-home \"{userHome}\"";

        AnsiConsole.MarkupLine($"[blue]配置目录: [bold]{configDir}[/][/]");
        AnsiConsole.MarkupLine($"[blue]用户目录: [bold]{userHome}[/][/]");

        var createResult = await RunScAsync(
            $"create {ServiceName} binPath= \"{binPath}\" start= auto DisplayName= \"{ServiceDisplayName}\"",
            cancellationToken).ConfigureAwait(false);

        if (createResult != 0)
        {
            AnsiConsole.MarkupLine("[red]创建服务失败。[/]");
            WaitForKey();
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
            AnsiConsole.MarkupLine("[yellow]服务已创建，但启动失败。请检查 ccc log 查看详细日志。[/]");
            WaitForKey();
            return 1;
        }

        await _configService.UpdateAsync(s => s.RunAsWindowsService = true, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]服务已安装并启动。[/]");
        WaitForKey();
        return 0;
    }

    /// <summary>
    /// 卸载 Windows 服务.
    /// </summary>
    public async Task<int> UninstallAsync(CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
        {
            return RelaunchAsAdmin("service-uninstall");
        }

        await RunScAsync($"stop {ServiceName}", cancellationToken).ConfigureAwait(false);
        var deleteResult = await RunScAsync($"delete {ServiceName}", cancellationToken).ConfigureAwait(false);

        if (deleteResult != 0)
        {
            AnsiConsole.MarkupLine("[red]删除服务失败。[/]");
            WaitForKey();
            return 1;
        }

        await _configService.UpdateAsync(s => s.RunAsWindowsService = false, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]服务已卸载。[/]");
        WaitForKey();
        return 0;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 以管理员身份重新启动当前进程，触发 UAC 提权弹窗.
    /// </summary>
    private static int RelaunchAsAdmin(string command)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            AnsiConsole.MarkupLine("[red]无法获取当前可执行文件路径。[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[yellow]需要管理员权限，正在请求提权...[/]");

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = command,
                Verb = "runas",
                UseShellExecute = true,
            });

            process?.WaitForExit();
            return process?.ExitCode ?? 1;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 用户取消了 UAC 弹窗
            AnsiConsole.MarkupLine("[yellow]已取消提权操作。[/]");
            return 1;
        }
    }

    /// <summary>
    /// 提权窗口执行完毕后暂停，让用户看到结果.
    /// </summary>
    private static void WaitForKey()
    {
        if (IsAdministrator() && !System.Console.IsInputRedirected)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]按任意键关闭窗口...[/]");
            System.Console.ReadKey(true);
        }
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
