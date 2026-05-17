-- ============================================================
-- Fee Structure Module  |  03_FeeStructure.sql
-- Run after 01_Schema.sql and 02_SeedAndSPs.sql
-- ============================================================
USE SchoolManagementDB;
GO

-- ── FeeStructure table (setup by admin) ──────────────────────
IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='FeeStructure')
CREATE TABLE FeeStructure (
    StructureId    INT IDENTITY PRIMARY KEY,
    AcademicYearId INT NOT NULL REFERENCES AcademicYears(YearId),
    ClassId        INT NOT NULL REFERENCES Classes(ClassId),
    SectionId      INT NOT NULL REFERENCES Sections(SectionId),
    FeeTypeId      INT NOT NULL REFERENCES FeeTypes(FeeTypeId),
    Amount         DECIMAL(10,2) NOT NULL,
    DueDay         INT DEFAULT 10,           -- day of month fee is due
    IsMonthly      BIT DEFAULT 1,            -- 1=monthly, 0=one-time
    Remarks        NVARCHAR(200),
    IsActive       BIT DEFAULT 1,
    CreatedBy      INT REFERENCES Users(UserId),
    CreatedAt      DATETIME DEFAULT GETDATE(),
    UNIQUE(AcademicYearId, ClassId, SectionId, FeeTypeId)
);
GO

-- ── StudentFees - fees auto-applied when student is added ────
IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='StudentFees')
CREATE TABLE StudentFees (
    StudentFeeId   INT IDENTITY PRIMARY KEY,
    StudentId      INT NOT NULL REFERENCES Students(StudentId),
    StructureId    INT NOT NULL REFERENCES FeeStructure(StructureId),
    AcademicYearId INT NOT NULL REFERENCES AcademicYears(YearId),
    FeeTypeId      INT NOT NULL REFERENCES FeeTypes(FeeTypeId),
    Month          NVARCHAR(20),             -- NULL for one-time fees
    DueDate        DATE,
    Amount         DECIMAL(10,2) NOT NULL,
    PaidAmount     DECIMAL(10,2) DEFAULT 0,
    Discount       DECIMAL(10,2) DEFAULT 0,
    LateFine       DECIMAL(10,2) DEFAULT 0,
    BalanceAmount  AS (Amount - PaidAmount - Discount + LateFine),  -- computed
    Status         NVARCHAR(20) DEFAULT 'Pending',   -- Pending/Partial/Paid/Waived
    CreatedAt      DATETIME DEFAULT GETDATE()
);
GO

-- ── STORED PROCEDURES ────────────────────────────────────────

-- sp_GetFeeStructure
IF OBJECT_ID('sp_GetFeeStructure','P') IS NOT NULL DROP PROC sp_GetFeeStructure; GO
CREATE PROCEDURE sp_GetFeeStructure
    @AcademicYearId INT = NULL,
    @ClassId        INT = NULL,
    @SectionId      INT = NULL
AS BEGIN
    SELECT fs.*,
           ay.YearName, c.ClassName, s.SectionName,
           ft.TypeName AS FeeTypeName,
           u.FullName AS CreatedByName
    FROM   FeeStructure fs
    LEFT JOIN AcademicYears ay ON ay.YearId   = fs.AcademicYearId
    LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
    LEFT JOIN Sections        s  ON s.SectionId = fs.SectionId
    LEFT JOIN FeeTypes        ft ON ft.FeeTypeId= fs.FeeTypeId
    LEFT JOIN Users           u  ON u.UserId    = fs.CreatedBy
    WHERE fs.IsActive = 1
      AND (@AcademicYearId IS NULL OR fs.AcademicYearId = @AcademicYearId)
      AND (@ClassId        IS NULL OR fs.ClassId         = @ClassId)
      AND (@SectionId      IS NULL OR fs.SectionId       = @SectionId)
    ORDER BY ay.YearName, c.OrderNo, s.SectionName, ft.TypeName;
END
GO

