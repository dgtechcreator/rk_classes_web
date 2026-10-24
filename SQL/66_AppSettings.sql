-- 66: AppSettings key/value table. Holds the mobile-app "maintenance mode" switch that the admin flips from the
-- website (App Settings page). The website also creates this table by itself the first time the switch is saved, so
-- running this script is optional - it just does the same thing up front. Safe to run more than once.
IF OBJECT_ID('dbo.AppSettings','U') IS NULL
    CREATE TABLE dbo.AppSettings (
        SettingKey   NVARCHAR(100)  NOT NULL PRIMARY KEY,
        SettingValue NVARCHAR(1000) NULL,
        UpdatedAt    DATETIME       NOT NULL DEFAULT GETDATE(),
        UpdatedBy    INT            NULL
    );
GO
PRINT 'AppSettings ready.';
