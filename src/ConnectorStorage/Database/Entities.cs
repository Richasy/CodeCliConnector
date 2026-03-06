// Copyright (c) Richasy. All rights reserved.

using CodeCliConnector.Core.Models;
using CodeCliConnector.Core.Models.Constants;
using CodeCliConnector.SqliteGenerator;

namespace CodeCliConnector.Storage.Database;

/// <summary>
/// 设备实体.
/// </summary>
[SqliteTable("Devices")]
internal sealed class DeviceEntity
{
    /// <summary>
    /// 设备唯一标识.
    /// </summary>
    [SqliteColumn("Id", IsPrimaryKey = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称.
    /// </summary>
    [SqliteColumn("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备类型.
    /// </summary>
    [SqliteColumn("Type")]
    public int Type { get; set; }

    /// <summary>
    /// 是否在线.
    /// </summary>
    [SqliteColumn("IsOnline")]
    public bool IsOnline { get; set; }

    /// <summary>
    /// 最后心跳时间.
    /// </summary>
    [SqliteColumn("LastHeartbeat")]
    public long LastHeartbeat { get; set; }

    /// <summary>
    /// 注册时间.
    /// </summary>
    [SqliteColumn("RegisteredAt")]
    public long RegisteredAt { get; set; }

    /// <summary>
    /// 转换为领域模型.
    /// </summary>
    public DeviceInfo ToModel()
    {
        return new DeviceInfo
        {
            DeviceId = Id,
            DeviceName = Name,
            DeviceType = (DeviceType)Type,
            IsOnline = IsOnline,
            LastHeartbeat = LastHeartbeat,
            RegisteredAt = RegisteredAt,
        };
    }

    /// <summary>
    /// 从领域模型创建实体.
    /// </summary>
    public static DeviceEntity FromModel(DeviceInfo model)
    {
        return new DeviceEntity
        {
            Id = model.DeviceId,
            Name = model.DeviceName,
            Type = (int)model.DeviceType,
            IsOnline = model.IsOnline,
            LastHeartbeat = model.LastHeartbeat,
            RegisteredAt = model.RegisteredAt,
        };
    }
}

/// <summary>
/// 消息实体.
/// </summary>
[SqliteTable("Messages")]
internal sealed class MessageEntity
{
    /// <summary>
    /// 消息唯一标识.
    /// </summary>
    [SqliteColumn("Id", IsPrimaryKey = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 来源设备 ID.
    /// </summary>
    [SqliteColumn("SourceDeviceId")]
    public string SourceDeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 目标设备 ID.
    /// </summary>
    [SqliteColumn("TargetDeviceId")]
    public string? TargetDeviceId { get; set; }

    /// <summary>
    /// 消息类型.
    /// </summary>
    [SqliteColumn("Type")]
    public int Type { get; set; }

    /// <summary>
    /// 消息状态.
    /// </summary>
    [SqliteColumn("Status")]
    public int Status { get; set; }

    /// <summary>
    /// 消息载荷.
    /// </summary>
    [SqliteColumn("Payload")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间.
    /// </summary>
    [SqliteColumn("CreatedAt")]
    public long CreatedAt { get; set; }

    /// <summary>
    /// 过期时间.
    /// </summary>
    [SqliteColumn("ExpiresAt")]
    public long ExpiresAt { get; set; }

    /// <summary>
    /// 关联消息 ID.
    /// </summary>
    [SqliteColumn("CorrelationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 转换为领域模型.
    /// </summary>
    public MessageInfo ToModel()
    {
        return new MessageInfo
        {
            MessageId = Id,
            SourceDeviceId = SourceDeviceId,
            TargetDeviceId = TargetDeviceId,
            MessageType = (MessageType)Type,
            Status = (MessageStatus)Status,
            Payload = Payload,
            CreatedAt = CreatedAt,
            ExpiresAt = ExpiresAt,
            CorrelationId = CorrelationId,
        };
    }

    /// <summary>
    /// 从领域模型创建实体.
    /// </summary>
    public static MessageEntity FromModel(MessageInfo model)
    {
        return new MessageEntity
        {
            Id = model.MessageId,
            SourceDeviceId = model.SourceDeviceId,
            TargetDeviceId = model.TargetDeviceId,
            Type = (int)model.MessageType,
            Status = (int)model.Status,
            Payload = model.Payload,
            CreatedAt = model.CreatedAt,
            ExpiresAt = model.ExpiresAt,
            CorrelationId = model.CorrelationId,
        };
    }
}

/// <summary>
/// 访问令牌实体.
/// </summary>
[SqliteTable("AccessTokens")]
internal sealed class AccessTokenEntity
{
    /// <summary>
    /// 令牌 ID.
    /// </summary>
    [SqliteColumn("Id", IsPrimaryKey = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 令牌 SHA256 哈希.
    /// </summary>
    [SqliteColumn("TokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// 关联设备 ID.
    /// </summary>
    [SqliteColumn("DeviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间.
    /// </summary>
    [SqliteColumn("CreatedAt")]
    public long CreatedAt { get; set; }

    /// <summary>
    /// 过期时间.
    /// </summary>
    [SqliteColumn("ExpiresAt")]
    public long ExpiresAt { get; set; }

    /// <summary>
    /// 是否已吊销.
    /// </summary>
    [SqliteColumn("IsRevoked")]
    public bool IsRevoked { get; set; }

    /// <summary>
    /// 转换为领域模型.
    /// </summary>
    public AccessTokenInfo ToModel()
    {
        return new AccessTokenInfo
        {
            TokenId = Id,
            TokenHash = TokenHash,
            DeviceId = DeviceId,
            CreatedAt = CreatedAt,
            ExpiresAt = ExpiresAt,
            IsRevoked = IsRevoked,
        };
    }

    /// <summary>
    /// 从领域模型创建实体.
    /// </summary>
    public static AccessTokenEntity FromModel(AccessTokenInfo model)
    {
        return new AccessTokenEntity
        {
            Id = model.TokenId,
            TokenHash = model.TokenHash,
            DeviceId = model.DeviceId,
            CreatedAt = model.CreatedAt,
            ExpiresAt = model.ExpiresAt,
            IsRevoked = model.IsRevoked,
        };
    }
}
