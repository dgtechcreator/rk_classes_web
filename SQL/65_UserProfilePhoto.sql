-- 65: make sure Users has a ProfilePicPath column (it was added by 10_UserManagement.sql, this is the safety net
-- for databases that were set up before that). Used by the profile photo upload on the web Users form and in the mobile app.
-- Safe to run more than once. Nothing is changed when the column is already there.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ProfilePicPath')
    ALTER TABLE Users ADD ProfilePicPath NVARCHAR(500) NULL;
GO
PRINT 'Users.ProfilePicPath ready.';
