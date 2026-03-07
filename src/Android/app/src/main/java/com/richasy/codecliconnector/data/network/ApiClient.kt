package com.richasy.codecliconnector.data.network

import android.util.Log
import com.richasy.codecliconnector.data.model.AuthResponse
import com.richasy.codecliconnector.data.model.DeviceStatusResponse
import com.richasy.codecliconnector.data.model.RefreshTokenRequest
import com.richasy.codecliconnector.data.model.RegisterDeviceRequest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

private const val TAG = "ApiClient"

/** REST API 客户端 */
@Singleton
class ApiClient @Inject constructor() {

    private val json = Json { ignoreUnknownKeys = true }

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .writeTimeout(15, TimeUnit.SECONDS)
        .build()

    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    /** 设备注册 */
    suspend fun register(
        serverUrl: String,
        deviceName: String,
        deviceType: Int,
        preSharedKey: String,
    ): Result<AuthResponse> = withContext(Dispatchers.IO) {
        runCatching {
            val body = json.encodeToString(
                RegisterDeviceRequest.serializer(),
                RegisterDeviceRequest(deviceName, deviceType, preSharedKey),
            )
            val url = "${serverUrl.trimEnd('/')}/api/auth/register"
            Log.d(TAG, "注册请求: POST $url, body=$body")

            val request = Request.Builder()
                .url(url)
                .post(body.toRequestBody(jsonMediaType))
                .build()
            val response = client.newCall(request).execute()
            val responseBody = response.body?.string()
            Log.d(TAG, "注册响应: code=${response.code}, body=$responseBody")

            if (!response.isSuccessful) {
                error("HTTP ${response.code}: ${responseBody ?: "无响应体"}")
            }
            if (responseBody.isNullOrBlank()) {
                error("服务器返回空响应")
            }
            json.decodeFromString(AuthResponse.serializer(), responseBody)
        }.onFailure { e ->
            Log.e(TAG, "注册失败", e)
        }
    }

    /** 刷新令牌 */
    suspend fun refreshToken(serverUrl: String, accessToken: String): Result<AuthResponse> = withContext(Dispatchers.IO) {
        runCatching {
            val body = json.encodeToString(
                RefreshTokenRequest.serializer(),
                RefreshTokenRequest(accessToken),
            )
            val url = "${serverUrl.trimEnd('/')}/api/auth/refresh"
            Log.d(TAG, "刷新令牌: POST $url")

            val request = Request.Builder()
                .url(url)
                .post(body.toRequestBody(jsonMediaType))
                .build()
            val response = client.newCall(request).execute()
            val responseBody = response.body?.string()
            Log.d(TAG, "刷新响应: code=${response.code}, body=$responseBody")

            if (!response.isSuccessful) {
                error("HTTP ${response.code}: ${responseBody ?: "无响应体"}")
            }
            if (responseBody.isNullOrBlank()) {
                error("服务器返回空响应")
            }
            json.decodeFromString(AuthResponse.serializer(), responseBody)
        }.onFailure { e ->
            Log.e(TAG, "刷新令牌失败", e)
        }
    }

    /** 获取所有设备状态 */
    suspend fun getDeviceStatus(serverUrl: String, accessToken: String): Result<List<DeviceStatusResponse>> = withContext(Dispatchers.IO) {
        runCatching {
            val url = "${serverUrl.trimEnd('/')}/api/devices/status"
            Log.d(TAG, "获取设备状态: GET $url")

            val request = Request.Builder()
                .url(url)
                .header("Authorization", "Bearer $accessToken")
                .get()
                .build()
            val response = client.newCall(request).execute()
            val responseBody = response.body?.string()
            Log.d(TAG, "设备状态响应: code=${response.code}, body=$responseBody")

            if (!response.isSuccessful) {
                error("HTTP ${response.code}: ${responseBody ?: "无响应体"}")
            }
            if (responseBody.isNullOrBlank()) {
                error("服务器返回空响应")
            }
            json.decodeFromString<List<DeviceStatusResponse>>(responseBody)
        }.onFailure { e ->
            Log.e(TAG, "获取设备状态失败", e)
        }
    }
}
