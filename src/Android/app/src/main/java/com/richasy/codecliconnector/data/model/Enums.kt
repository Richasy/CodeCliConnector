package com.richasy.codecliconnector.data.model

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/** 设备类型枚举，与服务端保持一致 */
@Serializable
enum class DeviceType(val value: Int) {
    @SerialName("0") ClaudeCode(0),
    @SerialName("1") Android(1),
    @SerialName("2") IOS(2),
    @SerialName("3") Web(3),
}

/** 消息类型枚举 */
@Serializable
enum class MessageType(val value: Int) {
    @SerialName("0") Heartbeat(0),
    @SerialName("1") Notification(1),
    @SerialName("2") Command(2),
    @SerialName("3") Response(3),
}

/** 消息状态枚举 */
@Serializable
enum class MessageStatus(val value: Int) {
    @SerialName("0") Pending(0),
    @SerialName("1") Delivered(1),
    @SerialName("2") Processed(2),
    @SerialName("3") Expired(3),
}
