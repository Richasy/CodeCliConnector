package com.richasy.codecliconnector.service

import android.app.Service
import android.content.Intent
import android.net.wifi.WifiManager
import android.os.IBinder
import android.os.PowerManager
import android.util.Log
import com.richasy.codecliconnector.data.db.NotificationEntity
import com.richasy.codecliconnector.data.model.NotificationPayload
import com.richasy.codecliconnector.data.model.PermissionRequestPayload
import com.richasy.codecliconnector.data.model.PermissionResponsePayload
import com.richasy.codecliconnector.data.model.ProcessedPayload
import com.richasy.codecliconnector.data.model.WebSocketMessage
import com.richasy.codecliconnector.data.network.WsClient
import com.richasy.codecliconnector.data.repository.NotificationRepository
import com.richasy.codecliconnector.data.repository.SettingsRepository
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import java.util.UUID
import javax.inject.Inject

private const val TAG = "ConnectionService"

@AndroidEntryPoint
class ConnectionService : Service() {

    @Inject lateinit var settingsRepository: SettingsRepository
    @Inject lateinit var notificationRepository: NotificationRepository
    @Inject lateinit var notificationHelper: NotificationHelper

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val json = Json { ignoreUnknownKeys = true }
    private var wsClient: WsClient? = null
    private var heartbeatJob: Job? = null
    private var connectionJob: Job? = null
    private var keepAwakeJob: Job? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var wifiLock: WifiManager.WifiLock? = null

    companion object {
        private val _isConnected = MutableStateFlow(false)
        val isConnected: StateFlow<Boolean> = _isConnected

        private val _connectionError = MutableStateFlow<String?>(null)
        val connectionError: StateFlow<String?> = _connectionError
    }

