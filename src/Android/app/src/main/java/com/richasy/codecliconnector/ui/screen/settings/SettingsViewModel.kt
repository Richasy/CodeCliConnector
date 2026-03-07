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

    private val _serverUrl = MutableStateFlow("")
    val serverUrl: StateFlow<String> = _serverUrl

    private val _preSharedKey = MutableStateFlow("")
    val preSharedKey: StateFlow<String> = _preSharedKey

    private val _deviceName = MutableStateFlow("")
    val deviceName: StateFlow<String> = _deviceName

    val deviceId: StateFlow<String> = settingsRepository.deviceId
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), "")

    val tokenExpiresAt: StateFlow<Long> = settingsRepository.tokenExpiresAt
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), 0L)

    private val _registering = MutableStateFlow(false)
    val registering: StateFlow<Boolean> = _registering

    private val _message = MutableStateFlow<String?>(null)
    val message: StateFlow<String?> = _message

    init {
        viewModelScope.launch {
            _serverUrl.value = settingsRepository.serverUrl.first()
            _preSharedKey.value = settingsRepository.preSharedKey.first()
            _deviceName.value = settingsRepository.deviceName.first()
        }
    }

    fun setServerUrl(url: String) {
        _serverUrl.value = url
        viewModelScope.launch { settingsRepository.setServerUrl(url) }
    }

    fun setPreSharedKey(key: String) {
        _preSharedKey.value = key
        viewModelScope.launch { settingsRepository.setPreSharedKey(key) }
    }

    fun setDeviceName(name: String) {
        _deviceName.value = name
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
