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
