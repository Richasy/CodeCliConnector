// Copyright (c) Richasy. All rights reserved.

using System.Text.Json.Serialization;
using CodeCliConnector.Console.Models;
using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using CodeCliConnector.Core.Models.Requests;
using CodeCliConnector.Core.Models.Responses;

namespace CodeCliConnector.Console;

/// <summary>
/// AOT JSON 序列化上下文.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(ConnectorSettings))]
[JsonSerializable(typeof(HookPayload))]
[JsonSerializable(typeof(PermissionRequestPayload))]
[JsonSerializable(typeof(PermissionResponsePayload))]
[JsonSerializable(typeof(NotificationPayload))]
[JsonSerializable(typeof(PermissionRequestHookResponse))]
[JsonSerializable(typeof(PermissionRequestHookOutput))]
[JsonSerializable(typeof(PermissionDecisionDetail))]
[JsonSerializable(typeof(WebSocketMessage))]
[JsonSerializable(typeof(RegisterDeviceRequest))]
[JsonSerializable(typeof(RefreshTokenRequest))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(DeviceStatusResponse))]
[JsonSerializable(typeof(List<DeviceStatusResponse>))]
[JsonSerializable(typeof(DeviceType))]
[JsonSerializable(typeof(MessageType))]
[JsonSerializable(typeof(ClaudeSettings))]
[JsonSerializable(typeof(ClaudeHookGroup))]
[JsonSerializable(typeof(ClaudeHookHandler))]
[JsonSerializable(typeof(List<ClaudeHookGroup>))]
[JsonSerializable(typeof(Dictionary<string, List<ClaudeHookGroup>>))]
[JsonSerializable(typeof(string))]
internal sealed partial class ConsoleJsonContext : JsonSerializerContext
{
}
