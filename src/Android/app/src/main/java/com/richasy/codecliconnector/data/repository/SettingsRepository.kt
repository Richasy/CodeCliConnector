package com.richasy.codecliconnector.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "settings")

/** 应用设置仓库，用 DataStore 持久化 */
@Singleton
class SettingsRepository @Inject constructor(
    @ApplicationContext private val context: Context,
) {
    private object Keys {
        val SERVER_URL = stringPreferencesKey("server_url")
        val PRE_SHARED_KEY = stringPreferencesKey("pre_shared_key")
        val DEVICE_NAME = stringPreferencesKey("device_name")
        val ACCESS_TOKEN = stringPreferencesKey("access_token")
        val DEVICE_ID = stringPreferencesKey("device_id")
        val TOKEN_EXPIRES_AT = longPreferencesKey("token_expires_at")
    }

    val serverUrl: Flow<String> = context.dataStore.data.map { it[Keys.SERVER_URL] ?: "" }
    val preSharedKey: Flow<String> = context.dataStore.data.map { it[Keys.PRE_SHARED_KEY] ?: "" }
    val deviceName: Flow<String> = context.dataStore.data.map { it[Keys.DEVICE_NAME] ?: android.os.Build.MODEL }
    val accessToken: Flow<String> = context.dataStore.data.map { it[Keys.ACCESS_TOKEN] ?: "" }
    val deviceId: Flow<String> = context.dataStore.data.map { it[Keys.DEVICE_ID] ?: "" }
    val tokenExpiresAt: Flow<Long> = context.dataStore.data.map { it[Keys.TOKEN_EXPIRES_AT] ?: 0L }

    suspend fun setServerUrl(url: String) {
        context.dataStore.edit { it[Keys.SERVER_URL] = url }
    }

    suspend fun setPreSharedKey(key: String) {
        context.dataStore.edit { it[Keys.PRE_SHARED_KEY] = key }
    }

    suspend fun setDeviceName(name: String) {
        context.dataStore.edit { it[Keys.DEVICE_NAME] = name }
    }

    suspend fun saveAuth(token: String, deviceId: String, expiresAt: Long) {
        context.dataStore.edit {
            it[Keys.ACCESS_TOKEN] = token
            it[Keys.DEVICE_ID] = deviceId
            it[Keys.TOKEN_EXPIRES_AT] = expiresAt
        }
    }

    suspend fun clearAuth() {
        context.dataStore.edit {
            it.remove(Keys.ACCESS_TOKEN)
            it.remove(Keys.DEVICE_ID)
            it.remove(Keys.TOKEN_EXPIRES_AT)
        }
    }
}
