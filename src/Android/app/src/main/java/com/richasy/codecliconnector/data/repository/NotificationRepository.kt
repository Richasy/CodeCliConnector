package com.richasy.codecliconnector.data.repository

import com.richasy.codecliconnector.data.db.NotificationDao
import com.richasy.codecliconnector.data.db.NotificationEntity
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

/** 通知数据仓库 */
@Singleton
class NotificationRepository @Inject constructor(
    private val dao: NotificationDao,
) {
    fun getAll(): Flow<List<NotificationEntity>> = dao.getAll()

    fun getPendingPermissionCount(): Flow<Int> = dao.getPendingPermissionCount()

    suspend fun getById(id: String): NotificationEntity? = dao.getById(id)

    suspend fun insert(entity: NotificationEntity) = dao.insert(entity)

    suspend fun updateStatus(id: String, status: String) = dao.updateStatus(id, status)

    suspend fun updateStatusByCorrelationId(correlationId: String, status: String) =
        dao.updateStatusByCorrelationId(correlationId, status)

    /** 清理 7 天前的记录 */
    suspend fun cleanup() {
        val sevenDaysAgo = System.currentTimeMillis() - 7 * 24 * 60 * 60 * 1000L
        dao.deleteOlderThan(sevenDaysAgo)
    }

    /** 清空所有通知 */
    suspend fun clearAll() = dao.deleteAll()
}
