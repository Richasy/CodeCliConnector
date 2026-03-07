// Copyright (c) Richasy. All rights reserved.

using System.Diagnostics;
using CodeCliConnector.Console.Services;
using Spectre.Console;

namespace CodeCliConnector.Console.Commands;

/// <summary>
/// log 命令：打开日志文件夹.
/// </summary>
internal sealed class LogCommand
{
    public static Task<int> ExecuteAsync()
    {
        var logDir = ConfigService.LogDir;
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        AnsiConsole.MarkupLine("[dim]日志目录: {0}[/]", Markup.Escape(logDir));

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]无法打开日志目录: {0}[/]", Markup.Escape(ex.Message));
            return Task.FromResult(1);
        }

        return Task.FromResult(0);
    }
}
