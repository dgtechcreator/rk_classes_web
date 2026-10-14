-- ============================================================
-- 63_UserGender.sql
-- Staff greeting "Kapil Sir" / "Pooja Ma'am": the gender comes from the linked Faculty profile; for admins / staff who
-- have no Faculty profile it can be set on the user (Users > Edit > Gender). Nullable, safe to run more than once.
-- ============================================================
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Users') AND name='Gender')
    ALTER TABLE Users ADD Gender NVARCHAR(10) NULL;
GO
PRINT 'Users.Gender ready.';
GO
