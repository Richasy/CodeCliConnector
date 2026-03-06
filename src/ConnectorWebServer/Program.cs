// Copyright (c) Richasy. All rights reserved.

using System.Text.Json.Serialization;
using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using CodeCliConnector.Core.Models.Requests;
using CodeCliConnector.Core.Models.Responses;
using CodeCliConnector.Server.Configuration;
using CodeCliConnector.Server.Endpoints;
using CodeCliConnector.Server.Middleware;
using CodeCliConnector.Server.Services;
using CodeCliConnector.Server.WebSocket;
using CodeCliConnector.Storage;

var builder = WebApplication.CreateSlimBuilder(args);

// 配置 JSON 序列化（AOT 兼容）
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddOpenApi();

// 绑定配置
builder.Services.Configure<ServerSettings>(
    builder.Configuration.GetSection(ServerSettings.SectionName));

// 注册数据服务（单例）
builder.Services.AddSingleton<ConnectorDataService>();

// 注册服务
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<ResponseTracker>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton(AppJsonSerializerContext.Default.WebSocketMessage);
builder.Services.AddSingleton<BroadcastService>();
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<WebSocketHandler>();

// 注册后台服务
builder.Services.AddHostedService<HeartbeatMonitorService>();
builder.Services.AddHostedService<MessageCleanupService>();

var app = builder.Build();

// 初始化数据库
var settings = app.Configuration.GetSection(ServerSettings.SectionName).Get<ServerSettings>() ?? new ServerSettings();
var dataService = app.Services.GetRequiredService<ConnectorDataService>();
await dataService.InitializeAsync(settings.DatabasePath).ConfigureAwait(false);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 中间件管道
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
});
app.UseMiddleware<TokenAuthMiddleware>();

// 映射 REST 端点
app.MapAuthEndpoints();
app.MapDeviceEndpoints();
app.MapMessageEndpoints();

// 映射 WebSocket 端点
app.Map("/ws/connect", async (HttpContext context, WebSocketHandler handler) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        await handler.HandleAsync(context).ConfigureAwait(false);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// AOT JSON 序列化上下文.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RegisterDeviceRequest))]
[JsonSerializable(typeof(RefreshTokenRequest))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(DeviceStatusResponse))]
[JsonSerializable(typeof(List<DeviceStatusResponse>))]
[JsonSerializable(typeof(MessageInfo))]
[JsonSerializable(typeof(List<MessageInfo>))]
[JsonSerializable(typeof(IReadOnlyList<MessageInfo>))]
[JsonSerializable(typeof(WebSocketMessage))]
[JsonSerializable(typeof(DeviceType))]
[JsonSerializable(typeof(MessageType))]
[JsonSerializable(typeof(MessageStatus))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
