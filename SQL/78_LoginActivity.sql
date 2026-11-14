-- 78: sign-in history (privacy / security) - who signed in, from which phone / browser, when.
-- Safe to run more than once. The website also creates this table by itself the first time it needs it
-- (LoginActivityRepo.EnsureSchema), so running it up front is optional.
IF OBJECT_ID('dbo.LoginActivity', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginActivity (
        LoginId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoginActivity PRIMARY KEY,
        AccountType NVARCHAR(10)  NOT NULL,            -- 'staff' (Users) or 'parent' (ParentAccounts = student / parent login)
        AccountId   INT           NULL,                -- UserId / ParentId (NULL when the login id was unknown)
        LoginName   NVARCHAR(100) NOT NULL CONSTRAINT DF_LoginActivity_LoginName DEFAULT '',   -- what was typed: username / phone (never the password)
        DisplayName NVARCHAR(150) NULL,
        RoleName    NVARCHAR(60)  NULL,
        Success     BIT           NOT NULL,
        Platform    NVARCHAR(20)  NULL,                -- 'app' / 'web'
        Device      NVARCHAR(200) NULL,                -- "Samsung SM-A515F · Android 13" / "Chrome 126 · Windows 10/11"
        AppVersion  NVARCHAR(30)  NULL,
        IpAddress   NVARCHAR(60)  NULL,
        LoggedAt    DATETIME      NOT NULL CONSTRAINT DF_LoginActivity_LoggedAt DEFAULT GETDATE()
    );
    CREATE INDEX IX_LoginActivity_Account ON dbo.LoginActivity (AccountType, AccountId, LoggedAt DESC);
    CREATE INDEX IX_LoginActivity_Time    ON dbo.LoginActivity (LoggedAt DESC);
END
GO
PRINT 'LoginActivity ready.';
