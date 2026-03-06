// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Storage;

namespace CodeCliConnector.Server.Endpoints;

/// <summary>
/// 消息端点.
/// </summary>
public static class MessageEndpoints
{
    /// <summary>
    /// 映射消息端点.
    /// </summary>
    public static void MapMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/messages");

        group.MapGet("/pending", GetPendingAsync)
            .WithName("GetPendingMessages");
    }

    private static async Task<IResult> GetPendingAsync(
        HttpContext context,
        ConnectorDataService dataService,
        CancellationToken cancellationToken)
    {
        var deviceId = context.Items["DeviceId"]?.ToString();
        if (string.IsNullOrEmpty(deviceId))
        {
            return Results.Unauthorized();
        }

        var messages = await dataService.GetPendingMessagesAsync(deviceId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(messages);
    }
}
