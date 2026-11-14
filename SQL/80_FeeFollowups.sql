-- 80: fee follow-ups (who followed up with whom, when, what was said, reason, next date).
-- Safe to run more than once. The website also creates this table by itself the first time it needs it
-- (FeeFollowupRepo), so running it up front is optional.
IF OBJECT_ID('dbo.FeeFollowups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FeeFollowups (
        FollowupId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeeFollowups PRIMARY KEY,
        StudentId      INT            NOT NULL,
        FollowupAt     DATETIME       NOT NULL CONSTRAINT DF_FeeFollowups_At DEFAULT GETDATE(),
        FollowedBy     INT            NULL,
        Mode           NVARCHAR(20)   NOT NULL CONSTRAINT DF_FeeFollowups_Mode DEFAULT 'Call',
        SpokeTo        NVARCHAR(30)   NULL,
        Outcome        NVARCHAR(40)   NOT NULL CONSTRAINT DF_FeeFollowups_Outcome DEFAULT '',
        Reason         NVARCHAR(300)  NULL,
        Notes          NVARCHAR(1500) NULL,
        PromisedAmount DECIMAL(12,2)  NULL,
        NextDate       DATE           NULL,
        NextDateBy     NVARCHAR(10)   NULL,
        BalanceAtTime  DECIMAL(12,2)  NULL,
        CreatedAt      DATETIME       NOT NULL CONSTRAINT DF_FeeFollowups_Created DEFAULT GETDATE(),
        IsDeleted      BIT            NOT NULL CONSTRAINT DF_FeeFollowups_Del DEFAULT 0,
        DeletedBy      INT            NULL,
        DeletedAt      DATETIME       NULL
    );
    CREATE INDEX IX_FeeFollowups_Student ON dbo.FeeFollowups (StudentId, FollowupAt DESC);
    CREATE INDEX IX_FeeFollowups_Next    ON dbo.FeeFollowups (NextDate);
END
GO
PRINT 'FeeFollowups ready.';
