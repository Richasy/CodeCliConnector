// Copyright (c) Richasy. All rights reserved.

using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace CodeCliConnector.Server.WebSocket;

/// <summary>
/// WebSocket 连接管理器.
/// </summary>
public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, System.Net.WebSockets.WebSocket> _connections = new();

    /// <summary>
    /// 注册连接.
    /// </summary>
    public void AddConnection(string deviceId, System.Net.WebSockets.WebSocket webSocket)
    {
        _connections[deviceId] = webSocket;
    }

    /// <summary>
    /// 移除连接.
    /// </summary>
    public void RemoveConnection(string deviceId)
    {
        _connections.TryRemove(deviceId, out _);
    }

    /// <summary>
    /// 获取连接.
    /// </summary>
    public System.Net.WebSockets.WebSocket? GetConnection(string deviceId)
    {
        _connections.TryGetValue(deviceId, out var ws);
        return ws is { State: WebSocketState.Open } ? ws : null;
    }

    /// <summary>
    /// 检查设备是否在线.
    /// </summary>
    public bool IsOnline(string deviceId)
    {
        return GetConnection(deviceId) is not null;
    }

    /// <summary>
    /// 获取所有在线设备 ID.
    /// </summary>
    public IReadOnlyList<string> GetOnlineDeviceIds()
    {
        return _connections
            .Where(kvp => kvp.Value.State == WebSocketState.Open)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// 获取所有在线连接.
    /// </summary>
    public IReadOnlyList<(string DeviceId, System.Net.WebSockets.WebSocket WebSocket)> GetAllConnections()
    {
        return _connections
            .Where(kvp => kvp.Value.State == WebSocketState.Open)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }
}
