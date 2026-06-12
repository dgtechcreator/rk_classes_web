-- =============================================
-- 33_ParentPortal.sql
-- Parent login portal — table + stored procedures
-- Run once on the live database (SSMS → Open → Execute)
-- =============================================

-- 1. ParentAccounts table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ParentAccounts')
BEGIN
    CREATE TABLE [dbo].[ParentAccounts] (
        [ParentId]  INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ParentAccounts] PRIMARY KEY,
        [Phone]     NVARCHAR(20)  NOT NULL,
        [Password]  NVARCHAR(100) NOT NULL,
        [FullName]  NVARCHAR(200) NULL,
        [IsActive]  BIT NOT NULL CONSTRAINT [DF_ParentAccounts_IsActive] DEFAULT 1,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_ParentAccounts_CreatedAt] DEFAULT GETDATE(),
        [LastLogin] DATETIME NULL
    );
    CREATE UNIQUE INDEX [UQ_ParentAccounts_Phone] ON [dbo].[ParentAccounts]([Phone]);
    PRINT 'ParentAccounts table created.';
END
ELSE
    PRINT 'ParentAccounts table already exists — skipped.';
GO

-- 2. sp_ParentGetByPhone — look up parent account by phone
CREATE OR ALTER PROCEDURE [dbo].[sp_ParentGetByPhone]
    @Phone NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ParentId, Phone, [Password], FullName, IsActive, CreatedAt, LastLogin
    FROM   ParentAccounts
    WHERE  Phone = @Phone;
END
GO

-- 3. sp_ParentSave — insert (register) or update parent account
CREATE OR ALTER PROCEDURE [dbo].[sp_ParentSave]
    @ParentId    INT           = 0,
    @Phone       NVARCHAR(20),
    @Password    NVARCHAR(100),
    @FullName    NVARCHAR(200) = NULL,
    @NewParentId INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @ParentId = 0
    BEGIN
        INSERT INTO ParentAccounts (Phone, [Password], FullName)
        VALUES (@Phone, @Password, @FullName);
        SET @NewParentId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE ParentAccounts
        SET    [Password] = @Password,
               FullName   = ISNULL(@FullName, FullName)
        WHERE  ParentId   = @ParentId;
        SET @NewParentId = @ParentId;
    END
END
GO

-- 4. sp_ParentUpdateLastLogin
CREATE OR ALTER PROCEDURE [dbo].[sp_ParentUpdateLastLogin]
    @ParentId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ParentAccounts SET LastLogin = GETDATE() WHERE ParentId = @ParentId;
END
GO

-- 5. sp_GetChildrenByParentPhone — students whose FatherPhone or MotherPhone matches
CREATE OR ALTER PROCEDURE [dbo].[sp_GetChildrenByParentPhone]
    @Phone NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.StudentId, s.AdmissionNo, s.FullName, s.DateOfBirth, s.Gender,
        s.FatherName, s.MotherName, s.Phone, s.FatherPhone, s.MotherPhone,
        s.ProfilePicPath, s.RollNo, s.BloodGroup, s.Status, s.AdmissionDate,
        s.AcademicYearId, ay.YearName,
        s.ClassId,   c.ClassName,
        s.SectionId, sec.SectionName,
        s.BatchId,   b.BatchName
    FROM   Students s
    LEFT JOIN AcademicYears ay  ON ay.YearId    = s.AcademicYearId
    LEFT JOIN Classes       c   ON c.ClassId    = s.ClassId
    LEFT JOIN Sections      sec ON sec.SectionId= s.SectionId
    LEFT JOIN Batches       b   ON b.BatchId    = s.BatchId
    WHERE  (s.FatherPhone = @Phone OR s.MotherPhone = @Phone)
      AND  ISNULL(s.Status, 'Active') <> 'Deleted'
    ORDER BY s.FullName;
END
GO

PRINT 'Parent portal stored procedures created/updated successfully.';
