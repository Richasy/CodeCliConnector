package com.richasy.codecliconnector.ui.screen.notifications

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Cancel
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.DesktopWindows
import androidx.compose.material.icons.filled.DeleteSweep
import androidx.compose.material.icons.filled.HourglassEmpty
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Security
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.richasy.codecliconnector.data.db.NotificationEntity
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable
fun NotificationsScreen(
    viewModel: NotificationsViewModel = hiltViewModel(),
    onNotificationClick: (NotificationEntity) -> Unit = {},
) {
    val notifications by viewModel.notifications.collectAsStateWithLifecycle()

    Column(modifier = Modifier.fillMaxSize()) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                text = "通知",
                style = MaterialTheme.typography.headlineMedium,
                modifier = Modifier.weight(1f),
            )
            if (notifications.isNotEmpty()) {
                IconButton(onClick = { viewModel.clearAll() }) {
                    Icon(Icons.Default.DeleteSweep, contentDescription = "清空通知")
                }
            }
        }

        Box(modifier = Modifier.fillMaxSize()) {
            if (notifications.isEmpty()) {
                Text(
                    text = "暂无通知",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.Center),
                )
            } else {
                LazyColumn(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(horizontal = 16.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    items(notifications, key = { it.id }) { entity ->
                        NotificationCard(
                            entity = entity,
                            onClick = { onNotificationClick(entity) },
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun NotificationCard(
    entity: NotificationEntity,
    onClick: () -> Unit,
) {
    val isPermission = entity.hookEvent == "permission_request"
    val isPending = entity.status == "pending"

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        colors = if (isPermission && isPending) {
            CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
        } else {
            CardDefaults.cardColors()
        },
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.Top,
        ) {
            Icon(
                imageVector = if (isPermission) Icons.Default.Security else Icons.Default.Info,
                contentDescription = null,
                modifier = Modifier.size(24.dp),
                tint = if (isPermission) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.secondary,
            )
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = if (isPermission) {
                        "权限请求: ${entity.toolName ?: "未知工具"}"
                    } else {
                        entity.title ?: "通知"
                    },
                    style = MaterialTheme.typography.titleSmall,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                if (!isPermission && entity.message != null) {
                    Text(
                        text = entity.message,
                        style = MaterialTheme.typography.bodySmall,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                if (isPermission && entity.cwd != null) {
                    Text(
                        text = entity.cwd,
                        style = MaterialTheme.typography.bodySmall,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                val sdf = SimpleDateFormat("MM-dd HH:mm:ss", Locale.getDefault())
                Text(
                    text = sdf.format(Date(entity.createdAt)),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            StatusIcon(entity.status)
        }
    }
}

@Composable
private fun StatusIcon(status: String) {
    when (status) {
        "pending" -> Icon(
            Icons.Default.HourglassEmpty,
            contentDescription = "待处理",
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.primary,
        )
        "approved" -> Icon(
            Icons.Default.CheckCircle,
            contentDescription = "已同意",
            modifier = Modifier.size(18.dp),
            tint = Color(0xFF4CAF50),
        )
        "denied" -> Icon(
            Icons.Default.Cancel,
            contentDescription = "已拒绝",
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.error,
        )
        "handled_elsewhere" -> Icon(
            Icons.Default.DesktopWindows,
            contentDescription = "已在终端处理",
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.outline,
        )
        else -> {}
    }
}
