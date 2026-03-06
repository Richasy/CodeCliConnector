// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Server.Middleware;

/// <summary>
/// Bearer token 验证中间件.
/// </summary>
public sealed class TokenAuthMiddleware
{
    private static readonly HashSet<string> _skipPaths =
    [
        "/api/auth/register",
        "/api/auth/refresh",
        "/openapi",
    ];

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenAuthMiddleware"/> class.
    /// </summary>
    public TokenAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// 处理请求.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, Services.TokenService tokenService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // 跳过不需要认证的路径
        if (ShouldSkipAuth(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // WebSocket 连接通过 query parameter 传递 token
        if (context.WebSockets.IsWebSocketRequest)
        {
            var wsToken = context.Request.Query["token"].ToString();
            if (string.IsNullOrEmpty(wsToken))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var wsTokenInfo = await tokenService.ValidateTokenAsync(wsToken, context.RequestAborted).ConfigureAwait(false);
            if (wsTokenInfo is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Items["DeviceId"] = wsTokenInfo.DeviceId;
            context.Items["TokenInfo"] = wsTokenInfo;
            await _next(context).ConfigureAwait(false);
            return;
        }

        // REST API 使用 Authorization: Bearer <token>
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var tokenInfo = await tokenService.ValidateTokenAsync(token, context.RequestAborted).ConfigureAwait(false);
        if (tokenInfo is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["DeviceId"] = tokenInfo.DeviceId;
        context.Items["TokenInfo"] = tokenInfo;
        await _next(context).ConfigureAwait(false);
    }

    private static bool ShouldSkipAuth(string path)
    {
        foreach (var skipPath in _skipPaths)
        {
            if (path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
