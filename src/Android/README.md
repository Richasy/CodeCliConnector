# Code Cli Connector - Android 客户端

CodeCliConnector 的 Android 客户端，用于在手机上远程接收 Claude Code 的通知并审批权限请求。

## 功能概览

- **实时通知推送**：通过 WebSocket 长连接接收 Claude Code 的通知消息（任务完成、错误提示等）
- **远程权限审批**：当 Claude Code 需要执行敏感操作（如文件写入、命令执行）时，可在手机上批准或拒绝
- **设备管理**：查看所有已注册设备的在线状态和最后心跳时间
- **自动连接**：启动后自动连接服务器，无需手动操作
- **断线重连**：连接断开后自动重连（指数退避，最多重试 3 次）
- **状态同步**：在电脑端本地处理的权限请求会自动同步状态到手机

## 前置条件

1. 已部署 CodeCliConnector 服务器（ConnectorWebServer）
2. 已获取服务器地址和预共享密钥（Pre-Shared Key）
3. Android 11 (API 30) 或更高版本

## 构建

使用 Android Studio 打开 `src/Android` 目录，等待 Gradle 同步完成后即可构建。

**环境要求**：
- Android Studio Meerkat 或更高版本
- JDK 11+
- Android SDK 36

## 使用方式

### 1. 配置服务器

首次打开应用后，进入 **设置** 页面：

1. 填写 **服务器地址**（如 `https://your-server.com`）
2. 填写 **预共享密钥**（与服务器配置的 PSK 一致）
3. 填写 **设备名称**（用于在设备列表中区分，如"我的手机"）
4. 点击 **注册设备**

注册成功后会显示设备 ID 和令牌过期时间。应用将自动连接到服务器。

### 2. 查看通知

切换到 **通知** 页面，所有来自 Claude Code 的消息会按时间倒序显示：

- **权限请求**（蓝色盾牌图标）：点击可查看详情并批准/拒绝
- **普通通知**（信息图标）：点击可查看详情

通知状态说明：
| 图标 | 状态 | 含义 |
|------|------|------|
| ⏳ | 待处理 | 等待审批 |
| ✅ | 已同意 | 已批准该权限请求 |
| ❌ | 已拒绝 | 已拒绝该权限请求 |
| 🖥️ | 已在终端处理 | 用户已在电脑端直接处理 |

点击右上角清空按钮可一键清除所有通知记录。

### 3. 审批权限请求

收到权限请求通知后：

1. 点击通知卡片，弹出详情底部面板
2. 查看工具名称、工作目录、工具输入等信息
3. 点击 **同意** 或 **拒绝**

审批结果会通过 WebSocket 实时发送回 Claude Code，操作会立即生效。

### 4. 查看设备

切换到 **设备** 页面，可查看所有已注册设备：

- 设备类型（Claude Code / Android / iOS / Web）
- 在线状态（绿点/灰点）
- 最后心跳时间

支持下拉刷新。

### 5. 令牌管理

访问令牌有效期为 3 天。过期前可在 **设置** 页面点击 **刷新令牌** 续期。

## 技术栈

| 组件 | 技术 |
|------|------|
| UI | Jetpack Compose + Material 3 |
| DI | Hilt |
| 网络 | OkHttp (REST + WebSocket) |
| 本地存储 | Room (通知记录) + DataStore (用户设置) |
| 序列化 | kotlinx-serialization-json |
| 架构 | MVVM (ViewModel + StateFlow) |

## 项目结构

```
app/src/main/java/com/richasy/codecliconnector/
├── ConnectorApplication.kt          # Application 入口 (Hilt)
├── MainActivity.kt                  # 主界面 (三标签导航 + 自动连接)
├── data/
│   ├── model/                       # 数据模型 (ApiModels, Enums)
│   ├── db/                          # Room 数据库 (NotificationEntity, NotificationDao)
│   ├── network/                     # 网络层 (ApiClient, WsClient)
│   └── repository/                  # 数据仓库 (NotificationRepository, SettingsRepository)
├── di/                              # Hilt 依赖注入模块
├── service/
│   ├── ConnectionService.kt         # 前台服务 (WebSocket 连接 + 心跳 + 消息处理)
│   └── NotificationHelper.kt        # 系统通知管理
└── ui/
    ├── component/                   # 共用组件 (BottomSheets, ConnectionStatusBar)
    ├── screen/
    │   ├── notifications/           # 通知列表 + 详情
    │   ├── devices/                 # 设备列表
    │   └── settings/                # 服务器配置 + 设备注册
    └── theme/                       # Material 3 主题
```
