// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Console.Commands;
using CodeCliConnector.Console.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;

var command = args.Length > 0 ? args[0] : "run";

// 解析 --config-dir 和 --user-home 参数（Windows 服务模式由 ServiceCommand 传入）
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--config-dir")
    {
        ConfigService.OverrideConfigDir(args[i + 1]);
    }
    else if (args[i] == "--user-home")
    {
        ConfigService.OverrideUserHome(args[i + 1]);
    }
}

// Serilog 需要在解析参数后初始化（ConfigDir 可能已被覆盖）
var logDir = ConfigService.LogDir;
Directory.CreateDirectory(logDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        Path.Combine(logDir, "ccc-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// "run" 命令使用 Host 模式，支持 Windows Service 和控制台两种运行方式
if (command == "run")
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddLogging(b => b.AddSerilog(dispose: true));
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "CodeCliConnector";
    });

    builder.Services.AddSingleton<ConfigService>();
    builder.Services.AddSingleton<PendingRequestTracker>();
    builder.Services.AddSingleton<ServerConnectionService>();
    builder.Services.AddSingleton<HookListenerService>();
    builder.Services.AddSingleton<HookConfigurationService>();
    builder.Services.AddHostedService<ConnectorWorker>();

    var host = builder.Build();

    // 加载配置
    var configService = host.Services.GetRequiredService<ConfigService>();
    await configService.LoadAsync().ConfigureAwait(false);

    if (!configService.IsConfigured())
    {
        AnsiConsole.MarkupLine("[red]未找到有效的配置，请先运行 ccc config 进行配置。[/]");
        return 1;
    }

    // 控制台模式下显示启动信息
    if (!Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
    {
        AnsiConsole.MarkupLine($"[green]Hook 监听器已启动 (端口 {configService.Settings.HookListenerPort})[/]");
        AnsiConsole.MarkupLine("[green]连接器已启动，按 Ctrl+C 退出。[/]");
        AnsiConsole.MarkupLine("[dim]Device ID: {0}[/]", configService.Settings.DeviceId ?? "N/A");
        AnsiConsole.MarkupLine("[dim]Server: {0}[/]", configService.Settings.ServerUrl);
        AnsiConsole.WriteLine();
    }

    await host.RunAsync().ConfigureAwait(false);
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
    return 0;
}

// 其他命令使用轻量 DI 容器
var services = new ServiceCollection();
services.AddLogging(b =>
{
    b.SetMinimumLevel(LogLevel.Debug);
    b.AddSerilog(dispose: true);
});

services.AddSingleton<ConfigService>();
services.AddSingleton<PendingRequestTracker>();
services.AddSingleton<ServerConnectionService>();
services.AddSingleton<HookListenerService>();
services.AddSingleton<HookConfigurationService>();
services.AddSingleton<ConfigCommand>();
services.AddSingleton<ReconnectCommand>();
services.AddSingleton<ListDevicesCommand>();
services.AddSingleton<UnhookCommand>();
services.AddSingleton<LogCommand>();
services.AddSingleton<ServiceCommand>();

var provider = services.BuildServiceProvider();
var cfg = provider.GetRequiredService<ConfigService>();
await cfg.LoadAsync().ConfigureAwait(false);

using var cts = new CancellationTokenSource();
System.Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var exitCode = command switch
{
    "config" => await provider.GetRequiredService<ConfigCommand>().ExecuteAsync(cts.Token).ConfigureAwait(false),
    "reconnect" => await provider.GetRequiredService<ReconnectCommand>().ExecuteAsync(cts.Token).ConfigureAwait(false),
    "list-devices" => await provider.GetRequiredService<ListDevicesCommand>().ExecuteAsync(cts.Token).ConfigureAwait(false),
    "unhook" => await provider.GetRequiredService<UnhookCommand>().ExecuteAsync(cts.Token).ConfigureAwait(false),
    "service-install" => await provider.GetRequiredService<ServiceCommand>().InstallAsync(cts.Token).ConfigureAwait(false),
    "service-uninstall" => await provider.GetRequiredService<ServiceCommand>().UninstallAsync(cts.Token).ConfigureAwait(false),
    "log" => await LogCommand.ExecuteAsync().ConfigureAwait(false),
    "help" or "--help" or "-h" => ShowHelp(),
    _ => ShowUnknownCommand(command),
};

await Log.CloseAndFlushAsync().ConfigureAwait(false);

if (provider is IAsyncDisposable asyncDisposable)
{
    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
}

return exitCode;

static int ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]CodeCliConnector (ccc)[/] - Claude Code 远程连接器");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]用法:[/] ccc [command]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]命令:[/]");
    AnsiConsole.MarkupLine("  [green]run[/]               启动连接器（默认命令）");
    AnsiConsole.MarkupLine("  [green]config[/]            配置服务器地址和密钥");
    AnsiConsole.MarkupLine("  [green]reconnect[/]         重新注册设备获取新令牌");
    AnsiConsole.MarkupLine("  [green]list-devices[/]      列出所有设备及在线状态");
    AnsiConsole.MarkupLine("  [green]unhook[/]            卸载 Claude Code Hook 配置");
    AnsiConsole.MarkupLine("  [green]service-install[/]   注册为 Windows 服务（需管理员权限）");
    AnsiConsole.MarkupLine("  [green]service-uninstall[/] 卸载 Windows 服务（需管理员权限）");
    AnsiConsole.MarkupLine("  [green]log[/]               打开日志文件夹");
    AnsiConsole.MarkupLine("  [green]help[/]              显示此帮助信息");
    return 0;
}

static int ShowUnknownCommand(string cmd)
{
    AnsiConsole.MarkupLine("[red]未知命令: {0}[/]", Markup.Escape(cmd));
    AnsiConsole.MarkupLine("运行 [green]ccc help[/] 查看可用命令。");
    return 1;
}
