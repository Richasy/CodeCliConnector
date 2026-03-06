// Copyright (c) Richasy. All rights reserved.

namespace CodeCliConnector.Storage.Database;

/// <summary>
/// 数据库表结构定义.
/// </summary>
internal static class Schema
{
    /// <summary>
    /// 创建 Devices 表.
    /// </summary>
    public const string CreateDevicesTable = """
        CREATE TABLE IF NOT EXISTS "Devices" (
            "Id" TEXT PRIMARY KEY NOT NULL,
            "Name" TEXT NOT NULL,
            "Type" INTEGER NOT NULL DEFAULT 0,
            "IsOnline" INTEGER NOT NULL DEFAULT 0,
            "LastHeartbeat" INTEGER NOT NULL DEFAULT 0,
            "RegisteredAt" INTEGER NOT NULL DEFAULT 0
        );
        """;

    /// <summary>
    /// 创建 Messages 表.
    /// </summary>
    public const string CreateMessagesTable = """
        CREATE TABLE IF NOT EXISTS "Messages" (
            "Id" TEXT PRIMARY KEY NOT NULL,
            "SourceDeviceId" TEXT NOT NULL,
            "TargetDeviceId" TEXT,
            "Type" INTEGER NOT NULL DEFAULT 0,
            "Status" INTEGER NOT NULL DEFAULT 0,
            "Payload" TEXT NOT NULL,
            "CreatedAt" INTEGER NOT NULL DEFAULT 0,
            "ExpiresAt" INTEGER NOT NULL DEFAULT 0,
            "CorrelationId" TEXT
        );
        CREATE INDEX IF NOT EXISTS "IX_Messages_TargetDeviceId_Status" ON "Messages"("TargetDeviceId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_Messages_CorrelationId" ON "Messages"("CorrelationId");
        CREATE INDEX IF NOT EXISTS "IX_Messages_ExpiresAt" ON "Messages"("ExpiresAt");
        """;

    /// <summary>
    /// 创建 AccessTokens 表.
    /// </summary>
    public const string CreateAccessTokensTable = """
        CREATE TABLE IF NOT EXISTS "AccessTokens" (
            "Id" TEXT PRIMARY KEY NOT NULL,
            "TokenHash" TEXT NOT NULL,
            "DeviceId" TEXT NOT NULL,
            "CreatedAt" INTEGER NOT NULL DEFAULT 0,
            "ExpiresAt" INTEGER NOT NULL DEFAULT 0,
            "IsRevoked" INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS "IX_AccessTokens_TokenHash" ON "AccessTokens"("TokenHash");
        CREATE INDEX IF NOT EXISTS "IX_AccessTokens_DeviceId" ON "AccessTokens"("DeviceId");
        """;
}
