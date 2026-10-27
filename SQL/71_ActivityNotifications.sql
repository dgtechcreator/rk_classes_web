-- 71: activity notifications for parents (attendance, marks, fees, lectures of THEIR children).
-- One row = one thing that happened to one student. A parent sees the rows of every child linked to their phone number;
-- "unread" = newer than ParentAccounts.NotifSeenAt, so two parents of one child each have their own unread badge.
-- Teachers keep using the existing Notifications table (a lecture assigned to a teacher lands there).
-- Safe to run more than once. The website also creates this by itself the first time it needs it (ActivityRepo.EnsureSchema),
-- so running it up front is optional.
IF OBJECT_ID('dbo.StudentActivities', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentActivities (
        ActivityId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentActivities PRIMARY KEY,
        StudentId   INT            NOT NULL,
        Kind        NVARCHAR(30)   NOT NULL,   -- absent, present, late, marks, fee, lecture, lecture_update, lecture_cancel, lecture_done
        Title       NVARCHAR(200)  NOT NULL,
        [Message]   NVARCHAR(1000) NOT NULL,
        DedupeKey   NVARCHAR(150)  NULL,       -- same key = same event: re-saving it updates the row instead of adding another
        ReferenceId INT            NULL,
        CreatedAt   DATETIME       NOT NULL CONSTRAINT DF_StudentActivities_CreatedAt DEFAULT GETDATE()
    );
    CREATE INDEX IX_StudentActivities_Student ON dbo.StudentActivities (StudentId, CreatedAt DESC);
    CREATE INDEX IX_StudentActivities_Dedupe  ON dbo.StudentActivities (StudentId, DedupeKey);
END
GO
IF COL_LENGTH('dbo.ParentAccounts', 'NotifSeenAt') IS NULL
    ALTER TABLE dbo.ParentAccounts ADD NotifSeenAt DATETIME NULL;
GO
PRINT 'StudentActivities + ParentAccounts.NotifSeenAt ready.';
