package com.richasy.codecliconnector.data.network

import android.util.Log
import com.richasy.codecliconnector.data.model.WebSocketMessage
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.serialization.json.Json
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.util.UUID
import java.util.concurrent.TimeUnit

private const val TAG = "WsClient"

/** WebSocket 连接客户端 */
class WsClient {

    private val json = Json { ignoreUnknownKeys = true }

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(0, TimeUnit.SECONDS)
        .pingInterval(30, TimeUnit.SECONDS)
        .build()

    private var webSocket: WebSocket? = null

    /** 连接并返回收到的消息流 */
    fun connect(
        serverUrl: String,
        accessToken: String,
        onConnected: () -> Unit = {},
    ): Flow<WebSocketMessage> = callbackFlow {
        val wsUrl = serverUrl.trimEnd('/')
            .replace("https://", "wss://")
            .replace("http://", "ws://") + "/ws/connect?token=$accessToken"

        val request = Request.Builder().url(wsUrl).build()
        val listener = object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                Log.i(TAG, "WebSocket 已连接")
                onConnected()
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                try {
                    val msg = json.decodeFromString(WebSocketMessage.serializer(), text)
                    trySend(msg)
                } catch (e: Exception) {
                    Log.e(TAG, "解析 WebSocket 消息失败", e)
                }
            }

            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                Log.i(TAG, "WebSocket 关闭中: $code $reason")
                webSocket.close(1000, null)
                close()
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                Log.e(TAG, "WebSocket 连接失败", t)
                close(CancellationException("WebSocket failure", t))
            }
        }

        webSocket = client.newWebSocket(request, listener)
        awaitClose {
            Log.i(TAG, "关闭 WebSocket 连接")
            webSocket?.close(1000, "客户端关闭")
            webSocket = null
        }
    }

    /** 发送心跳消息 */
    fun sendHeartbeat(deviceId: String): Boolean {
        val msg = WebSocketMessage(
            messageId = UUID.randomUUID().toString().replace("-", ""),
            type = 0, // Heartbeat
            sourceDeviceId = deviceId,
        )
        val text = json.encodeToString(WebSocketMessage.serializer(), msg)
        return webSocket?.send(text) == true
    }

    /** 发送消息 */
    fun send(message: WebSocketMessage): Boolean {
        val text = json.encodeToString(WebSocketMessage.serializer(), message)
        return webSocket?.send(text) == true
    }

    fun disconnect() {
        webSocket?.close(1000, "客户端断开")
        webSocket = null
    }
}
