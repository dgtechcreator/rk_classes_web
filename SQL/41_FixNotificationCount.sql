-- ============================================================
-- 41_FixNotificationCount.sql — Run this in SSMS
-- Mark all unread notifications as Read
-- ============================================================
USE SchoolManagementDB;
GO

-- Mark ALL unread notifications as Read
UPDATE Notifications
SET Status = 'Read'
WHERE Status = 'Unread';

-- Verify
SELECT COUNT(*) as RemainingUnread FROM Notifications WHERE Status = 'Unread';

PRINT '✅ Done. All unread notifications marked as Read.';
GO
