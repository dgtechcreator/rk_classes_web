-- ============================================================
-- 52_FixFeeStructureReAdd.sql
-- Bug: Fee Head delete is a soft delete (IsActive=0) but FeeStructure has
-- UNIQUE(AcademicYearId, ClassId, SectionId, FeeTypeId) over ALL rows.
-- Re-adding a combination that was deleted earlier hit the unique key and
-- the app showed "server error".
-- Fix: sp_SaveFeeStructure re-activates the soft-deleted row instead of INSERTing.
-- Needs SQL/55 (StudentFees.IsActive) to be applied first. StudentFees rows are never hard-deleted any more.
-- ============================================================
IF OBJECT_ID('sp_SaveFeeStructure','P') IS NOT NULL DROP PROC sp_SaveFeeStructure;
GO
CREATE PROCEDURE sp_SaveFeeStructure
    @StructureId    INT = 0,
    @AcademicYearId INT,
    @ClassId        INT,
    @SectionId      INT,
    @FeeTypeId      INT,
    @Amount         DECIMAL(10,2),
    @DueDay         INT = 10,
    @IsMonthly      BIT = 1,
    @Remarks        NVARCHAR(200) = NULL,
    @CreatedBy      INT = NULL,
    @NewId          INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @StructureId = 0
    BEGIN
        -- Active duplicate -> signal to caller
        IF EXISTS(SELECT 1 FROM FeeStructure
                  WHERE AcademicYearId=@AcademicYearId AND ClassId=@ClassId
                    AND SectionId=@SectionId AND FeeTypeId=@FeeTypeId AND IsActive=1)
        BEGIN
            SET @NewId = -1;
            RETURN;
        END

        -- Previously deleted (IsActive=0) row for the same combination -> re-activate it
        DECLARE @Existing INT = (SELECT TOP 1 StructureId FROM FeeStructure
                  WHERE AcademicYearId=@AcademicYearId AND ClassId=@ClassId
                    AND SectionId=@SectionId AND FeeTypeId=@FeeTypeId AND IsActive=0);
        IF @Existing IS NOT NULL
        BEGIN
            UPDATE FeeStructure
            SET IsActive=1, Amount=@Amount, DueDay=@DueDay, IsMonthly=@IsMonthly,
                Remarks=@Remarks, CreatedBy=@CreatedBy, CreatedAt=GETDATE()
            WHERE StructureId=@Existing;
            SET @NewId = @Existing;
        END
        ELSE
        BEGIN
            INSERT INTO FeeStructure(AcademicYearId,ClassId,SectionId,FeeTypeId,Amount,DueDay,IsMonthly,Remarks,CreatedBy)
            VALUES(@AcademicYearId,@ClassId,@SectionId,@FeeTypeId,@Amount,@DueDay,@IsMonthly,@Remarks,@CreatedBy);
            SET @NewId = SCOPE_IDENTITY();
        END

        -- Auto-apply to existing students matching year+class+section
        -- (re-activate a row that was switched off earlier, otherwise insert)
        UPDATE sf SET IsActive=1, Amount=@Amount, DeactivatedAt=NULL, DeactivatedReason=NULL
        FROM StudentFees sf
        JOIN Students s ON s.StudentId=sf.StudentId
        WHERE sf.StructureId=@NewId AND sf.IsActive=0 AND s.Status='Active'
          AND s.AcademicYearId=@AcademicYearId AND s.ClassId=@ClassId AND s.SectionId=@SectionId;

        INSERT INTO StudentFees(StudentId,StructureId,AcademicYearId,FeeTypeId,Amount,DueDate)
        SELECT s.StudentId, @NewId, @AcademicYearId, @FeeTypeId, @Amount,
               DATEADD(DAY, @DueDay-1, DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
        FROM Students s
        WHERE s.AcademicYearId=@AcademicYearId AND s.ClassId=@ClassId
          AND s.SectionId=@SectionId AND s.Status='Active'
          AND NOT EXISTS(SELECT 1 FROM StudentFees sf
                         WHERE sf.StudentId=s.StudentId AND sf.StructureId=@NewId);

        -- Students of this section may carry an unpaid stand-in row of the same fee type that was copied from
        -- another section of the class while this section had no head (Pay-page fallback). Switch it OFF
        -- (IsActive=0, nothing is deleted), otherwise the app counts both heads.
        UPDATE sf
        SET IsActive=0, DeactivatedAt=GETDATE(), DeactivatedReason='Replaced by own-section fee head'
        FROM StudentFees sf
        JOIN Students s       ON s.StudentId = sf.StudentId
        JOIN FeeStructure old ON old.StructureId = sf.StructureId
        WHERE s.AcademicYearId=@AcademicYearId AND s.ClassId=@ClassId AND s.SectionId=@SectionId AND s.Status='Active'
          AND sf.IsActive=1 AND sf.StructureId <> @NewId AND old.FeeTypeId=@FeeTypeId
          AND old.AcademicYearId=@AcademicYearId AND old.ClassId=@ClassId AND old.SectionId<>@SectionId
          AND ISNULL(sf.PaidAmount,0)=0 AND sf.Status='Pending';
    END
    ELSE
    BEGIN
        UPDATE FeeStructure
        SET Amount=@Amount, DueDay=@DueDay, IsMonthly=@IsMonthly, Remarks=@Remarks
        WHERE StructureId=@StructureId;
        UPDATE StudentFees
        SET Amount=@Amount
        WHERE StructureId=@StructureId AND Status='Pending' AND IsActive=1;
        SET @NewId = @StructureId;
    END
END
GO
