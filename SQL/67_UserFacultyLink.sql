-- 67: explicit link between a login (Users) and a teacher's Faculty profile.
-- Until now the two tables were matched only by guessing (same phone, then same name - TeacherAccountLinker). With this
-- column the admin can pick the Faculty profile on the Users form; NULL keeps the old automatic matching.
-- "One Faculty profile = one login" is enforced by the website, not by an index (a filtered unique index would need
-- QUOTED_IDENTIFIER ON, and the existing user stored procedures were created with it OFF).
-- Safe to run more than once. The website also adds the column by itself the first time a link is saved, so running this
-- up front is optional.
IF COL_LENGTH('dbo.Users', 'FacultyId') IS NULL
    ALTER TABLE dbo.Users ADD FacultyId INT NULL;
GO
PRINT 'Users.FacultyId ready.';
