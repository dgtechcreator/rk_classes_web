-- ============================================================
-- 07_FeeTypeMaster.sql
-- Add FeeType CRUD stored procedures
-- ============================================================
USE SchoolManagementDB;
GO

-- Add Description column if not exists
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FeeTypes') AND name='Description')
    ALTER TABLE FeeTypes ADD Description NVARCHAR(200) NULL;
GO

IF OBJECT_ID('sp_GetFeeTypes','P') IS NOT NULL DROP PROC sp_GetFeeTypes; GO
CREATE PROCEDURE sp_GetFeeTypes AS BEGIN
    SELECT ft.*,
           (SELECT COUNT(*) FROM FeeStructure fs WHERE fs.FeeTypeId=ft.FeeTypeId AND fs.IsActive=1) AS StructureCount,
           (SELECT COUNT(*) FROM FeePayments  fp WHERE fp.FeeTypeId=ft.FeeTypeId)                   AS PaymentCount
    FROM FeeTypes ft
    ORDER BY ft.FeeTypeId;
END
GO

IF OBJECT_ID('sp_SaveFeeType','P') IS NOT NULL DROP PROC sp_SaveFeeType; GO
CREATE PROCEDURE sp_SaveFeeType
    @FeeTypeId   INT = 0,
    @TypeName    NVARCHAR(100),
    @Description NVARCHAR(200) = NULL,
    @IsActive    BIT = 1,
    @NewId       INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @FeeTypeId = 0 BEGIN
        IF EXISTS(SELECT 1 FROM FeeTypes WHERE TypeName=@TypeName AND IsActive=1)
        BEGIN SET @NewId=-1; RETURN; END
        INSERT INTO FeeTypes(TypeName,Description,IsActive) VALUES(@TypeName,@Description,@IsActive);
        SET @NewId=SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE FeeTypes SET TypeName=@TypeName,Description=@Description,IsActive=@IsActive
        WHERE FeeTypeId=@FeeTypeId;
        SET @NewId=@FeeTypeId;
    END
END
GO

IF OBJECT_ID('sp_DeleteFeeType','P') IS NOT NULL DROP PROC sp_DeleteFeeType; GO
CREATE PROCEDURE sp_DeleteFeeType @FeeTypeId INT AS BEGIN
    IF EXISTS(SELECT 1 FROM FeeStructure WHERE FeeTypeId=@FeeTypeId AND IsActive=1)
        RAISERROR('Cannot delete: this fee type is used in an active fee structure.',16,1);
    ELSE IF EXISTS(SELECT 1 FROM FeePayments WHERE FeeTypeId=@FeeTypeId)
        RAISERROR('Cannot delete: payments exist for this fee type.',16,1);
    ELSE
        UPDATE FeeTypes SET IsActive=0 WHERE FeeTypeId=@FeeTypeId;
END
GO

PRINT '✅ FeeType master SPs created.';
GO
