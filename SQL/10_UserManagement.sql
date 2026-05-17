-- ============================================================
-- 10_UserManagement.sql
-- User creation, permission modules, role-based access
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Modules (menu items / features) ──────────────────────────
IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Modules')
CREATE TABLE Modules (
    ModuleId    INT IDENTITY PRIMARY KEY,
    ModuleName  NVARCHAR(100) NOT NULL,
    ModuleKey   NVARCHAR(50)  NOT NULL UNIQUE, -- used in code to check permission
    Icon        NVARCHAR(50),
    GroupName   NVARCHAR(50),                  -- for grouping in UI
    OrderNo     INT DEFAULT 0,
    IsActive    BIT DEFAULT 1
);
GO

-- Seed modules
IF NOT EXISTS(SELECT 1 FROM Modules)
INSERT INTO Modules(ModuleName,ModuleKey,Icon,GroupName,OrderNo) VALUES
-- Overview
('Dashboard',          'dashboard',          'fas fa-th-large',           'Overview',  1),
-- Masters
('Masters Setup',      'masters',            'fas fa-database',           'Masters',   10),
-- Faculty
('View Faculty',       'faculty_view',       'fas fa-chalkboard-teacher', 'Faculty',   20),
('Add/Edit Faculty',   'faculty_edit',       'fas fa-user-edit',          'Faculty',   21),
-- Students
('View Students',      'student_view',       'fas fa-user-graduate',      'Students',  30),
('Add/Edit Student',   'student_edit',       'fas fa-user-plus',          'Students',  31),
-- Academics
('Attendance Entry',   'attendance_entry',   'fas fa-calendar-check',     'Academics', 40),
('Attendance Report',  'attendance_report',  'fas fa-chart-bar',          'Academics', 41),
('Date-wise Grid',     'attendance_grid',    'fas fa-calendar-alt',       'Academics', 42),
('Test Marks Entry',   'marks_entry',        'fas fa-marker',             'Academics', 43),
('Test Marks Report',  'marks_report',       'fas fa-print',              'Academics', 44),
-- Finance
('Fee Structure Setup','fee_structure',      'fas fa-cog',                'Finance',   50),
('Fees Due Ledger',    'fees_due',           'fas fa-list-alt',           'Finance',   51),
('Fee Collection',     'fee_collection',     'fas fa-rupee-sign',         'Finance',   52),
('Collect Fee',        'fee_collect',        'fas fa-plus-circle',        'Finance',   53),
('Expenses',           'expenses_view',      'fas fa-receipt',            'Finance',   54),
('Add Expense',        'expenses_edit',      'fas fa-plus-circle',        'Finance',   55);
GO

-- ── UserPermissions ───────────────────────────────────────────
IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='UserPermissions')
CREATE TABLE UserPermissions (
    PermissionId INT IDENTITY PRIMARY KEY,
    UserId       INT NOT NULL REFERENCES Users(UserId),
    ModuleId     INT NOT NULL REFERENCES Modules(ModuleId),
    CanView      BIT DEFAULT 1,
    CanEdit      BIT DEFAULT 0,
    UNIQUE(UserId, ModuleId)
);
GO

-- Add extra columns to Users if not exists
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Users') AND name='IsPasswordChanged')
    ALTER TABLE Users ADD IsPasswordChanged BIT DEFAULT 0;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Users') AND name='CreatedBy')
    ALTER TABLE Users ADD CreatedBy INT NULL REFERENCES Users(UserId);
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Users') AND name='ProfilePicPath')
    ALTER TABLE Users ADD ProfilePicPath NVARCHAR(500) NULL;
GO

-- ── Grant all permissions to Admin by default ─────────────────
IF NOT EXISTS(SELECT 1 FROM UserPermissions WHERE UserId=1)
INSERT INTO UserPermissions(UserId,ModuleId,CanView,CanEdit)
SELECT 1, ModuleId, 1, 1 FROM Modules WHERE IsActive=1;
GO

-- ── SPs ───────────────────────────────────────────────────────

-- Get all users with roles
IF OBJECT_ID('sp_GetUsers','P') IS NOT NULL DROP PROC sp_GetUsers; GO
CREATE PROCEDURE sp_GetUsers AS BEGIN
    SELECT u.*, r.RoleName,
           (SELECT COUNT(*) FROM UserPermissions WHERE UserId=u.UserId) AS PermCount
    FROM Users u
    LEFT JOIN Roles r ON r.RoleId=u.RoleId
    ORDER BY u.FullName;
END
GO

