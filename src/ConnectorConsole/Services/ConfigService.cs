// Copyright (c) Richasy. All rights reserved.

using System.Text.Json;
using CodeCliConnector.Console.Models;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Console.Services;

/// <summary>
/// 配置管理服务.
/// </summary>
internal sealed class ConfigService
{
    private static string s_configDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ccc");

    private static string s_userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private readonly ILogger<ConfigService> _logger;
    private ConnectorSettings? _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigService"/> class.
    /// </summary>
    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取当前配置.
    /// </summary>
    public ConnectorSettings Settings => _settings ?? new ConnectorSettings();

    /// <summary>
    /// 获取日志目录路径.
    /// </summary>
    public static string LogDir => Path.Combine(s_configDir, "logs");

    /// <summary>
    /// 获取用户主目录路径.
    /// </summary>
    public static string UserHome => s_userHome;

    /// <summary>
    /// 获取配置目录路径.
    /// </summary>
    public static string ConfigDir => s_configDir;

    /// <summary>
    /// 覆盖配置目录（用于 Windows 服务以 LocalSystem 运行时指定实际用户路径）.
    /// </summary>
    public static void OverrideConfigDir(string configDir)
    {
        s_configDir = configDir;
    }

    /// <summary>
    /// 覆盖用户主目录（用于 Windows 服务以 LocalSystem 运行时指定实际用户路径）.
    /// </summary>
    public static void OverrideUserHome(string userHome)
    {
        s_userHome = userHome;
    }

    /// <summary>
    /// 加载配置.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(s_configDir, "config.json");
        if (!File.Exists(configPath))
        {
            _logger.LogInformation("配置文件不存在，使用默认配置");
            _settings = new ConnectorSettings();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            _settings = JsonSerializer.Deserialize(json, ConsoleJsonContext.Default.ConnectorSettings) ?? new ConnectorSettings();
            _logger.LogInformation("配置已加载: {Path}", configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配置失败，使用默认配置");
            _settings = new ConnectorSettings();
        }
    }

    /// <summary>
    /// 保存配置.
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(s_configDir);
        var configPath = Path.Combine(s_configDir, "config.json");
        var json = JsonSerializer.Serialize(Settings, ConsoleJsonContext.Default.ConnectorSettings);
        await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("配置已保存: {Path}", configPath);
    }

    /// <summary>
    /// 更新配置.
    /// </summary>
    public async Task UpdateAsync(Action<ConnectorSettings> configure, CancellationToken cancellationToken = default)
    {
        _settings ??= new ConnectorSettings();
        configure(_settings);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 配置是否完整（具备连接所需的基本信息）.
    /// </summary>
    public bool IsConfigured()
        => !string.IsNullOrEmpty(Settings.ServerUrl) && !string.IsNullOrEmpty(Settings.PreSharedKey);

    /// <summary>
    /// 是否已注册设备.
    /// </summary>
    public bool IsRegistered()
        => !string.IsNullOrEmpty(Settings.DeviceId) && !string.IsNullOrEmpty(Settings.AccessToken);

    /// <summary>
    /// 令牌是否已过期.
    /// </summary>
    public bool IsTokenExpired()
        => Settings.TokenExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
