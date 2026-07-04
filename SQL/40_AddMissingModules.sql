-- ============================================================
-- 40_AddMissingModules.sql
-- Add missing modules for menu permission checks
-- ============================================================
USE SchoolManagementDB;
GO

-- Helper: Insert module if not exists
DECLARE @GroupName NVARCHAR(50);
DECLARE @OrderNo INT;

-- Dashboard
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'dashboard_view')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Dashboard', 'dashboard_view', 'fas fa-th-large', 'Overview', 10, 1);
END

-- Masters/Academic Manager
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'masters_view')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Academic Manager', 'masters_view', 'fas fa-layer-group', 'Masters', 20, 1);
END

-- Users & Permissions
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'users_view')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Users & Permissions', 'users_view', 'fas fa-users-cog', 'Masters', 30, 1);
END

-- Student 360 View
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'student_profile')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Student 360 View', 'student_profile', 'fas fa-id-card', 'Students', 60, 1);
END

-- Test Reports
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'marks_report')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Test Reports', 'marks_report', 'fas fa-chart-line', 'Academics', 80, 1);
END

-- Top 5 Students
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'marks_topstudents')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Top 5 Students', 'marks_topstudents', 'fas fa-trophy', 'Academics', 90, 1);
END

-- Deleted Records
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'deleted_records')
BEGIN
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Deleted Records', 'deleted_records', 'fas fa-trash-restore', 'Records', 100, 1);
END

PRINT '✅ Missing modules added.';
GO
