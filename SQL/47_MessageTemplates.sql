-- ============================================================
-- 47_MessageTemplates.sql
-- Message Template master — WhatsApp/SMS texts (absent alert, fee reminder, general notice ...)
-- are stored here and edited from Masters instead of being hardcoded in the mobile app.
-- Body supports placeholders: {student} {class} {medium} {date} {school} {total} {paid} {balance} {receipt} {amount}
-- ============================================================
USE SchoolManagementDB;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MessageTemplates')
CREATE TABLE MessageTemplates (
    TemplateId INT IDENTITY(1,1) PRIMARY KEY,
    Category   NVARCHAR(50)   NOT NULL,          -- Absent, FeeReminder, General ...
    Title      NVARCHAR(100)  NOT NULL,
    Body       NVARCHAR(1000) NOT NULL,
    IsActive   BIT            NOT NULL DEFAULT 1,
    CreatedAt  DATETIME       NOT NULL DEFAULT GETDATE()
);
GO

IF NOT EXISTS (SELECT 1 FROM MessageTemplates)
BEGIN
    INSERT INTO MessageTemplates (Category, Title, Body) VALUES
    ('Absent',      N'Absent Alert',
     N'Dear Parent, {student} ({class}) was absent today ({date}). Please send the reason for absence. - {school}'),
    ('FeeReminder', N'Fee Reminder',
     N'Dear Parent, this is a gentle reminder regarding the fees of {student} ({class}). Total fees: Rs {total}, paid: Rs {paid}, balance due: Rs {balance}. Kindly clear the balance at the earliest. - {school}'),
    ('General',     N'General Notice',
     N'Dear Parent, a message regarding {student} ({class}) from {school}.');
END
GO

IF NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Category = 'Receipt')
    INSERT INTO MessageTemplates (Category, Title, Body) VALUES
    ('Receipt', N'Fee Receipt',
     N'Dear Parent, thank you. Please find attached the fee receipt {receipt} of Rs {amount} for {student} ({class}). - {school}');
GO

IF OBJECT_ID('sp_GetMessageTemplates','P') IS NOT NULL DROP PROC sp_GetMessageTemplates; GO
CREATE PROCEDURE sp_GetMessageTemplates AS BEGIN
    SELECT TemplateId, Category, Title, Body, IsActive
    FROM MessageTemplates WHERE IsActive = 1 ORDER BY Category, Title;
END
GO

IF OBJECT_ID('sp_SaveMessageTemplate','P') IS NOT NULL DROP PROC sp_SaveMessageTemplate; GO
CREATE PROCEDURE sp_SaveMessageTemplate
    @TemplateId INT = 0,
    @Category   NVARCHAR(50),
    @Title      NVARCHAR(100),
    @Body       NVARCHAR(1000),
    @IsActive   BIT = 1,
    @NewId      INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @TemplateId = 0 BEGIN
        INSERT INTO MessageTemplates (Category, Title, Body, IsActive) VALUES (@Category, @Title, @Body, @IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE MessageTemplates SET Category=@Category, Title=@Title, Body=@Body, IsActive=@IsActive
        WHERE TemplateId=@TemplateId;
        SET @NewId = @TemplateId;
    END
END
GO

IF OBJECT_ID('sp_DeleteMessageTemplate','P') IS NOT NULL DROP PROC sp_DeleteMessageTemplate; GO
CREATE PROCEDURE sp_DeleteMessageTemplate @TemplateId INT AS BEGIN
    UPDATE MessageTemplates SET IsActive = 0 WHERE TemplateId = @TemplateId;
END
GO

PRINT '✅ Message templates added.';
GO
