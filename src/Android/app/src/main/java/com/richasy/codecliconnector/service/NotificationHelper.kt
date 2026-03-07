package com.richasy.codecliconnector.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import com.richasy.codecliconnector.MainActivity
import com.richasy.codecliconnector.R
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

/** 系统通知管理 */
@Singleton
class NotificationHelper @Inject constructor(
    @ApplicationContext private val context: Context,
) {
    companion object {
        const val CHANNEL_SERVICE = "ws_service"
        const val CHANNEL_ALERTS = "alerts"
        const val NOTIFY_ID_SERVICE = 1
        private var nextAlertId = 100
    }

    init {
        createChannels()
    }

    private fun createChannels() {
        val manager = context.getSystemService(NotificationManager::class.java)

        val serviceChannel = NotificationChannel(
            CHANNEL_SERVICE,
            "连接服务",
            NotificationManager.IMPORTANCE_LOW,
        ).apply { description = "WebSocket 连接服务运行中" }

        val alertChannel = NotificationChannel(
            CHANNEL_ALERTS,
            "Claude Code 通知",
            NotificationManager.IMPORTANCE_HIGH,
        ).apply { description = "来自 Claude Code 的权限请求和通知" }

        manager.createNotificationChannel(serviceChannel)
        manager.createNotificationChannel(alertChannel)
    }

    /** 构建前台服务通知 */
    fun buildServiceNotification(): Notification {
        val intent = Intent(context, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            context, 0, intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        return NotificationCompat.Builder(context, CHANNEL_SERVICE)
            .setSmallIcon(R.drawable.ic_launcher_foreground)
            .setContentTitle("Code Cli Connector")
            .setContentText("正在监听 Claude Code 通知")
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }

    /** 发送权限请求通知 */
    fun sendPermissionRequestAlert(notificationId: String, toolName: String?) {
        val intent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            putExtra("notification_id", notificationId)
        }
        val pendingIntent = PendingIntent.getActivity(
            context, nextAlertId, intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val notification = NotificationCompat.Builder(context, CHANNEL_ALERTS)
            .setSmallIcon(R.drawable.ic_launcher_foreground)
            .setContentTitle("权限请求")
            .setContentText("Claude Code 请求执行: ${toolName ?: "未知工具"}")
            .setContentIntent(pendingIntent)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.notify(nextAlertId++, notification)
    }

    /** 发送普通通知 */
    fun sendInfoAlert(title: String?, message: String?) {
        val intent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val pendingIntent = PendingIntent.getActivity(
            context, nextAlertId, intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val notification = NotificationCompat.Builder(context, CHANNEL_ALERTS)
            .setSmallIcon(R.drawable.ic_launcher_foreground)
            .setContentTitle(title ?: "Claude Code")
            .setContentText(message ?: "")
            .setContentIntent(pendingIntent)
            .setAutoCancel(true)
            .build()
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.notify(nextAlertId++, notification)
    }
}
