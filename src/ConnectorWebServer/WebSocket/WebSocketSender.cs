// Copyright (c) Richasy. All rights reserved.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CodeCliConnector.Core.Models;

namespace CodeCliConnector.Server.WebSocket;

/// <summary>
/// WebSocket 发送器（AOT 友好）.
/// </summary>
public static class WebSocketSender
{
    /// <summary>
    /// 发送 WebSocket 消息.
    /// </summary>
    public static async Task SendAsync(
        System.Net.WebSockets.WebSocket webSocket,
        WebSocketMessage message,
        JsonTypeInfo<WebSocketMessage> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        if (webSocket.State != System.Net.WebSockets.WebSocketState.Open)
        {
            return;
        }

        var json = JsonSerializer.Serialize(message, jsonTypeInfo);
        var bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            System.Net.WebSockets.WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 从 WebSocket 接收消息.
    /// </summary>
    public static async Task<WebSocketMessage?> ReceiveAsync(
        System.Net.WebSockets.WebSocket webSocket,
        JsonTypeInfo<WebSocketMessage> jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        System.Net.WebSockets.WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken).ConfigureAwait(false);

            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
            {
                return null;
            }

            await ms.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
        }
        while (!result.EndOfMessage);

        ms.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync(ms, jsonTypeInfo, cancellationToken).ConfigureAwait(false);
    }
}
