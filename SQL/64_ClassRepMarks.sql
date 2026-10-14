-- ============================================================
-- 64_ClassRepMarks.sql
-- A Class Representative can also enter test marks of his own class. New CRs get the module automatically;
-- this gives it to the CRs that already exist. Safe to run more than once.
-- ============================================================
INSERT INTO UserPermissions(UserId, ModuleId, CanView, CanEdit)
SELECT cr.UserId, m.ModuleId, 1, 1
FROM ClassRepresentatives cr
CROSS JOIN Modules m
WHERE m.ModuleKey = 'marks_entry'
  AND NOT EXISTS (SELECT 1 FROM UserPermissions up WHERE up.UserId = cr.UserId AND up.ModuleId = m.ModuleId);
UPDATE up SET CanView = 1, CanEdit = 1
FROM UserPermissions up
JOIN ClassRepresentatives cr ON cr.UserId = up.UserId
JOIN Modules m ON m.ModuleId = up.ModuleId AND m.ModuleKey = 'marks_entry';
GO
PRINT 'Class Representatives can enter marks.';
GO
