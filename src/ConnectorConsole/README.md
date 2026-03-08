# CodeCliConnector (ccc)

Claude Code 远程连接器 CLI 工具。

将 Claude Code 的 Hook 通知和权限请求转发到远程服务器，支持从移动设备实时审批操作。

## 安装

```bash
dotnet tool install -g CodeCliConnector
```

## 使用

```bash
# 配置服务器地址和密钥
ccc config

# 启动连接器
ccc run

# 查看帮助
ccc help
```

## 命令

| 命令 | 说明 |
|------|------|
| `run` | 启动连接器（默认命令） |
| `config` | 配置服务器地址和密钥 |
| `reconnect` | 重新注册设备获取新令牌 |
| `list-devices` | 列出所有设备及在线状态 |
| `unhook` | 卸载 Claude Code Hook 配置 |
| `service-install` | 注册为 Windows 服务（需管理员权限） |
| `service-uninstall` | 卸载 Windows 服务（需管理员权限） |
| `log` | 打开日志文件夹 |
| `help` | 显示帮助信息 |

## Hook 事件

启动后连接器自动配置以下 Claude Code Hook（写入 `~/.claude/settings.json`）：

| Hook | 端点 | 模式 | 说明 |
|------|------|------|------|
| **Notification** | `/notification` | 异步 | 接收通知消息（过滤掉 `permission_prompt` 避免重复） |
| **PermissionRequest** | `/permission` | 同步, 6h 超时 | 权限请求转发到手机，支持同意/拒绝/总是允许等操作 |
| **PostToolUse** | `/tool-completed` | 异步 | 检测终端本地已处理的权限以同步状态 |
| **Stop** | `/stop` | 异步 | Claude 完成任务时通知手机 |

### 权限请求延迟转发

收到权限请求后，连接器会先等待一段时间（默认 15 秒），给用户在终端直接操作的机会。如果等待期间用户已在终端处理，则不会转发到手机。可通过配置文件 `~/.ccc/config.json` 的 `permissionForwardDelaySeconds` 调整。