-- sp_SaveFeeStructure
IF OBJECT_ID('sp_SaveFeeStructure','P') IS NOT NULL DROP PROC sp_SaveFeeStructure; GO
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
        -- Check duplicate
        IF EXISTS(SELECT 1 FROM FeeStructure
                  WHERE AcademicYearId=@AcademicYearId AND ClassId=@ClassId
                    AND SectionId=@SectionId AND FeeTypeId=@FeeTypeId AND IsActive=1)
        BEGIN
            SET @NewId = -1;   -- signal duplicate
            RETURN;
        END
        INSERT INTO FeeStructure(AcademicYearId,ClassId,SectionId,FeeTypeId,Amount,DueDay,IsMonthly,Remarks,CreatedBy)
        VALUES(@AcademicYearId,@ClassId,@SectionId,@FeeTypeId,@Amount,@DueDay,@IsMonthly,@Remarks,@CreatedBy);
        SET @NewId = SCOPE_IDENTITY();

        -- Auto-apply to existing students matching year+class+section
        INSERT INTO StudentFees(StudentId,StructureId,AcademicYearId,FeeTypeId,Amount,DueDate)
        SELECT s.StudentId, SCOPE_IDENTITY(), @AcademicYearId, @FeeTypeId, @Amount,
               DATEADD(DAY, @DueDay-1, DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
        FROM Students s
        WHERE s.AcademicYearId=@AcademicYearId AND s.ClassId=@ClassId
          AND s.SectionId=@SectionId AND s.Status='Active'
          AND NOT EXISTS(SELECT 1 FROM StudentFees sf
                         WHERE sf.StudentId=s.StudentId AND sf.StructureId=SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE FeeStructure
        SET Amount=@Amount, DueDay=@DueDay, IsMonthly=@IsMonthly, Remarks=@Remarks
        WHERE StructureId=@StructureId;
        -- Update pending student fees too
        UPDATE StudentFees
        SET Amount=@Amount
        WHERE StructureId=@StructureId AND Status='Pending';
        SET @NewId = @StructureId;
    END
END
GO

-- sp_DeleteFeeStructure
IF OBJECT_ID('sp_DeleteFeeStructure','P') IS NOT NULL DROP PROC sp_DeleteFeeStructure; GO
CREATE PROCEDURE sp_DeleteFeeStructure @StructureId INT AS BEGIN
    UPDATE FeeStructure SET IsActive=0 WHERE StructureId=@StructureId;
    -- Remove only unpaid student fees
    DELETE FROM StudentFees WHERE StructureId=@StructureId AND Status='Pending';
END
GO

-- sp_ApplyFeesToStudent  (called when new student is added)
IF OBJECT_ID('sp_ApplyFeesToStudent','P') IS NOT NULL DROP PROC sp_ApplyFeesToStudent; GO
CREATE PROCEDURE sp_ApplyFeesToStudent @StudentId INT AS BEGIN
    SET NOCOUNT ON;
    DECLARE @YearId INT, @ClassId INT, @SectionId INT;
    SELECT @YearId=AcademicYearId, @ClassId=ClassId, @SectionId=SectionId
    FROM Students WHERE StudentId=@StudentId;

    INSERT INTO StudentFees(StudentId, StructureId, AcademicYearId, FeeTypeId, Amount, DueDate)
    SELECT @StudentId, fs.StructureId, fs.AcademicYearId, fs.FeeTypeId, fs.Amount,
           DATEADD(DAY, fs.DueDay-1, DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
    FROM FeeStructure fs
    WHERE fs.AcademicYearId = @YearId
      AND fs.ClassId = @ClassId
      AND fs.SectionId = @SectionId
      AND fs.IsActive = 1
      AND NOT EXISTS(
          SELECT 1 FROM StudentFees sf
          WHERE sf.StudentId=@StudentId AND sf.StructureId=fs.StructureId
      );
END
GO

-- sp_GetStudentFees  (ledger for one student)
IF OBJECT_ID('sp_GetStudentFees','P') IS NOT NULL DROP PROC sp_GetStudentFees; GO
CREATE PROCEDURE sp_GetStudentFees @StudentId INT AS BEGIN
    SELECT sf.*,
           ft.TypeName AS FeeTypeName,
           ay.YearName,
           s.FullName AS StudentName, s.AdmissionNo, s.RollNo,
           c.ClassName, sec.SectionName
    FROM   StudentFees sf
    INNER JOIN Students       s   ON s.StudentId   = sf.StudentId
    INNER JOIN FeeTypes       ft  ON ft.FeeTypeId  = sf.FeeTypeId
    INNER JOIN AcademicYears  ay  ON ay.YearId     = sf.AcademicYearId
    INNER JOIN Classes        c   ON c.ClassId     = s.ClassId
    INNER JOIN Sections       sec ON sec.SectionId = s.SectionId
    WHERE sf.StudentId = @StudentId
    ORDER BY sf.FeeTypeId, sf.Month;
END
GO

-- sp_GetFeesDue  (all pending/partial across all students, filterable)
IF OBJECT_ID('sp_GetFeesDue','P') IS NOT NULL DROP PROC sp_GetFeesDue; GO
CREATE PROCEDURE sp_GetFeesDue
    @AcademicYearId INT = NULL,
    @ClassId        INT = NULL,
    @SectionId      INT = NULL,
    @FeeTypeId      INT = NULL,
    @Status         NVARCHAR(20) = NULL,
    @PageNo         INT = 1,
    @PageSize       INT = 20,
    @TotalCount     INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    SELECT @TotalCount = COUNT(*)
    FROM StudentFees sf
    INNER JOIN Students s ON s.StudentId=sf.StudentId
    WHERE (@AcademicYearId IS NULL OR sf.AcademicYearId=@AcademicYearId)
      AND (@ClassId  IS NULL OR s.ClassId=@ClassId)
      AND (@SectionId IS NULL OR s.SectionId=@SectionId)
      AND (@FeeTypeId IS NULL OR sf.FeeTypeId=@FeeTypeId)
      AND (@Status   IS NULL OR sf.Status=@Status);

    SELECT sf.*,
           ft.TypeName AS FeeTypeName, ay.YearName,
           s.FullName AS StudentName, s.AdmissionNo, s.RollNo,
           c.ClassName, sec.SectionName
    FROM   StudentFees sf
    INNER JOIN Students       s   ON s.StudentId   = sf.StudentId
    INNER JOIN FeeTypes       ft  ON ft.FeeTypeId  = sf.FeeTypeId
    INNER JOIN AcademicYears  ay  ON ay.YearId     = sf.AcademicYearId
    INNER JOIN Classes        c   ON c.ClassId     = s.ClassId
    INNER JOIN Sections       sec ON sec.SectionId = s.SectionId
    WHERE (@AcademicYearId IS NULL OR sf.AcademicYearId=@AcademicYearId)
      AND (@ClassId  IS NULL OR s.ClassId=@ClassId)
      AND (@SectionId IS NULL OR s.SectionId=@SectionId)
      AND (@FeeTypeId IS NULL OR sf.FeeTypeId=@FeeTypeId)
      AND (@Status   IS NULL OR sf.Status=@Status)
    ORDER BY sf.Status, s.FullName
    OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- sp_CollectStudentFee  (collect against a StudentFee record)
IF OBJECT_ID('sp_CollectStudentFee','P') IS NOT NULL DROP PROC sp_CollectStudentFee; GO
CREATE PROCEDURE sp_CollectStudentFee
    @StudentFeeId  INT,
    @PaidAmount    DECIMAL(10,2),
    @Discount      DECIMAL(10,2) = 0,
    @LateFine      DECIMAL(10,2) = 0,
    @PaymentMode   NVARCHAR(30) = 'Cash',
    @TransactionRef NVARCHAR(100) = NULL,
    @Remarks       NVARCHAR(300) = NULL,
    @CollectedBy   INT = NULL,
    @NewPaymentId  INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    -- Update StudentFees record
    UPDATE StudentFees
    SET PaidAmount  = PaidAmount + @PaidAmount,
        Discount    = @Discount,
        LateFine    = @LateFine,
        Status      = CASE
                        WHEN (PaidAmount+@PaidAmount) >= (Amount-@Discount+@LateFine) THEN 'Paid'
                        WHEN (PaidAmount+@PaidAmount) > 0 THEN 'Partial'
                        ELSE 'Pending'
                      END
    WHERE StudentFeeId = @StudentFeeId;

    -- Also insert into FeePayments for receipt
    DECLARE @SId INT, @FTId INT, @AYId INT, @Month NVARCHAR(20);
    SELECT @SId=StudentId, @FTId=FeeTypeId, @AYId=AcademicYearId, @Month=Month
    FROM StudentFees WHERE StudentFeeId=@StudentFeeId;

    DECLARE @RNo NVARCHAR(30) = 'RCP-'+CAST(YEAR(GETDATE()) AS NVARCHAR)+'-'
        +RIGHT('0000'+CAST((SELECT ISNULL(MAX(PaymentId),0)+1 FROM FeePayments) AS NVARCHAR),4);

    INSERT INTO FeePayments(ReceiptNo,StudentId,FeeTypeId,Amount,Discount,LateFine,
        NetAmount,PaymentDate,PaymentMode,TransactionRef,AcademicYearId,Month,Remarks,CollectedBy)
    VALUES(@RNo,@SId,@FTId,@PaidAmount,@Discount,@LateFine,
        @PaidAmount-@Discount+@LateFine,GETDATE(),@PaymentMode,
        @TransactionRef,@AYId,@Month,@Remarks,@CollectedBy);

    SET @NewPaymentId = SCOPE_IDENTITY();
END
GO

-- sp_GenerateMonthlyFees (run at start of each month - can be scheduled)
IF OBJECT_ID('sp_GenerateMonthlyFees','P') IS NOT NULL DROP PROC sp_GenerateMonthlyFees; GO
CREATE PROCEDURE sp_GenerateMonthlyFees
    @Month         NVARCHAR(20),   -- e.g. 'April'
    @AcademicYearId INT
AS BEGIN
    SET NOCOUNT ON;
    DECLARE @DueBase DATE = DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1);

    INSERT INTO StudentFees(StudentId,StructureId,AcademicYearId,FeeTypeId,Amount,Month,DueDate)
    SELECT s.StudentId, fs.StructureId, fs.AcademicYearId, fs.FeeTypeId, fs.Amount,
           @Month, DATEADD(DAY,fs.DueDay-1,@DueBase)
    FROM FeeStructure fs
    INNER JOIN Students s ON s.AcademicYearId=fs.AcademicYearId
                          AND s.ClassId=fs.ClassId AND s.SectionId=fs.SectionId
    WHERE fs.IsActive=1 AND fs.IsMonthly=1
      AND fs.AcademicYearId=@AcademicYearId
      AND s.Status='Active'
      AND NOT EXISTS(
          SELECT 1 FROM StudentFees sf2
          WHERE sf2.StudentId=s.StudentId AND sf2.StructureId=fs.StructureId
            AND sf2.Month=@Month
      );

    SELECT @@ROWCOUNT AS FeesGenerated;
END
GO

-- sp_GetFeeStructureSummary  (dashboard-style totals per class/section)
IF OBJECT_ID('sp_GetFeeStructureSummary','P') IS NOT NULL DROP PROC sp_GetFeeStructureSummary; GO
CREATE PROCEDURE sp_GetFeeStructureSummary @AcademicYearId INT = NULL AS BEGIN
    SELECT c.ClassName, sec.SectionName, ay.YearName,
           COUNT(DISTINCT s.StudentId)                                    AS StudentCount,
           SUM(fs.Amount)                                                 AS TotalMonthlyFee,
           COUNT(fs.StructureId)                                          AS FeeHeads,
           ISNULL(SUM(CASE WHEN sf.Status='Paid'    THEN sf.PaidAmount END),0) AS CollectedAmt,
           ISNULL(SUM(CASE WHEN sf.Status='Pending' THEN sf.Amount     END),0) AS PendingAmt
    FROM FeeStructure fs
    INNER JOIN Classes        c   ON c.ClassId   = fs.ClassId
    INNER JOIN Sections       sec ON sec.SectionId= fs.SectionId
    INNER JOIN AcademicYears  ay  ON ay.YearId   = fs.AcademicYearId
    LEFT  JOIN Students       s   ON s.ClassId   = fs.ClassId
                                 AND s.SectionId  = fs.SectionId
                                 AND s.AcademicYearId=fs.AcademicYearId
                                 AND s.Status='Active'
    LEFT  JOIN StudentFees    sf  ON sf.StudentId= s.StudentId
                                 AND sf.StructureId=fs.StructureId
    WHERE fs.IsActive=1
      AND (@AcademicYearId IS NULL OR fs.AcademicYearId=@AcademicYearId)
    GROUP BY c.ClassName, sec.SectionName, ay.YearName, c.OrderNo
    ORDER BY c.OrderNo, sec.SectionName;
END
GO

PRINT '✅ Fee Structure module created successfully.';
GO
