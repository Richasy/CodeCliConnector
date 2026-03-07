package com.richasy.codecliconnector.ui.screen.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.richasy.codecliconnector.data.model.DeviceType
import com.richasy.codecliconnector.data.network.ApiClient
import com.richasy.codecliconnector.data.repository.SettingsRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val settingsRepository: SettingsRepository,
    private val apiClient: ApiClient,
) : ViewModel() {

    val serverUrl: StateFlow<String> = settingsRepository.serverUrl
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), "")

    val preSharedKey: StateFlow<String> = settingsRepository.preSharedKey
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), "")

    val deviceName: StateFlow<String> = settingsRepository.deviceName
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), "")

    val deviceId: StateFlow<String> = settingsRepository.deviceId
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), "")

    val tokenExpiresAt: StateFlow<Long> = settingsRepository.tokenExpiresAt
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), 0L)

    private val _registering = MutableStateFlow(false)
    val registering: StateFlow<Boolean> = _registering

    private val _message = MutableStateFlow<String?>(null)
    val message: StateFlow<String?> = _message

    fun setServerUrl(url: String) {
        viewModelScope.launch { settingsRepository.setServerUrl(url) }
    }

    fun setPreSharedKey(key: String) {
        viewModelScope.launch { settingsRepository.setPreSharedKey(key) }
    }

    fun setDeviceName(name: String) {
        viewModelScope.launch { settingsRepository.setDeviceName(name) }
    }

    fun register() {
        viewModelScope.launch {
            _registering.value = true
            _message.value = null
            val serverUrl = settingsRepository.serverUrl.first()
            val psk = settingsRepository.preSharedKey.first()
            val name = settingsRepository.deviceName.first()

            if (serverUrl.isBlank() || psk.isBlank()) {
                _message.value = "请填写服务器地址和预共享密钥"
                _registering.value = false
                return@launch
            }

            apiClient.register(serverUrl, name, DeviceType.Android.value, psk)
                .onSuccess { auth ->
                    settingsRepository.saveAuth(auth.accessToken, auth.deviceId, auth.expiresAt)
                    _message.value = "注册成功"
                }
                .onFailure {
                    val detail = it.cause?.message ?: it.message ?: it.toString()
                    _message.value = "注册失败: $detail"
                }

            _registering.value = false
        }
    }

    fun refreshToken() {
        viewModelScope.launch {
            _registering.value = true
            _message.value = null
            val serverUrl = settingsRepository.serverUrl.first()
            val token = settingsRepository.accessToken.first()

            apiClient.refreshToken(serverUrl, token)
                .onSuccess { auth ->
                    settingsRepository.saveAuth(auth.accessToken, auth.deviceId, auth.expiresAt)
                    _message.value = "令牌已刷新"
                }
                .onFailure {
                    val detail = it.cause?.message ?: it.message ?: it.toString()
                    _message.value = "刷新失败: $detail"
                }

            _registering.value = false
        }
    }

    fun clearMessage() {
        _message.value = null
    }
}
