package com.richasy.codecliconnector

import android.Manifest
import android.content.pm.PackageManager
import android.content.Intent
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Devices
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.core.content.ContextCompat
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.richasy.codecliconnector.service.ConnectionService
import com.richasy.codecliconnector.ui.component.ConnectionStatusBar
import com.richasy.codecliconnector.ui.component.NotificationDetailSheet
import com.richasy.codecliconnector.ui.component.PermissionRequestSheet
import com.richasy.codecliconnector.ui.screen.devices.DevicesScreen
import com.richasy.codecliconnector.ui.screen.notifications.NotificationsScreen
import com.richasy.codecliconnector.ui.screen.notifications.NotificationsViewModel
import com.richasy.codecliconnector.ui.screen.settings.SettingsScreen
import com.richasy.codecliconnector.ui.screen.settings.SettingsViewModel
import com.richasy.codecliconnector.ui.theme.CodeCliConnectorTheme
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    private val notificationPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission(),
    ) { _ -> }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        requestNotificationPermission()
        setContent {
            CodeCliConnectorTheme {
                MainApp()
            }
        }
    }

    private fun requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(
                    this, Manifest.permission.POST_NOTIFICATIONS,
                ) != PackageManager.PERMISSION_GRANTED
            ) {
                notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
    }
}

private data class NavItem(
    val label: String,
    val icon: ImageVector,
)

@Composable
private fun MainApp() {
    val navItems = listOf(
        NavItem("通知", Icons.Default.Notifications),
        NavItem("设备", Icons.Default.Devices),
        NavItem("设置", Icons.Default.Settings),
    )

    var selectedIndex by rememberSaveable { mutableIntStateOf(0) }

    val notificationsViewModel: NotificationsViewModel = hiltViewModel()
    val pendingCount by notificationsViewModel.pendingCount.collectAsStateWithLifecycle()
    val selectedNotification by notificationsViewModel.selectedNotification.collectAsStateWithLifecycle()

    // 自动连接：已注册过且未连接时自动启动服务
    val settingsViewModel: SettingsViewModel = hiltViewModel()
    val deviceId by settingsViewModel.deviceId.collectAsStateWithLifecycle()
    val isConnected by ConnectionService.isConnected.collectAsStateWithLifecycle()
    val context = LocalContext.current

    LaunchedEffect(deviceId) {
        if (deviceId.isNotBlank() && !isConnected) {
            val intent = Intent(context, ConnectionService::class.java).apply {
                action = "CONNECT"
            }
            context.startForegroundService(intent)
        }
    }

    Scaffold(
        modifier = Modifier.fillMaxSize(),
        bottomBar = {
            NavigationBar {
                navItems.forEachIndexed { index, item ->
                    NavigationBarItem(
                        selected = selectedIndex == index,
                        onClick = { selectedIndex = index },
                        icon = {
                            if (index == 0 && pendingCount > 0) {
                                BadgedBox(badge = { Badge { Text("$pendingCount") } }) {
                                    Icon(item.icon, contentDescription = item.label)
                                }
                            } else {
                                Icon(item.icon, contentDescription = item.label)
                            }
                        },
                        label = { Text(item.label) },
                    )
                }
            }
        },
    ) { innerPadding ->
        Column(modifier = Modifier.padding(innerPadding)) {
            ConnectionStatusBar()
            when (selectedIndex) {
                0 -> NotificationsScreen(
                    viewModel = notificationsViewModel,
                    onNotificationClick = { notificationsViewModel.select(it) },
                )
                1 -> DevicesScreen()
                2 -> SettingsScreen()
            }
        }

        // 底部弹出页
        selectedNotification?.let { entity ->
            if (entity.hookEvent == "permission_request") {
                PermissionRequestSheet(
                    entity = entity,
                    onAllow = { notificationsViewModel.respondToPermission(entity, allow = true) },
                    onDeny = { notificationsViewModel.respondToPermission(entity, allow = false) },
                    onDismiss = { notificationsViewModel.select(null) },
                )
            } else {
                NotificationDetailSheet(
                    entity = entity,
                    onDismiss = { notificationsViewModel.select(null) },
                )
            }
        }
    }
}
