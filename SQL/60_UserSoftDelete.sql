-- ============================================================
-- 60_UserSoftDelete.sql
-- Admin can delete a user (soft delete - nothing is removed) and restore it later
-- from Deleted Records. Safe to run more than once. Run this BEFORE deploying the new build.
-- Deleting also sets IsActive=0, so the existing login check (IsActive=1) already blocks the user.
-- ============================================================
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Users') AND name='IsDeleted')
    ALTER TABLE Users ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT 0;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Users') AND name='DeletedAt')
    ALTER TABLE Users ADD DeletedAt DATETIME NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Users') AND name='DeletedBy')
    ALTER TABLE Users ADD DeletedBy INT NULL;
GO
PRINT 'Users.IsDeleted / DeletedAt / DeletedBy ready.';
GO
