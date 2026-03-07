package com.richasy.codecliconnector.ui.component

import android.content.Context
import android.content.Intent
import androidx.compose.animation.animateColorAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Circle
import androidx.compose.material.icons.filled.Link
import androidx.compose.material.icons.filled.LinkOff
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.richasy.codecliconnector.service.ConnectionService

/** 连接状态栏 */
@Composable
fun ConnectionStatusBar() {
    val context = LocalContext.current
    val isConnected by ConnectionService.isConnected.collectAsStateWithLifecycle()
    val error by ConnectionService.connectionError.collectAsStateWithLifecycle()

    val bgColor by animateColorAsState(
        targetValue = if (isConnected) {
            Color(0xFF4CAF50).copy(alpha = 0.12f)
        } else {
            MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.5f)
        },
        label = "statusBg",
    )

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bgColor)
            .padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Icon(
            Icons.Default.Circle,
            contentDescription = null,
            modifier = Modifier.size(10.dp),
            tint = if (isConnected) Color(0xFF4CAF50) else MaterialTheme.colorScheme.error,
        )
        Text(
            text = when {
                isConnected -> "已连接"
                error != null -> error!!
                else -> "未连接"
            },
            style = MaterialTheme.typography.bodySmall,
            modifier = Modifier.weight(1f),
        )
        IconButton(
            onClick = {
                if (isConnected) {
                    stopService(context)
                } else {
                    startService(context)
                }
            },
        ) {
            Icon(
                imageVector = if (isConnected) Icons.Default.LinkOff else Icons.Default.Link,
                contentDescription = if (isConnected) "断开" else "连接",
            )
        }
    }
}

private fun startService(context: Context) {
    val intent = Intent(context, ConnectionService::class.java).apply {
        action = "CONNECT"
    }
    context.startForegroundService(intent)
}

private fun stopService(context: Context) {
    val intent = Intent(context, ConnectionService::class.java).apply {
        action = "DISCONNECT"
    }
    context.startService(intent)
}
