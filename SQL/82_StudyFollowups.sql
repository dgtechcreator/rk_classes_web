-- 82: study follow-ups = (a) calls to parents about a student's studies / academics (what the parent said, what we told them),
--     (b) oral tests (subject, topic, result: gave it / partly / could not give, optional score).
-- Parents get the oral-test result (and, if chosen, a call summary) as a notification (StudentActivities, Kind = 'study').
-- Safe to run more than once. The website also creates the table + the "Study Follow-up" right by itself the first time it
-- starts (StudyFollowupRepo), so running this script up front is optional.
IF OBJECT_ID('dbo.StudyFollowups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudyFollowups (
        FollowupId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudyFollowups PRIMARY KEY,
        StudentId      INT            NOT NULL,
        FollowupAt     DATETIME       NOT NULL CONSTRAINT DF_StudyFollowups_At DEFAULT GETDATE(),
        FollowedBy     INT            NULL,
        Kind           NVARCHAR(10)   NOT NULL CONSTRAINT DF_StudyFollowups_Kind DEFAULT 'Call',   -- Call | Oral
        Mode           NVARCHAR(20)   NULL,       -- Call, WhatsApp, Visit, SMS, Other      (Kind = Call)
        SpokeTo        NVARCHAR(30)   NULL,       -- Father, Mother, Student, Other         (Kind = Call)
        Category       NVARCHAR(40)   NULL,       -- what the call was about               (Kind = Call)
        Outcome        NVARCHAR(60)   NULL,       -- how the call ended                     (Kind = Call)
        Subject        NVARCHAR(100)  NULL,       --                                        (Kind = Oral)
        Topic          NVARCHAR(200)  NULL,       --                                        (Kind = Oral)
        Result         NVARCHAR(30)   NULL,       -- Gave it | Partly | Could not give | Not taken (Kind = Oral)
        Marks          DECIMAL(6,2)   NULL,
        OutOf          DECIMAL(6,2)   NULL,
        ParentSaid     NVARCHAR(1000) NULL,       -- what the parent said
        Notes          NVARCHAR(1500) NULL,       -- what we said / the conversation / the teacher's remark
        NextDate       DATE           NULL,       -- when to follow up again
        ParentNotified BIT            NOT NULL CONSTRAINT DF_StudyFollowups_Notified DEFAULT 0,
        NotifiedAt     DATETIME       NULL,
        CreatedAt      DATETIME       NOT NULL CONSTRAINT DF_StudyFollowups_Created DEFAULT GETDATE(),
        IsDeleted      BIT            NOT NULL CONSTRAINT DF_StudyFollowups_Del DEFAULT 0,
        DeletedBy      INT            NULL,
        DeletedAt      DATETIME       NULL
    );
    CREATE INDEX IX_StudyFollowups_Student ON dbo.StudyFollowups (StudentId, FollowupAt DESC);
    CREATE INDEX IX_StudyFollowups_At      ON dbo.StudyFollowups (FollowupAt DESC);
END
GO
-- the right that opens the module (Users > Permissions). The admin always has it.
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'study_followup')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Study Follow-up', 'study_followup', 'fas fa-book-reader', 'Academics', 72, 1);
GO
PRINT 'StudyFollowups ready.';
