-- ============================================================
-- 39_AddStudentRole.sql
-- Add Student role to the system
-- ============================================================
USE SchoolManagementDB;
GO

-- Check if Student role already exists, if not add it
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleName = 'Student')
BEGIN
    INSERT INTO Roles (RoleName)
    VALUES ('Student');
    PRINT '✅ Student role added successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Student role already exists.';
END
GO
