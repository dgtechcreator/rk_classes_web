-- Mark Notification as Read
IF EXISTS (SELECT * FROM sys.objects WHERE type='P' AND name='sp_MarkNotificationRead')
    DROP PROCEDURE sp_MarkNotificationRead;
GO

CREATE PROCEDURE sp_MarkNotificationRead
    @NotificationId INT
AS
BEGIN
    UPDATE Notifications
    SET Status = 'Read', ReadAt = GETDATE()
    WHERE NotificationId = @NotificationId
END
GO

-- Dismiss Notification
IF EXISTS (SELECT * FROM sys.objects WHERE type='P' AND name='sp_DismissNotification')
    DROP PROCEDURE sp_DismissNotification;
GO

CREATE PROCEDURE sp_DismissNotification
    @NotificationId INT
AS
BEGIN
    UPDATE Notifications
    SET Status = 'Dismissed'
    WHERE NotificationId = @NotificationId
END
GO
