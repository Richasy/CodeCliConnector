package com.richasy.codecliconnector.ui.component

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import com.richasy.codecliconnector.data.db.NotificationEntity
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive

private val lenientJson = Json { ignoreUnknownKeys = true }

/** 权限请求详情底部弹出页 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PermissionRequestSheet(
    entity: NotificationEntity,
    onAllow: () -> Unit,
    onDeny: () -> Unit,
    onAllowWithPermission: (updatedPermissions: String) -> Unit,
    onDismiss: () -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val suggestions = remember(entity.permissionSuggestions) {
        parseSuggestionsRaw(entity.permissionSuggestions)
    }

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 24.dp)
                .padding(bottom = 32.dp)
                .verticalScroll(rememberScrollState()),
        ) {
            Text(
                text = "权限请求",
                style = MaterialTheme.typography.headlineSmall,
            )

            Spacer(Modifier.height(16.dp))

            // 工具名称
            if (entity.toolName != null) {
                LabelValue("工具", entity.toolName)
            }

            // 工作目录
            if (entity.cwd != null) {
                LabelValue("工作目录", entity.cwd)
            }

            // 权限模式
            if (entity.permissionMode != null) {
                LabelValue("权限模式", entity.permissionMode)
            }

            // 工具输入（代码块显示）
            if (!entity.toolInput.isNullOrBlank()) {
                Spacer(Modifier.height(12.dp))
                Text(
                    text = "工具输入",
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.primary,
                )
                Spacer(Modifier.height(4.dp))
                Text(
                    text = formatToolInput(entity.toolInput),
                    style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState())
                        .padding(8.dp),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }

            Spacer(Modifier.height(24.dp))

            // 操作按钮（仅对仍为 pending 状态的请求展示）
            if (entity.status == "pending") {
                // 主操作按钮
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    OutlinedButton(
                        onClick = onDeny,
                        modifier = Modifier.weight(1f),
                    ) {
                        Text("拒绝")
                    }
                    Button(
                        onClick = onAllow,
                        modifier = Modifier.weight(1f),
                    ) {
                        Text("同意")
                    }
                }

                // "总是允许"等建议选项
                if (suggestions.isNotEmpty()) {
                    Spacer(Modifier.height(12.dp))
                    Column(
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        suggestions.forEach { (label, rawJson) ->
                            FilledTonalButton(
                                onClick = { onAllowWithPermission(rawJson) },
                                modifier = Modifier.fillMaxWidth(),
                            ) {
                                Text(label)
                            }
                        }
                    }
                }
            } else {
                Text(
                    text = when (entity.status) {
                        "approved" -> "已同意"
                        "denied" -> "已拒绝"
                        "expired" -> "已过期"
                        "handled_elsewhere" -> "已在终端处理"
                        else -> entity.status
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

/** 通知详情底部弹出页 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NotificationDetailSheet(
    entity: NotificationEntity,
    onDismiss: () -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 24.dp)
                .padding(bottom = 32.dp)
                .verticalScroll(rememberScrollState()),
        ) {
            Text(
                text = entity.title ?: "通知",
                style = MaterialTheme.typography.headlineSmall,
            )

            Spacer(Modifier.height(16.dp))

            if (entity.message != null) {
                Text(
                    text = entity.message,
                    style = MaterialTheme.typography.bodyMedium,
                )
                Spacer(Modifier.height(8.dp))
            }

            if (entity.cwd != null) {
                LabelValue("工作目录", entity.cwd)
            }

            if (entity.notificationType != null) {
                LabelValue("类型", entity.notificationType)
            }
        }
    }
}

@Composable
private fun LabelValue(label: String, value: String) {
    Column(modifier = Modifier.padding(vertical = 4.dp)) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.primary,
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
        )
    }
}

/**
 * 解析权限建议原始 JSON 数组，返回 (显示标签, 原始JSON字符串) 列表.
 * 直接操作 JsonElement 以保留所有字段，确保回传完整性.
 */
private fun parseSuggestionsRaw(json: String?): List<Pair<String, String>> {
    if (json.isNullOrBlank()) return emptyList()
    return try {
        val array = lenientJson.parseToJsonElement(json).jsonArray
        array.map { element ->
            val obj = element.jsonObject
            val label = formatSuggestionLabel(obj)
            // 包装为单元素数组，因为 updatedPermissions 期望的是数组格式
            val rawJson = JsonArray(listOf(element)).toString()
            label to rawJson
        }
    } catch (_: Exception) {
        emptyList()
    }
}

/** 将建议项格式化为用户可读标签 */
private fun formatSuggestionLabel(obj: JsonObject): String {
    val type = obj["type"]?.jsonPrimitive?.content
    return when (type) {
        "toolAlwaysAllow" -> {
            val tool = obj["tool"]?.jsonPrimitive?.content ?: "未知工具"
            "总是允许 $tool"
        }
        "prefixAlwaysAllow" -> {
            val tool = obj["tool"]?.jsonPrimitive?.content ?: "未知工具"
            "总是允许 $tool (前缀匹配)"
        }
        "addDirectories" -> {
            val dirs = try {
                obj["directories"]?.jsonArray?.map { it.jsonPrimitive.content }
            } catch (_: Exception) { null }
            val dirText = dirs?.joinToString(", ") ?: "未知目录"
            "允许访问 $dirText"
        }
        "setMode" -> {
            val mode = obj["mode"]?.jsonPrimitive?.content
            val modeLabel = when (mode) {
                "acceptEdits" -> "接受编辑"
                "plan" -> "计划模式"
                "bypassPermissions" -> "跳过权限"
                else -> mode ?: "未知模式"
            }
            "切换为「$modeLabel」模式"
        }
        else -> type ?: "未知操作"
    }
}

private fun formatToolInput(raw: String): String {
    // 简单格式化 JSON
    return try {
        val sb = StringBuilder()
        var indent = 0
        var inString = false
        var escape = false
        for (c in raw) {
            if (escape) {
                sb.append(c)
                escape = false
                continue
            }
            if (c == '\\' && inString) {
                sb.append(c)
                escape = true
                continue
            }
            if (c == '"') inString = !inString
            when {
                inString -> sb.append(c)
                c == '{' || c == '[' -> {
                    sb.append(c)
                    indent++
                    sb.append('\n')
                    repeat(indent * 2) { sb.append(' ') }
                }
                c == '}' || c == ']' -> {
                    indent--
                    sb.append('\n')
                    repeat(indent * 2) { sb.append(' ') }
                    sb.append(c)
                }
                c == ',' -> {
                    sb.append(c)
                    sb.append('\n')
                    repeat(indent * 2) { sb.append(' ') }
                }
                c == ':' -> sb.append(": ")
                c == ' ' || c == '\n' || c == '\r' || c == '\t' -> { }
                else -> sb.append(c)
            }
        }
        sb.toString()
    } catch (_: Exception) {
        raw
    }
}
