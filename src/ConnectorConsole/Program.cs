// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Console.Commands;
using CodeCliConnector.Console.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;

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

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddSerilog(dispose: true);
});

services.AddSingleton<ConfigService>();
services.AddSingleton<PendingRequestTracker>();
services.AddSingleton<ServerConnectionService>();
services.AddSingleton<HookListenerService>();
services.AddSingleton<HookConfigurationService>();
services.AddSingleton<RunCommand>();
services.AddSingleton<ConfigCommand>();
services.AddSingleton<ReconnectCommand>();
services.AddSingleton<ListDevicesCommand>();
services.AddSingleton<UnhookCommand>();
services.AddSingleton<LogCommand>();
services.AddSingleton<ServiceCommand>();

var provider = services.BuildServiceProvider();
var configService = provider.GetRequiredService<ConfigService>();
await configService.LoadAsync().ConfigureAwait(false);

using var cts = new CancellationTokenSource();
System.Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var command = args.Length > 0 ? args[0] : "run";

var exitCode = command switch
{
    "run" => await provider.GetRequiredService<RunCommand>().ExecuteAsync(cts.Token).ConfigureAwait(false),
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
    AnsiConsole.MarkupLine("  [green]run[/]           启动连接器（默认命令）");
    AnsiConsole.MarkupLine("  [green]config[/]        配置服务器地址和密钥");
    AnsiConsole.MarkupLine("  [green]reconnect[/]     重新注册设备获取新令牌");
    AnsiConsole.MarkupLine("  [green]list-devices[/]  列出所有设备及在线状态");
    AnsiConsole.MarkupLine("  [green]unhook[/]            卸载 Claude Code Hook 配置");
    AnsiConsole.MarkupLine("  [green]service-install[/]  注册为 Windows 服务（需管理员权限）");
    AnsiConsole.MarkupLine("  [green]service-uninstall[/]卸载 Windows 服务（需管理员权限）");
    AnsiConsole.MarkupLine("  [green]log[/]             打开日志文件夹");
    AnsiConsole.MarkupLine("  [green]help[/]          显示此帮助信息");
    return 0;
}

static int ShowUnknownCommand(string cmd)
{
    AnsiConsole.MarkupLine("[red]未知命令: {0}[/]", Markup.Escape(cmd));
    AnsiConsole.MarkupLine("运行 [green]ccc help[/] 查看可用命令。");
    return 1;
}
