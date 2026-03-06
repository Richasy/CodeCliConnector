# CodeCliConnector Server

CodeCliConnector 中转服务器，让用户从移动设备远程控制本机 Claude Code 实例（接收通知、授权操作、发送指令）。

## 架构概览

```
┌─────────────────────────────────────────────────────────┐
│                   ConnectorWebServer                    │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │  REST API    │  │  WebSocket   │  │  Background   │  │
│  │  Endpoints   │  │  Handler     │  │  Services     │  │
│  └──────┬───────┘  └──────┬───────┘  └───────┬───────┘  │
│         │                 │                   │          │
│  ┌──────┴─────────────────┴───────────────────┴───────┐  │
│  │                  Middleware Pipeline                │  │
│  │  ErrorHandling → WebSocket → TokenAuth → Routing   │  │
│  └────────────────────────┬───────────────────────────┘  │
│                           │                              │
│  ┌────────────────────────┴───────────────────────────┐  │
│  │              Service Layer                         │  │
│  │  TokenService · MessageRouter · BroadcastService   │  │
│  │  ConnectionManager · ResponseTracker               │  │
│  └────────────────────────┬───────────────────────────┘  │
│                           │                              │
│  ┌────────────────────────┴───────────────────────────┐  │
│  │           ConnectorStorage (SQLite)                │  │
│  │  ConnectorDataService (Devices·Messages·Tokens)    │  │
│  └────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 技术栈

- ASP.NET Core Minimal API (net10.0) + Native AOT
- WebSocket 实时推送 + 心跳检测
- SQLite (WAL 模式) + 源码生成器自动生成 Repository
- 预共享密钥认证 → Base64Url 访问令牌（默认 3 天有效期）
- AOT 兼容的 JSON 序列化（`JsonSerializerContext` + camelCase）

### 核心消息流

1. 设备通过预共享密钥注册，获取访问令牌
2. 设备通过 WebSocket 连接，自动接收离线期间的待处理消息
3. **通知消息**：广播到所有在线终端（排除发送方）
4. **命令消息**：定向发送给目标设备，或广播
5. **响应消息**：第一个回复生效（first-response-wins），通知其余终端消息已处理
6. 离线设备的消息会暂存到数据库，上线后自动投递

---

## 认证

### 预共享密钥

注册和认证基于预共享密钥（`PreSharedKey`），在 `appsettings.json` 中配置。

### REST API 认证

受保护的端点需要在请求头中携带 Bearer Token：

```
Authorization: Bearer <accessToken>
```

### WebSocket 认证

WebSocket 连接通过 URL query parameter 传递令牌：

```
ws://<host>/ws/connect?token=<accessToken>
```

### 免认证端点

| 端点 | 说明 |
|------|------|
| `POST /api/auth/register` | 设备注册 |
| `POST /api/auth/refresh` | 令牌刷新 |
| `GET /openapi` | OpenAPI 文档（仅 Development 环境） |

---

## REST API 端点

### POST /api/auth/register

注册设备。同名同类型设备重复注册时会复用已有 `deviceId` 并吊销旧令牌。

**请求体：**

```json
{
  "preSharedKey": "your-pre-shared-key",
  "deviceName": "MyPhone",
  "deviceType": 1
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `preSharedKey` | string | 是 | 预共享密钥 |
| `deviceName` | string | 是 | 设备名称 |
| `deviceType` | int | 是 | 设备类型枚举值 |

`deviceType` 枚举：

| 值 | 名称 | 说明 |
|----|------|------|
| 0 | ClaudeCode | Claude Code 实例 |
| 1 | Android | Android 终端 |
| 2 | IOS | iOS 终端 |
| 3 | Web | Web 终端 |

**响应 200：**

```json
{
  "accessToken": "Pk58wbjaz-Ho4zjtojkkTTvlBs9knDvtKcjDROUNugU",
  "deviceId": "ce11523a7b154d99baa227ec52f3374b",
  "expiresAt": 1773066868
}
```

**响应 401：** 预共享密钥错误

---

### POST /api/auth/refresh

刷新令牌。旧令牌将被吊销，返回新令牌。

**请求体：**

```json
{
  "accessToken": "current-access-token"
}
```

**响应 200：**

```json
{
  "accessToken": "new-access-token",
  "deviceId": "ce11523a7b154d99baa227ec52f3374b",
  "expiresAt": 1773325268
}
```

**响应 401：** 令牌无效或已过期

---

### GET /api/devices/status

获取所有已注册设备的状态。**需要认证。**

**响应 200：**

```json
[
  {
    "deviceId": "ce11523a7b154d99baa227ec52f3374b",
    "deviceName": "MyPC",
    "deviceType": 0,
    "isOnline": true,
    "lastHeartbeat": 1772804217
  },
  {
    "deviceId": "8f78132bfa4a4b108e61b677a36835d5",
    "deviceName": "MyPhone",
    "deviceType": 1,
    "isOnline": false,
    "lastHeartbeat": 1772803877
  }
]
```

---

### GET /api/messages/pending

获取当前设备的待处理消息（REST 轮询备用方案）。**需要认证。**

**响应 200：**

```json
[
  {
    "messageId": "notif-001_device2id",
    "sourceDeviceId": "ce11523a7b154d99baa227ec52f3374b",
    "targetDeviceId": "8f78132bfa4a4b108e61b677a36835d5",
    "messageType": 1,
    "status": 0,
    "payload": "{\"text\":\"hello\"}",
    "createdAt": 1772804000,
    "expiresAt": 1772804300,
    "correlationId": null
  }
]
```

---

## WebSocket 协议

### 连接

```
ws://<host>/ws/connect?token=<accessToken>
```

连接成功后：
1. 服务端标记设备为在线
2. 自动投递离线期间的待处理消息
3. 进入消息收发循环

### 消息格式

所有 WebSocket 消息均为 JSON 格式的 `WebSocketMessage`：

```json
{
  "messageId": "unique-id",
  "type": 1,
  "sourceDeviceId": "sender-device-id",
  "targetDeviceId": "receiver-device-id",
  "payload": "{\"key\":\"value\"}",
  "correlationId": "related-message-id",
  "timestamp": 1772804217
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `messageId` | string | 消息唯一标识（必填） |
| `type` | int | 消息类型枚举值 |
| `sourceDeviceId` | string? | 来源设备 ID（服务端自动填充） |
| `targetDeviceId` | string? | 目标设备 ID，为空表示广播 |
| `payload` | string | 消息载荷，JSON 字符串 |
| `correlationId` | string? | 关联消息 ID，用于 Response 关联 Command |
| `timestamp` | long | Unix 时间戳（秒），服务端自动填充 |

### 消息类型 (type)

| 值 | 名称 | 行为 |
|----|------|------|
| 0 | Heartbeat | 心跳保活，更新设备最后活跃时间，不转发 |
| 1 | Notification | 广播给所有在线终端（排除发送方），为离线设备暂存 |
| 2 | Command | 定向发送给 `targetDeviceId`（如指定），否则广播 |
| 3 | Response | 根据 `correlationId` 关联原始 Command，第一个回复生效 |

### 消息状态 (status)

REST API 返回的 `MessageInfo` 中包含消息状态：

| 值 | 名称 | 说明 |
|----|------|------|
| 0 | Pending | 等待投递 |
| 1 | Delivered | 已投递 |
| 2 | Processed | 已处理 |
| 3 | Expired | 已过期 |

### 使用示例

**发送心跳：**

```json
{"messageId": "hb-001", "type": 0}
```

**发送通知（广播）：**

```json
{
  "messageId": "notif-001",
  "type": 1,
  "payload": "{\"title\":\"Permission Required\",\"detail\":\"approve file edit?\"}"
}
```

**发送命令（定向）：**

```json
{
  "messageId": "cmd-001",
  "type": 2,
  "targetDeviceId": "target-device-id",
  "payload": "{\"action\":\"authorize\",\"scope\":\"file-edit\"}"
}
```

**发送响应：**

```json
{
  "messageId": "resp-001",
  "type": 3,
  "targetDeviceId": "requester-device-id",
  "correlationId": "cmd-001",
  "payload": "{\"result\":\"approved\"}"
}
```

---

## 配置

在 `appsettings.json` 的 `ServerSettings` 节中配置：

```json
{
  "ServerSettings": {
    "PreSharedKey": "change-me-in-production",
    "TokenExpiryDays": 3,
    "HeartbeatTimeoutSeconds": 30,
    "DatabasePath": "connector.db",
    "MessageExpirySeconds": 300,
    "MessageCleanupDays": 7,
    "HeartbeatCheckIntervalSeconds": 10,
    "MessageCleanupIntervalSeconds": 60
  }
}
```

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `PreSharedKey` | `change-me-in-production` | 预共享密钥，**生产环境必须修改** |
| `TokenExpiryDays` | 3 | 令牌有效期（天） |
| `HeartbeatTimeoutSeconds` | 30 | 心跳超时阈值（秒），超时标记为离线 |
| `DatabasePath` | `connector.db` | SQLite 数据库文件路径 |
| `MessageExpirySeconds` | 300 | 消息过期时间（秒） |
| `MessageCleanupDays` | 7 | 旧消息清理阈值（天） |
| `HeartbeatCheckIntervalSeconds` | 10 | 心跳监控检查间隔（秒） |
| `MessageCleanupIntervalSeconds` | 60 | 消息清理任务间隔（秒） |

---

## 后台服务

| 服务 | 说明 |
|------|------|
| `HeartbeatMonitorService` | 定期检查心跳超时的设备并标记为离线 |
| `MessageCleanupService` | 定期清理过期消息和已吊销的令牌 |
