// Copyright (c) Richasy. All rights reserved.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Console.Services;

/// <summary>
/// 管理 Claude Code 的 hook 配置（~/.claude/settings.json）.
/// </summary>
internal sealed class HookConfigurationService
{
    private const string NotificationCommand = "curl -s -X POST http://localhost:{0}/notification -H \"Content-Type: application/json\" -d @-";
    private const string PermissionCommand = "curl -s -X POST http://localhost:{0}/permission -H \"Content-Type: application/json\" -d @-";
    private const int PermissionTimeout = 21600;

    private static string ClaudeSettingsPath => Path.Combine(ConfigService.UserHome, ".claude", "settings.json");

    private readonly ConfigService _configService;
    private readonly ILogger<HookConfigurationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HookConfigurationService"/> class.
    /// </summary>
    public HookConfigurationService(ConfigService configService, ILogger<HookConfigurationService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 安装 hook 配置到 Claude Code settings.json.
    /// </summary>
    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        var port = _configService.Settings.HookListenerPort;
        var root = await LoadSettingsNodeAsync(cancellationToken).ConfigureAwait(false);

        var hooks = root["hooks"]?.AsObject() ?? [];
        if (root["hooks"] is null)
        {
            root["hooks"] = hooks;
        }

        // Notification hook
        var notificationGroup = CreateHookGroup(
            string.Empty,
            string.Format(NotificationCommand, port),
            timeout: null,
            isAsync: true);
        hooks["Notification"] = notificationGroup;

        // PermissionRequest hook
        var permissionGroup = CreateHookGroup(
            string.Empty,
            string.Format(PermissionCommand, port),
            timeout: PermissionTimeout,
            isAsync: false);
        hooks["PermissionRequest"] = permissionGroup;

        await SaveSettingsNodeAsync(root, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Hook 配置已安装到 {Path}", ClaudeSettingsPath);
    }

    /// <summary>
    /// 卸载 hook 配置.
    /// </summary>
    public async Task UninstallAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadSettingsNodeAsync(cancellationToken).ConfigureAwait(false);
        var hooks = root["hooks"]?.AsObject();
        if (hooks is null)
        {
            _logger.LogInformation("未找到 hook 配置，无需卸载");
            return;
        }

        var port = _configService.Settings.HookListenerPort;
        var notifCmd = string.Format(NotificationCommand, port);
        var permCmd = string.Format(PermissionCommand, port);

        RemoveMatchingHook(hooks, "Notification", notifCmd);
        RemoveMatchingHook(hooks, "PermissionRequest", permCmd);

        await SaveSettingsNodeAsync(root, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Hook 配置已从 {Path} 卸载", ClaudeSettingsPath);
    }

    /// <summary>
    /// 检查 hook 是否已配置.
    /// </summary>
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ClaudeSettingsPath))
        {
            return false;
        }

        var root = await LoadSettingsNodeAsync(cancellationToken).ConfigureAwait(false);
        var hooks = root["hooks"]?.AsObject();
        if (hooks is null)
        {
            return false;
        }

        var port = _configService.Settings.HookListenerPort;
        var permCmd = string.Format(PermissionCommand, port);

        return HasMatchingHook(hooks, "PermissionRequest", permCmd);
    }

    private static JsonArray CreateHookGroup(string matcher, string command, int? timeout, bool isAsync)
    {
        var handler = new JsonObject { ["type"] = "command", ["command"] = command };
        if (timeout.HasValue)
        {
            handler["timeout"] = timeout.Value;
        }

        if (isAsync)
        {
            handler["async"] = true;
        }

        var group = new JsonObject
        {
            ["matcher"] = matcher,
            ["hooks"] = new JsonArray((JsonNode)handler),
        };
        return new JsonArray((JsonNode)group);
    }

    private static void RemoveMatchingHook(JsonObject hooks, string eventName, string command)
    {
        var groups = hooks[eventName]?.AsArray();
        if (groups is null)
        {
            return;
        }

        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i]?.AsObject();
            var handlers = group?["hooks"]?.AsArray();
            if (handlers is null)
            {
                continue;
            }

            for (var j = handlers.Count - 1; j >= 0; j--)
            {
                var cmd = handlers[j]?["command"]?.GetValue<string>();
                if (cmd == command)
                {
                    handlers.RemoveAt(j);
                }
            }

            if (handlers.Count == 0)
            {
                groups.RemoveAt(i);
            }
        }

        if (groups.Count == 0)
        {
            hooks.Remove(eventName);
        }
    }

    private static bool HasMatchingHook(JsonObject hooks, string eventName, string command)
    {
        var groups = hooks[eventName]?.AsArray();
        if (groups is null)
        {
            return false;
        }

        foreach (var groupNode in groups)
        {
            var handlers = groupNode?.AsObject()?["hooks"]?.AsArray();
            if (handlers is null)
            {
                continue;
            }

            foreach (var handler in handlers)
            {
                if (handler?["command"]?.GetValue<string>() == command)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task<JsonObject> LoadSettingsNodeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(ClaudeSettingsPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(ClaudeSettingsPath, cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(json)?.AsObject() ?? [];
    }

    private static async Task SaveSettingsNodeAsync(JsonObject root, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(ClaudeSettingsPath);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = root.ToJsonString(options);
        await File.WriteAllTextAsync(ClaudeSettingsPath, json, cancellationToken).ConfigureAwait(false);
    }
}