    override fun onCreate() {
        super.onCreate()
        startForeground(
            NotificationHelper.NOTIFY_ID_SERVICE,
            notificationHelper.buildServiceNotification(),
        )
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            "CONNECT" -> startConnection()
            "DISCONNECT" -> stopConnection()
            "RESPOND" -> {
                val requestId = intent.getStringExtra("request_id") ?: return START_STICKY
                val behavior = intent.getStringExtra("behavior") ?: "deny"
                val correlationId = intent.getStringExtra("correlation_id")
                val sourceDeviceId = intent.getStringExtra("source_device_id")
                val updatedPermissions = intent.getStringExtra("updated_permissions")
                scope.launch { sendPermissionResponse(requestId, behavior, correlationId, sourceDeviceId, updatedPermissions) }
            }
        }
        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        stopConnection()
        scope.cancel()
        super.onDestroy()
    }

    private fun startConnection() {
        connectionJob?.cancel()
        connectionJob = scope.launch {
            val serverUrl = settingsRepository.serverUrl.first()
            val token = settingsRepository.accessToken.first()
            val deviceId = settingsRepository.deviceId.first()

            if (serverUrl.isBlank() || token.isBlank()) {
                _connectionError.value = "请先配置服务器地址并注册设备"
                return@launch
            }

            // 监听保活设置变化，动态获取/释放锁
            keepAwakeJob?.cancel()
            keepAwakeJob = scope.launch {
                settingsRepository.keepAwake.collect { enabled ->
                    if (enabled) acquireLocks() else releaseLocks()
                }
            }

            connectWithRetry(serverUrl, token, deviceId)
        }
    }

    private suspend fun connectWithRetry(serverUrl: String, token: String, deviceId: String) {
        var retryDelay = 2000L
        while (true) {
            try {
                _connectionError.value = null
                val client = WsClient()
                wsClient = client

                client.connect(serverUrl, token, onConnected = {
                    _isConnected.value = true
                    retryDelay = 2000L

                    // 启动心跳
                    heartbeatJob?.cancel()
                    heartbeatJob = scope.launch {
                        while (true) {
                            delay(10_000)
                            if (!client.sendHeartbeat(deviceId)) {
                                Log.w(TAG, "心跳发送失败")
                                break
                            }
                        }
                    }
                }).collect { message ->
                    handleMessage(message)
                }
            } catch (e: Exception) {
                Log.e(TAG, "连接断开", e)
            } finally {
                _isConnected.value = false
                heartbeatJob?.cancel()
                wsClient = null
            }

            Log.i(TAG, "将在 ${retryDelay / 1000}s 后重连")
            _connectionError.value = "连接断开，${retryDelay / 1000}s 后重连..."
            delay(retryDelay)
            retryDelay = (retryDelay * 2).coerceAtMost(60_000L)
        }
    }

    private suspend fun handleMessage(message: WebSocketMessage) {
        when (message.type) {
            0 -> { /* 心跳响应，忽略 */ }
            1 -> handleNotification(message)
            2 -> handleCommand(message)
            3 -> handleResponse(message)
            else -> Log.w(TAG, "未知消息类型: ${message.type}")
        }
    }

    private suspend fun handleNotification(message: WebSocketMessage) {
        val payload = message.payload
        if (payload.isBlank()) return

        // 尝试解析为权限请求
        try {
            val permReq = json.decodeFromString(PermissionRequestPayload.serializer(), payload)
            if (permReq.requestId.isNotBlank()) {
                val entity = NotificationEntity(
                    id = message.messageId,
                    hookEvent = "permission_request",
                    sourceDeviceId = message.sourceDeviceId,
                    sessionId = permReq.sessionId,
                    cwd = permReq.cwd,
                    title = null,
                    message = null,
                    notificationType = null,
                    requestId = permReq.requestId,
                    permissionMode = permReq.permissionMode,
                    toolName = permReq.toolName,
                    toolInput = permReq.toolInput,
                    permissionSuggestions = permReq.permissionSuggestions,
                    status = "pending",
                    correlationId = message.correlationId,
                )
                notificationRepository.insert(entity)
                notificationHelper.sendPermissionRequestAlert(message.messageId, permReq.toolName)
                return
            }
        } catch (_: Exception) { }

        // 普通通知
        try {
            val notif = json.decodeFromString(NotificationPayload.serializer(), payload)
            val hookEvent = notif.hookEvent ?: "notification"
            val entity = NotificationEntity(
                id = message.messageId,
                hookEvent = hookEvent,
                sourceDeviceId = message.sourceDeviceId,
                sessionId = notif.sessionId,
                cwd = notif.cwd,
                title = notif.title,
                message = notif.message,
                notificationType = notif.notificationType,
                requestId = null,
                permissionMode = null,
                toolName = null,
                toolInput = null,
                status = "delivered",
                correlationId = message.correlationId,
            )
            notificationRepository.insert(entity)

            if (hookEvent == "stop") {
                notificationHelper.sendStopAlert(notif.title, notif.message)
            } else {
                notificationHelper.sendInfoAlert(notif.title, notif.message)
            }
        } catch (e: Exception) {
            Log.e(TAG, "解析通知载荷失败", e)
        }
    }

    private suspend fun handleCommand(message: WebSocketMessage) {
        Log.i(TAG, "收到指令消息: ${message.messageId}")
    }

    private suspend fun handleResponse(message: WebSocketMessage) {
        val payload = message.payload
        if (payload.isBlank()) return

        try {
            val processed = json.decodeFromString(ProcessedPayload.serializer(), payload)
            if (processed.status == "processed_by_other" && !processed.correlationId.isNullOrBlank()) {
                Log.i(TAG, "权限请求已被其他设备处理: correlationId=${processed.correlationId}")
                notificationRepository.updateStatusByCorrelationId(processed.correlationId, "handled_elsewhere")
                return
            }
        } catch (_: Exception) { }

        // 也尝试解析为 PermissionResponsePayload（behavior=locally_handled 的情况）
        try {
            val resp = json.decodeFromString(PermissionResponsePayload.serializer(), payload)
            if (resp.behavior == "locally_handled" && !message.correlationId.isNullOrBlank()) {
                Log.i(TAG, "权限请求已在终端本地处理: requestId=${resp.requestId}")
                notificationRepository.updateStatusByCorrelationId(message.correlationId, "handled_elsewhere")
            }
        } catch (_: Exception) { }
    }

    private suspend fun sendPermissionResponse(
        requestId: String,
        behavior: String,
        correlationId: String?,
        sourceDeviceId: String?,
        updatedPermissions: String? = null,
    ) {
        val deviceId = settingsRepository.deviceId.first()
        val responsePayload = PermissionResponsePayload(
            requestId = requestId,
            behavior = behavior,
            updatedPermissions = updatedPermissions,
        )
        val payloadJson = json.encodeToString(PermissionResponsePayload.serializer(), responsePayload)
        val wsMsg = WebSocketMessage(
            messageId = UUID.randomUUID().toString().replace("-", ""),
            type = 3, // Response - 与 ConnectorConsole 的 HandleReceivedMessageAsync 匹配
            sourceDeviceId = deviceId,
            targetDeviceId = sourceDeviceId,
            payload = payloadJson,
            correlationId = correlationId,
        )
        val sent = wsClient?.send(wsMsg) == true
        if (sent) {
            Log.i(TAG, "权限响应已发送: $requestId -> $behavior")
        } else {
            Log.e(TAG, "权限响应发送失败: WebSocket 未连接")
        }
    }

    private fun stopConnection() {
        connectionJob?.cancel()
        heartbeatJob?.cancel()
        keepAwakeJob?.cancel()
        wsClient?.disconnect()
        wsClient = null
        _isConnected.value = false
        _connectionError.value = null
        releaseLocks()
    }

    @Suppress("DEPRECATION")
    private fun acquireLocks() {
        if (wakeLock == null) {
            val pm = getSystemService(POWER_SERVICE) as PowerManager
            wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "CCC::WebSocket")
            wakeLock?.acquire()
        }
        if (wifiLock == null) {
            val wm = applicationContext.getSystemService(WIFI_SERVICE) as WifiManager
            wifiLock = wm.createWifiLock(WifiManager.WIFI_MODE_FULL_HIGH_PERF, "CCC::WebSocket")
            wifiLock?.acquire()
        }
    }

    private fun releaseLocks() {
        wakeLock?.let { if (it.isHeld) it.release() }
        wakeLock = null
        wifiLock?.let { if (it.isHeld) it.release() }
        wifiLock = null
    }
}
