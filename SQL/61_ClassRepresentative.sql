-- ============================================================
-- 61_ClassRepresentative.sql
-- Class Representative (CR / Monitor): a student with his own login who can mark ONLY his class's
-- student attendance and add teacher attendance (subject + topic) for his class.
-- Also completes the permission module list and fixes the user permission count.
-- Safe to run more than once. Run BEFORE deploying the new build.
-- ============================================================

-- 1) Role for CR logins
IF NOT EXISTS(SELECT 1 FROM Roles WHERE RoleName='Class Representative')
    INSERT INTO Roles(RoleName) VALUES('Class Representative');
GO

-- 2) The CR table. Class / section / batch are NOT copied: they are read live from the student, so if the
--    student changes class the CR scope follows (and an inactive student automatically loses CR access).
IF NOT EXISTS(SELECT 1 FROM sys.tables WHERE name='ClassRepresentatives')
CREATE TABLE ClassRepresentatives (
    CrId         INT IDENTITY PRIMARY KEY,
    UserId       INT NOT NULL REFERENCES Users(UserId),
    StudentId    INT NOT NULL REFERENCES Students(StudentId),
    LimitToBatch BIT NOT NULL CONSTRAINT DF_ClassRep_LimitToBatch DEFAULT 1,   -- 1 = only the student's own batch (Morning/Evening)
    IsActive     BIT NOT NULL CONSTRAINT DF_ClassRep_IsActive DEFAULT 1,
    CreatedAt    DATETIME NOT NULL CONSTRAINT DF_ClassRep_CreatedAt DEFAULT GETDATE(),
    CreatedBy    INT NULL
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_ClassRep_User' AND object_id=OBJECT_ID('ClassRepresentatives'))
    CREATE UNIQUE INDEX UX_ClassRep_User ON ClassRepresentatives(UserId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_ClassRep_Student' AND object_id=OBJECT_ID('ClassRepresentatives'))
    CREATE UNIQUE INDEX UX_ClassRep_Student ON ClassRepresentatives(StudentId);
GO

-- 3) Permission modules the code already checks but no script created (so they could never be granted
--    from the Permissions screen). Only inserted when the key is missing.
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'teacher_attendance')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Teachers Attendance', 'teacher_attendance', 'fas fa-clock', 'Staff', 120, 1);
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'teacher_payment')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Teachers Payment', 'teacher_payment', 'fas fa-money-bill-wave', 'Staff', 121, 1);
GO

-- 4) "N modules" on the Users list counted every saved row, even fully revoked ones. Count real access only.
IF OBJECT_ID('sp_GetUsers','P') IS NOT NULL DROP PROC sp_GetUsers;
GO
CREATE PROCEDURE sp_GetUsers AS BEGIN
    SELECT u.*, r.RoleName,
           (SELECT COUNT(*) FROM UserPermissions WHERE UserId=u.UserId AND (CanView=1 OR CanEdit=1)) AS PermCount
    FROM Users u
    LEFT JOIN Roles r ON r.RoleId=u.RoleId
    ORDER BY u.FullName;
END
GO
PRINT 'Class Representative tables / role / modules ready.';
GO
