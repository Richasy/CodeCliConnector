package com.richasy.codecliconnector.ui.screen.notifications

import android.content.Context
import android.content.Intent
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.richasy.codecliconnector.data.db.NotificationEntity
import com.richasy.codecliconnector.data.repository.NotificationRepository
import com.richasy.codecliconnector.service.ConnectionService
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class NotificationsViewModel @Inject constructor(
    private val repository: NotificationRepository,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    val notifications: StateFlow<List<NotificationEntity>> = repository.getAll()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    val pendingCount: StateFlow<Int> = repository.getPendingPermissionCount()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), 0)

    private val _selectedNotificationId = MutableStateFlow<String?>(null)
    private val _selectedNotification = MutableStateFlow<NotificationEntity?>(null)
    val selectedNotification: StateFlow<NotificationEntity?> = _selectedNotification

    init {
        // 当通知列表更新时，同步刷新选中的通知实体
        viewModelScope.launch {
            notifications.collect { list ->
                val selectedId = _selectedNotificationId.value ?: return@collect
                _selectedNotification.value = list.find { it.id == selectedId }
            }
        }
    }

    fun select(entity: NotificationEntity?) {
        _selectedNotificationId.value = entity?.id
        _selectedNotification.value = entity
    }

    /** 简单同意/拒绝 */
    fun respondToPermission(entity: NotificationEntity, allow: Boolean) {
        respondToPermissionAdvanced(
            entity = entity,
            behavior = if (allow) "allow" else "deny",
        )
    }

    /**
     * 高级权限响应（支持"总是允许"等权限规则更新）.
     *
     * @param entity 通知实体
     * @param behavior "allow" 或 "deny"
     * @param updatedPermissions 要应用的权限规则 JSON（如 [{"type":"toolAlwaysAllow","tool":"Bash"}]）
     */
    fun respondToPermissionAdvanced(
        entity: NotificationEntity,
        behavior: String,
        updatedPermissions: String? = null,
    ) {
        viewModelScope.launch {
            val status = if (behavior == "allow") "approved" else "denied"
            repository.updateStatus(entity.id, status)
            _selectedNotificationId.value = null
            _selectedNotification.value = null

            val intent = Intent(context, ConnectionService::class.java).apply {
                action = "RESPOND"
                putExtra("request_id", entity.requestId)
                putExtra("behavior", behavior)
                putExtra("correlation_id", entity.correlationId)
                putExtra("source_device_id", entity.sourceDeviceId)
                if (updatedPermissions != null) putExtra("updated_permissions", updatedPermissions)
            }
            context.startForegroundService(intent)
        }
    }

    fun cleanup() {
        viewModelScope.launch { repository.cleanup() }
    }

    fun clearAll() {
        viewModelScope.launch { repository.clearAll() }
    }
}
