package com.richasy.codecliconnector.data.model

import kotlinx.serialization.Serializable

/** 设备注册请求 */
@Serializable
data class RegisterDeviceRequest(
    val deviceName: String,
    val deviceType: Int,
    val preSharedKey: String,
)

/** 令牌刷新请求 */
@Serializable
data class RefreshTokenRequest(
    val accessToken: String,
)

/** 认证响应 */
@Serializable
data class AuthResponse(
    val accessToken: String,
    val deviceId: String,
    val expiresAt: Long,
)

/** 设备状态响应 */
@Serializable
data class DeviceStatusResponse(
    val deviceId: String,
    val deviceName: String,
    val deviceType: Int,
    val isOnline: Boolean,
    val lastHeartbeat: Long,
)

/** WebSocket 消息 */
@Serializable
data class WebSocketMessage(
    val messageId: String,
    val type: Int,
    val sourceDeviceId: String? = null,
    val targetDeviceId: String? = null,
    val payload: String = "",
    val correlationId: String? = null,
    val timestamp: Long = 0,
)

/** 通知载荷 */
@Serializable
data class NotificationPayload(
    val hookEvent: String? = null,
    val sessionId: String? = null,
    val cwd: String? = null,
    val title: String? = null,
    val message: String? = null,
    val notificationType: String? = null,
)

/** 权限请求载荷 */
@Serializable
data class PermissionRequestPayload(
    val requestId: String,
    val sessionId: String? = null,
    val cwd: String? = null,
    val permissionMode: String? = null,
    val toolName: String? = null,
    val toolInput: String? = null,
    val receivedTimestampMs: Long = 0,
    /** 权限建议列表 JSON 字符串（如"总是允许 Bash"等选项） */
    val permissionSuggestions: String? = null,
)

/** 权限响应载荷（发送回服务端） */
@Serializable
data class PermissionResponsePayload(
    val requestId: String,
    val behavior: String = "deny",
    val message: String? = null,
    /** 要应用的权限规则更新 JSON 字符串（等同于用户选择"总是允许"选项） */
    val updatedPermissions: String? = null,
    /** 是否中断 Claude（仅 deny 时有效） */
    val interrupt: Boolean? = null,
)

/** 已处理通知载荷（由服务器广播，通知其他终端某请求已被处理） */
@Serializable
data class ProcessedPayload(
    val correlationId: String? = null,
    val status: String? = null,
)
