# CodeCliConnector

从手机远程控制 [Claude Code](https://docs.anthropic.com/en/docs/build-with-claude/claude-code) — 接收通知、审批权限请求。

## 工作原理

```
┌──────────────┐      Hook (stdin/stdout)      ┌────────────────────┐
│  Claude Code  │ ◄──────────────────────────► │  ConnectorConsole   │
│   (本机 CLI)  │                               │  (本机连接器, ccc)  │
└──────────────┘                               └─────────┬──────────┘
                                                         │ WebSocket
                                                         ▼
                                               ┌────────────────────┐
                                               │  ConnectorWebServer │
                                               │  (中转服务器, Docker)│
                                               └─────────┬──────────┘
                                                         │ WebSocket
                                                         ▼
                                               ┌────────────────────┐
                                               │   Android 客户端    │
                                               │    (你的手机)       │
                                               └────────────────────┘
```

**典型场景**：Claude Code 在后台执行长任务，需要文件写入/命令执行等权限时，手机收到推送通知，一键批准或拒绝，无需回到电脑前。

## 组件

| 组件 | 说明 | 技术栈 |
|------|------|--------|
| **ConnectorWebServer** | 中转服务器，路由消息、管理设备 | ASP.NET Core Minimal API, .NET 10, Native AOT, SQLite |
| **ConnectorConsole** | 本机连接器 CLI 工具 (`ccc`)，拦截 Claude Code Hook 并转发 | .NET 10, Native AOT, dotnet tool |
| **Android App** | 手机端，接收通知并远程审批 | Kotlin, Jetpack Compose, Material 3, Hilt |
| ConnectorShare | 共享模型库 | .NET 10 |
| ConnectorStorage | SQLite 存储层 + 源码生成器 | .NET 10 |

## 快速开始

### 1. 部署服务器

服务器以 Docker 镜像形式提供，支持 `docker run` 和 `docker compose` 两种方式。

#### Docker Run

```bash
docker run -d \
  --name codecliconnector-server \
  -p 8080:8080 \
  -v connector-data:/app/data \
  -e ServerSettings__PreSharedKey=your-secret-key-here \
  richasyz/codecliconnector-server:latest
```

#### Docker Compose

项目根目录提供了 [`docker-compose.example.yml`](docker-compose.example.yml)，复制并修改即可：

```bash
cp docker-compose.example.yml docker-compose.yml
# 编辑 docker-compose.yml，修改 PreSharedKey
docker compose up -d
```

#### 服务器配置项

所有配置通过环境变量设置，使用 `ServerSettings__` 前缀：

| 环境变量 | 默认值 | 说明 |
|----------|--------|------|
| `ServerSettings__PreSharedKey` | `change-me-in-production` | **必须修改。** 预共享密钥，所有客户端注册时使用 |
| `ServerSettings__TokenExpiryDays` | `3` | 访问令牌有效期（天） |
| `ServerSettings__HeartbeatTimeoutSeconds` | `30` | 心跳超时阈值（秒），超时标记设备离线 |
| `ServerSettings__DatabasePath` | `/app/data/connector.db` | SQLite 数据库路径，挂载 `/app/data` 卷以持久化 |
| `ServerSettings__MessageExpirySeconds` | `300` | 待处理消息过期时间（秒） |
| `ServerSettings__MessageCleanupDays` | `7` | 历史消息清理阈值（天） |

> **生产部署建议**：在服务器前放置反向代理（Nginx / Caddy / Traefik）并启用 TLS。WebSocket 连接需要正确转发 `Upgrade` 和 `Connection` 头。

### 2. 安装本机连接器

ConnectorConsole 以 [NuGet dotnet tool](https://www.nuget.org/packages/CodeCliConnector) 形式发布：

```bash
dotnet tool install -g CodeCliConnector
```

更新到最新版本：

```bash
dotnet tool update -g CodeCliConnector
```

安装后使用 `ccc` 命令操作。

#### 配置

首次使用需要配置服务器地址和密钥：

```bash
ccc config
```

按提示填写：
- **服务器地址**：你部署的服务器 URL（如 `https://connector.example.com`）
- **预共享密钥**：与服务器配置的 `PreSharedKey` 一致

#### 启动

```bash
ccc run
```

启动后连接器会：
1. 向服务器注册设备并获取访问令牌
2. 自动配置 Claude Code Hook（写入 `~/.claude/settings.json`）
3. 建立 WebSocket 长连接，开始转发通知和权限请求

#### 注册为 Windows 服务（可选）

如果希望连接器在后台自动运行：

```bash
# 注册服务（需要管理员权限）
ccc service-install

# 卸载服务
ccc service-uninstall
```

#### 所有命令

| 命令 | 说明 |
|------|------|
| `ccc run` | 启动连接器（默认命令） |
| `ccc config` | 配置服务器地址和密钥 |
| `ccc reconnect` | 重新注册设备获取新令牌 |
| `ccc list-devices` | 列出所有设备及在线状态 |
| `ccc unhook` | 卸载 Claude Code Hook 配置 |
| `ccc service-install` | 注册为 Windows 服务 |
| `ccc service-uninstall` | 卸载 Windows 服务 |
| `ccc log` | 打开日志文件夹 |
| `ccc help` | 显示帮助信息 |

### 3. 安装 Android 客户端

从 [GitHub Releases](https://github.com/Richasy/CodeCliConnector/releases) 下载最新 APK 安装。

**系统要求**：Android 11 (API 30) 或更高版本

#### 配置

1. 打开应用，进入 **设置** 页面
2. 填写 **服务器地址**（与连接器配置的相同）
3. 填写 **预共享密钥**（与服务器配置的相同）
4. 填写 **设备名称**（用于区分设备，如"我的手机"）
5. 点击 **注册设备**

注册成功后应用将自动连接到服务器。

#### 使用

- **通知页**：实时接收 Claude Code 的消息（任务完成、错误提示等）
- **权限审批**：收到权限请求时，点击通知卡片查看详情，一键批准或拒绝
- **设备页**：查看所有已注册设备的在线状态
- **设置页**：管理服务器连接、刷新令牌

## 完整链路

```
1. Claude Code 执行操作需要权限
2. Hook 触发，将请求发送到本机 ConnectorConsole
3. ConnectorConsole 通过 WebSocket 转发到服务器
4. 服务器广播给所有在线终端（手机）
5. 手机收到通知，用户点击批准/拒绝
6. 响应原路返回到 Claude Code
7. Claude Code 继续/中止操作
```

## 项目结构

```
src/
├── Utilities/SqliteGenerator/    # Roslyn 源码生成器（自动生成 SQLite Repository）
├── ConnectorShare/               # 共享模型、DTO、数据库迁移
├── ConnectorStorage/             # SQLite 存储层（DataService）
├── ConnectorWebServer/           # 中转服务器（Minimal API + WebSocket）
├── ConnectorConsole/             # 本机连接器 CLI（dotnet tool）
└── Android/                      # Android 客户端（Jetpack Compose）
```

## 协议

MIT

## 链接

- [服务器 Docker 镜像](https://hub.docker.com/r/richasyz/codecliconnector-server)
- [NuGet 工具包](https://www.nuget.org/packages/CodeCliConnector)
- [服务器 API 文档](src/ConnectorWebServer/README.md)
- [Android 客户端文档](src/Android/README.md)
- [连接器 CLI 文档](src/ConnectorConsole/README.md)
