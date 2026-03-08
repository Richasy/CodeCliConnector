package com.richasy.codecliconnector.data.db

import androidx.room.Entity
import androidx.room.PrimaryKey

/** 本地通知记录实体 */
@Entity(tableName = "notifications")
data class NotificationEntity(
    @PrimaryKey
    val id: String,
    /** 通知类型: "notification" 或 "permission_request" */
    val hookEvent: String,
    val sourceDeviceId: String?,
    val sessionId: String?,
    val cwd: String?,
    /** 通知标题（Notification 类型） */
    val title: String?,
    /** 通知消息（Notification 类型） */
    val message: String?,
    /** 通知子类型 (info/warning/error) */
    val notificationType: String?,
    /** 权限请求 ID（PermissionRequest 类型） */
    val requestId: String?,
    val permissionMode: String?,
    val toolName: String?,
    val toolInput: String?,
    /** 权限建议列表 JSON 字符串 */
    val permissionSuggestions: String? = null,
    /** 处理状态: "pending", "approved", "denied", "expired", "handled_elsewhere" */
    val status: String = "pending",
    val createdAt: Long = System.currentTimeMillis(),
    /** WebSocket 消息的 correlationId，用于发送响应 */
    val correlationId: String? = null,
)