-- Get single user with permissions
IF OBJECT_ID('sp_GetUserById','P') IS NOT NULL DROP PROC sp_GetUserById; GO
CREATE PROCEDURE sp_GetUserById @UserId INT AS BEGIN
    SELECT u.*, r.RoleName
    FROM Users u LEFT JOIN Roles r ON r.RoleId=u.RoleId
    WHERE u.UserId=@UserId;
    -- permissions
    SELECT m.*, ISNULL(up.CanView,0) AS CanView, ISNULL(up.CanEdit,0) AS CanEdit
    FROM Modules m
    LEFT JOIN UserPermissions up ON up.ModuleId=m.ModuleId AND up.UserId=@UserId
    WHERE m.IsActive=1
    ORDER BY m.OrderNo;
END
GO

-- Create/Update user
IF OBJECT_ID('sp_SaveUser','P') IS NOT NULL DROP PROC sp_SaveUser; GO
CREATE PROCEDURE sp_SaveUser
    @UserId    INT = 0,
    @FullName  NVARCHAR(150),
    @Username  NVARCHAR(100),
    @Password  NVARCHAR(300),
    @RoleId    INT,
    @Email     NVARCHAR(150) = NULL,
    @Phone     NVARCHAR(20)  = NULL,
    @IsActive  BIT = 1,
    @CreatedBy INT = NULL,
    @NewId     INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @UserId = 0 BEGIN
        IF EXISTS(SELECT 1 FROM Users WHERE Username=@Username)
        BEGIN SET @NewId=-1; RETURN; END
        INSERT INTO Users(FullName,Username,PasswordHash,RoleId,Email,Phone,IsActive,CreatedBy)
        VALUES(@FullName,@Username,@Password,@RoleId,@Email,@Phone,@IsActive,@CreatedBy);
        SET @NewId=SCOPE_IDENTITY();
        -- Auto-grant dashboard permission
        INSERT INTO UserPermissions(UserId,ModuleId,CanView,CanEdit)
        SELECT @NewId, ModuleId, 1, 0 FROM Modules WHERE ModuleKey='dashboard';
    END ELSE BEGIN
        UPDATE Users SET FullName=@FullName, RoleId=@RoleId,
            Email=@Email, Phone=@Phone, IsActive=@IsActive,
            PasswordHash=CASE WHEN @Password<>'' THEN @Password ELSE PasswordHash END
        WHERE UserId=@UserId;
        SET @NewId=@UserId;
    END
END
GO

-- Save permissions for a user
IF OBJECT_ID('sp_SaveUserPermissions','P') IS NOT NULL DROP PROC sp_SaveUserPermissions; GO
CREATE PROCEDURE sp_SaveUserPermissions
    @UserId INT,
    @ModuleId INT,
    @CanView BIT,
    @CanEdit BIT
AS BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM UserPermissions WHERE UserId=@UserId AND ModuleId=@ModuleId)
        UPDATE UserPermissions SET CanView=@CanView, CanEdit=@CanEdit
        WHERE UserId=@UserId AND ModuleId=@ModuleId;
    ELSE
        INSERT INTO UserPermissions(UserId,ModuleId,CanView,CanEdit)
        VALUES(@UserId,@ModuleId,@CanView,@CanEdit);
END
GO

-- Get user permissions as a flat string (for session caching)
IF OBJECT_ID('sp_GetUserPermissions','P') IS NOT NULL DROP PROC sp_GetUserPermissions; GO
CREATE PROCEDURE sp_GetUserPermissions @UserId INT AS BEGIN
    SELECT m.ModuleKey, up.CanView, up.CanEdit
    FROM UserPermissions up
    INNER JOIN Modules m ON m.ModuleId=up.ModuleId
    WHERE up.UserId=@UserId AND m.IsActive=1;
END
GO

-- Change password
IF OBJECT_ID('sp_ChangePassword','P') IS NOT NULL DROP PROC sp_ChangePassword; GO
CREATE PROCEDURE sp_ChangePassword
    @UserId      INT,
    @OldPassword NVARCHAR(300),
    @NewPassword NVARCHAR(300),
    @Result      NVARCHAR(50) OUTPUT
AS BEGIN
    IF NOT EXISTS(SELECT 1 FROM Users WHERE UserId=@UserId AND PasswordHash=@OldPassword)
    BEGIN SET @Result='WRONG_PASSWORD'; RETURN; END
    UPDATE Users SET PasswordHash=@NewPassword, IsPasswordChanged=1 WHERE UserId=@UserId;
    SET @Result='OK';
END
GO

PRINT '✅ User Management + Permissions created.';
GO
