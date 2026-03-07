package com.richasy.codecliconnector.ui.screen.devices

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.richasy.codecliconnector.data.model.DeviceStatusResponse
import com.richasy.codecliconnector.data.network.ApiClient
import com.richasy.codecliconnector.data.repository.SettingsRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class DevicesViewModel @Inject constructor(
    private val apiClient: ApiClient,
    private val settingsRepository: SettingsRepository,
) : ViewModel() {

    private val _devices = MutableStateFlow<List<DeviceStatusResponse>>(emptyList())
    val devices: StateFlow<List<DeviceStatusResponse>> = _devices

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    fun refresh() {
        viewModelScope.launch {
            _isLoading.value = true
            _error.value = null
            val serverUrl = settingsRepository.serverUrl.first()
            val token = settingsRepository.accessToken.first()
            if (serverUrl.isBlank() || token.isBlank()) {
                _error.value = "请先配置服务器并注册设备"
                _isLoading.value = false
                return@launch
            }
            apiClient.getDeviceStatus(serverUrl, token)
                .onSuccess { _devices.value = it }
                .onFailure { _error.value = it.message }
            _isLoading.value = false
        }
    }
}
