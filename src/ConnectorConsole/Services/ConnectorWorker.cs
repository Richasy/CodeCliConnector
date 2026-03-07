// Copyright (c) Richasy. All rights reserved.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeCliConnector.Console.Services;

/// <summary>
/// 连接器后台工作服务，用于 Windows Service 和控制台模式.
/// </summary>
internal sealed class ConnectorWorker : BackgroundService
{
    private readonly ConfigService _configService;
    private readonly ServerConnectionService _serverConnection;
    private readonly HookListenerService _hookListener;
    private readonly HookConfigurationService _hookConfiguration;
    private readonly ILogger<ConnectorWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectorWorker"/> class.
    /// </summary>
    public ConnectorWorker(
        ConfigService configService,
        ServerConnectionService serverConnection,
        HookListenerService hookListener,
        HookConfigurationService hookConfiguration,
        ILogger<ConnectorWorker> logger)
    {
        _configService = configService;
        _serverConnection = serverConnection;
        _hookListener = hookListener;
        _hookConfiguration = hookConfiguration;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("连接器服务启动中...");

        // 检查配置
        if (!_configService.IsConfigured())
        {
            _logger.LogError("未找到有效的配置，请先运行 ccc config 进行配置");
            return;
        }

        // 注册或刷新令牌
        if (!_configService.IsRegistered() || _configService.IsTokenExpired())
        {
            _logger.LogInformation("正在注册设备...");
            if (!await _serverConnection.RegisterDeviceAsync(stoppingToken).ConfigureAwait(false))
            {
                _logger.LogError("设备注册失败，请检查服务器地址和预共享密钥");
                return;
            }

            _logger.LogInformation("设备注册成功");
        }

        // 安装 hook 配置
        if (!await _hookConfiguration.IsConfiguredAsync(stoppingToken).ConfigureAwait(false))
        {
            _logger.LogInformation("正在安装 Claude Code Hook 配置...");
            await _hookConfiguration.InstallAsync(stoppingToken).ConfigureAwait(false);
            _logger.LogInformation("Hook 配置已安装");
        }

        // 启动 Hook 监听器
        _hookListener.Start(stoppingToken);
        _logger.LogInformation("Hook 监听器已启动 (端口 {Port})", _configService.Settings.HookListenerPort);

        _serverConnection.ConnectionStateChanged += connected =>
        {
            if (connected)
            {
                _logger.LogInformation("WebSocket 已连接");
            }
            else
            {
                _logger.LogWarning("WebSocket 已断开，等待重连...");
            }
        };

        _logger.LogInformation("连接器已启动, DeviceId={DeviceId}, Server={Server}",
            _configService.Settings.DeviceId ?? "N/A", _configService.Settings.ServerUrl);

        // 启动带自动重连的 WebSocket 连接循环
        try
        {
            await _serverConnection.RunWithReconnectAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常停止
        }

        // 清理
        _logger.LogInformation("连接器服务正在停止...");
        await _hookListener.StopAsync().ConfigureAwait(false);
        await _serverConnection.DisconnectAsync().ConfigureAwait(false);
        _logger.LogInformation("连接器服务已停止");
    }
}
