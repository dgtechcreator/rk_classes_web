-- ============================================================
-- 55_StudentFeesSoftDelete.sql
-- StudentFees rows are never hard-deleted any more: IsActive=0 switches a row off and EVERY reader ignores it.
-- Also cleans the stand-in rows that made phone (StudentFees) and web Pay page (FeeStructure) disagree.
-- Run order: this file, then 52_FixFeeStructureReAdd.sql.
-- ============================================================
IF COL_LENGTH('StudentFees','IsActive') IS NULL
    ALTER TABLE StudentFees ADD IsActive BIT NOT NULL CONSTRAINT DF_StudentFees_IsActive DEFAULT 1;
IF COL_LENGTH('StudentFees','DeactivatedAt') IS NULL
    ALTER TABLE StudentFees ADD DeactivatedAt DATETIME NULL;
IF COL_LENGTH('StudentFees','DeactivatedReason') IS NULL
    ALTER TABLE StudentFees ADD DeactivatedReason NVARCHAR(200) NULL;
GO
ALTER PROCEDURE [dbo].[sp_ApplyFeesToStudent] @StudentId INT AS BEGIN
    SET NOCOUNT ON;
    DECLARE @YearId INT, @ClassId INT, @SectionId INT;
    SELECT @YearId=AcademicYearId, @ClassId=ClassId, @SectionId=SectionId
    FROM Students WHERE StudentId=@StudentId;

    -- re-activate a row that was switched off earlier for a head that is active again
    UPDATE sf SET IsActive=1, Amount=fs.Amount, DeactivatedAt=NULL, DeactivatedReason=NULL
    FROM StudentFees sf
    JOIN FeeStructure fs ON fs.StructureId=sf.StructureId
    WHERE sf.StudentId=@StudentId AND sf.IsActive=0 AND fs.IsActive=1
      AND fs.AcademicYearId=@YearId AND fs.ClassId=@ClassId AND fs.SectionId=@SectionId;

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
ALTER PROCEDURE [dbo].[sp_DeleteFeeStructure] @StructureId INT AS BEGIN
    UPDATE FeeStructure SET IsActive=0 WHERE StructureId=@StructureId;
    -- Switch off (never delete) the unpaid student fees of this head
    UPDATE StudentFees SET IsActive=0, DeactivatedAt=GETDATE(), DeactivatedReason='Fee head deleted'
    WHERE StructureId=@StructureId AND Status='Pending' AND IsActive=1;
END
GO
ALTER PROCEDURE [dbo].[sp_GetFeesDue]
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
    WHERE sf.IsActive=1
      AND (@AcademicYearId IS NULL OR sf.AcademicYearId=@AcademicYearId)
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
    WHERE sf.IsActive=1
      AND (@AcademicYearId IS NULL OR sf.AcademicYearId=@AcademicYearId)
      AND (@ClassId  IS NULL OR s.ClassId=@ClassId)
      AND (@SectionId IS NULL OR s.SectionId=@SectionId)
      AND (@FeeTypeId IS NULL OR sf.FeeTypeId=@FeeTypeId)
      AND (@Status   IS NULL OR sf.Status=@Status)
    ORDER BY sf.Status, s.FullName
    OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO
ALTER PROCEDURE [dbo].[sp_GetFeeStructureSummary] @AcademicYearId INT = NULL AS BEGIN
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
                                 AND sf.IsActive=1
    WHERE fs.IsActive=1
      AND (@AcademicYearId IS NULL OR fs.AcademicYearId=@AcademicYearId)
    GROUP BY c.ClassName, sec.SectionName, ay.YearName, c.OrderNo
    ORDER BY c.OrderNo, sec.SectionName;
END
GO
ALTER PROCEDURE [dbo].[sp_GetFinanceDashboardAcademic]
    @AcademicYearId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ------------------------------------------------------------
    -- Default Current Academic Year
    ------------------------------------------------------------
    IF @AcademicYearId IS NULL
    BEGIN
        SELECT TOP 1
            @AcademicYearId = YearId
        FROM AcademicYears
        WHERE IsCurrent = 1
        ORDER BY YearId DESC;
    END;


    ------------------------------------------------------------
    -- Active Students Only
    ------------------------------------------------------------
    ;WITH ActiveStudents AS
    (
        SELECT DISTINCT
            StudentId,
            ClassId,
            BatchId,
            SectionId
        FROM Students
        WHERE Status = 'Active'
          AND AcademicYearId = @AcademicYearId
    ),

    ------------------------------------------------------------
    -- Student-wise Total Fees
    -- If StudentFees exists -> use StudentFees
    -- If StudentFees does not exist -> use FeeStructure
    ------------------------------------------------------------
    FeeData AS
    (
        SELECT
            a.StudentId,

            CASE
                WHEN EXISTS
                (
                    SELECT 1
                    FROM StudentFees sf
                    WHERE sf.StudentId = a.StudentId
                      AND sf.AcademicYearId = @AcademicYearId
                      AND sf.IsActive = 1

                )
                THEN
                (
                    SELECT
                        SUM(ISNULL(sf.Amount, 0))
                    FROM StudentFees sf
                    WHERE sf.StudentId = a.StudentId
                      AND sf.AcademicYearId = @AcademicYearId
                      AND sf.IsActive = 1

                )

                ELSE
                (
                    SELECT TOP 1
                        ISNULL(fs.Amount, 0)
                    FROM FeeStructure fs
                    WHERE fs.ClassId = a.ClassId
                      AND (fs.SectionId IS NULL OR fs.SectionId = a.SectionId)
                    ORDER BY
                        CASE
                            WHEN fs.SectionId = a.SectionId THEN 1
                            ELSE 2
                        END
                )
            END AS TotalFees

        FROM ActiveStudents a
    ),

    ------------------------------------------------------------
    -- Student-wise Collected + Discount
    ------------------------------------------------------------
    PaymentData AS
    (
        SELECT
            fp.StudentId,

            SUM(
                CASE
                    WHEN fp.IsDeleted = 0
                    THEN ISNULL(fp.Amount, 0)
                    ELSE 0
                END
            ) AS Collected,

            SUM(
                CASE
                    WHEN fp.IsDeleted = 0
                    THEN ISNULL(fp.Discount, 0)
                    ELSE 0
                END
            ) AS Discount

        FROM FeePayments fp
        INNER JOIN ActiveStudents a
            ON a.StudentId = fp.StudentId
        GROUP BY
            fp.StudentId
    )

    ------------------------------------------------------------
    -- Class / Batch Breakdown
    ------------------------------------------------------------
    SELECT
        c.ClassName,
        b.BatchName,

        COUNT(DISTINCT a.StudentId) AS StudentCount,

        ISNULL(SUM(fd.TotalFees), 0) AS TotalFees,

        ISNULL(SUM(pd.Collected), 0) AS EstimatedCollected,

        ISNULL(SUM(pd.Discount), 0) AS EstimatedDiscount

    FROM ActiveStudents a

    LEFT JOIN Classes c
        ON a.ClassId = c.ClassId

    LEFT JOIN Batches b
        ON a.BatchId = b.BatchId

    LEFT JOIN FeeData fd
        ON a.StudentId = fd.StudentId

    LEFT JOIN PaymentData pd
        ON a.StudentId = pd.StudentId

    GROUP BY
        c.ClassId,
        c.ClassName,
        b.BatchId,
        b.BatchName

    ------------------------------------------------------------
    -- Standard + Batch Order
    ------------------------------------------------------------
    ORDER BY
        CASE
            WHEN LTRIM(RTRIM(c.ClassName)) = '5th' THEN 1
            WHEN LTRIM(RTRIM(c.ClassName)) = '6th' THEN 2
            WHEN LTRIM(RTRIM(c.ClassName)) = '7th' THEN 3
            WHEN LTRIM(RTRIM(c.ClassName)) = '8th' THEN 4
            WHEN LTRIM(RTRIM(c.ClassName)) = '9th' THEN 5
            WHEN LTRIM(RTRIM(c.ClassName)) = '10th' THEN 6
            WHEN LTRIM(RTRIM(c.ClassName)) = '11th' THEN 7
            WHEN LTRIM(RTRIM(c.ClassName)) = '12th' THEN 8
            ELSE 99
        END,

        CASE
            WHEN LOWER(LTRIM(RTRIM(b.BatchName))) LIKE '%morning%'
                 AND LOWER(LTRIM(RTRIM(b.BatchName))) NOT LIKE '%evening%'
                THEN 1

            WHEN LOWER(LTRIM(RTRIM(b.BatchName))) LIKE '%evening%'
                THEN 2

            WHEN LOWER(LTRIM(RTRIM(b.BatchName))) LIKE '%afternoon%'
                THEN 3

            ELSE 4
        END,

        b.BatchName;

END
GO
ALTER PROCEDURE sp_GetFinanceDashboardSummary     
    @AcademicYearId INT = NULL     
AS     
BEGIN     
    SET NOCOUNT ON;     
     
    ------------------------------------------------------------     
    -- Default Current Academic Year     
    ------------------------------------------------------------     
    IF @AcademicYearId IS NULL     
    BEGIN     
        SELECT TOP 1     
            @AcademicYearId = YearId     
        FROM AcademicYears     
        WHERE IsCurrent = 1     
        ORDER BY YearId DESC;     
    END;     
     
     
    ------------------------------------------------------------     
    -- Variables     
    ------------------------------------------------------------     
    DECLARE @TotalStudents INT = 0;     
    DECLARE @TotalFees DECIMAL(18,2) = 0;     
    DECLARE @TotalCollected DECIMAL(18,2) = 0;     
    DECLARE @TotalDiscount DECIMAL(18,2) = 0;     
    DECLARE @CollectionPercentage DECIMAL(18,2) = 0;     
     
     
    ------------------------------------------------------------     
    -- ACTIVE STUDENTS ONLY     
    ------------------------------------------------------------     
    SELECT     
        @TotalStudents = COUNT(DISTINCT s.StudentId)     
    FROM Students s     
    WHERE s.Status = 'Active'     
      AND s.AcademicYearId = @AcademicYearId;     
     
     
    ------------------------------------------------------------     
    -- ACTIVE STUDENTS' TOTAL FEES     
    ------------------------------------------------------------     
    SELECT     
        @TotalFees = ISNULL(SUM(sf.Amount), 0)     
    FROM Students s     
    INNER JOIN StudentFees sf     
        ON sf.StudentId = s.StudentId     
    WHERE s.Status = 'Active'     
      AND s.AcademicYearId = @AcademicYearId     
      AND sf.IsActive = 1;     
     
     
    ------------------------------------------------------------     
    -- ACTIVE STUDENTS' TOTAL COLLECTED     
    ------------------------------------------------------------     
    SELECT     
        @TotalCollected = ISNULL(SUM(fp.Amount), 0)     
    FROM Students s     
    INNER JOIN FeePayments fp     
        ON fp.StudentId = s.StudentId     
    WHERE s.Status = 'Active'     
      AND s.AcademicYearId = @AcademicYearId     
      AND fp.IsDeleted = 0;     
     
     
    ------------------------------------------------------------     
    -- ACTIVE STUDENTS' TOTAL DISCOUNT     
    ------------------------------------------------------------     
    SELECT     
        @TotalDiscount = ISNULL(SUM(fp.Discount), 0)     
    FROM Students s     
    INNER JOIN FeePayments fp     
        ON fp.StudentId = s.StudentId     
    WHERE s.Status = 'Active'     
      AND s.AcademicYearId = @AcademicYearId     
      AND fp.IsDeleted = 0;     
     
     
    ------------------------------------------------------------     
    -- Collection Percentage     
    ------------------------------------------------------------     
    IF @TotalFees > 0     
    BEGIN     
        SET @CollectionPercentage =     
            ROUND(     
                (@TotalCollected / @TotalFees) * 100,     
                2     
            );     
    END;     
     
     
    ------------------------------------------------------------     
    -- MAIN SUMMARY     
    ------------------------------------------------------------     
    SELECT     
        @TotalStudents AS TotalStudents,     
        @TotalFees AS TotalFees,     
        @TotalCollected AS TotalCollected,     
        @TotalDiscount AS TotalDiscount,     
        (@TotalFees - @TotalCollected - @TotalDiscount) AS TotalBalance,     
        @CollectionPercentage AS CollectionPercentage;     
     
     
    ------------------------------------------------------------     
    -- STUDENT-WISE FEES     
    -- Only Active + Selected Academic Year     
    ------------------------------------------------------------     
    ;WITH ActiveStudents AS     
    (     
        SELECT DISTINCT     
            StudentId,     
            ClassId,     
            BatchId     
        FROM Students     
        WHERE Status = 'Active'     
          AND AcademicYearId = @AcademicYearId     
    ),     
     
    StudentFeeData AS     
    (     
        SELECT     
            sf.StudentId,     
            SUM(ISNULL(sf.Amount, 0)) AS TotalFees     
        FROM StudentFees sf     
        INNER JOIN ActiveStudents a     
            ON a.StudentId = sf.StudentId     
        WHERE sf.IsActive = 1     
        GROUP BY sf.StudentId     
    ),     
     
    PaymentData AS     
    (     
        SELECT     
            fp.StudentId,     
     
            SUM(     
                CASE     
                    WHEN fp.IsDeleted = 0     
                    THEN ISNULL(fp.Amount, 0)     
                    ELSE 0     
                END     
            ) AS Collected,     
     
            SUM(     
                CASE     
                    WHEN fp.IsDeleted = 0     
                    THEN ISNULL(fp.Discount, 0)     
                    ELSE 0     
                END     
            ) AS Discount     
     
        FROM FeePayments fp     
        INNER JOIN ActiveStudents a     
            ON a.StudentId = fp.StudentId     
        GROUP BY fp.StudentId     
    )     
     
     
    ------------------------------------------------------------     
    -- CLASS / BATCH BREAKDOWN     
    ------------------------------------------------------------     
    SELECT     
        c.ClassName,     
        b.BatchName,     
     
        COUNT(DISTINCT a.StudentId) AS StudentCount,     
     
        ISNULL(SUM(sfd.TotalFees), 0) AS TotalFees,     
     
        ISNULL(SUM(pd.Collected), 0) AS Collected,     
     
        ISNULL(SUM(pd.Discount), 0) AS Discount     
     
    FROM ActiveStudents a     
     
    LEFT JOIN Classes c     
        ON a.ClassId = c.ClassId     
     
    LEFT JOIN Batches b     
        ON a.BatchId = b.BatchId     
     
    LEFT JOIN StudentFeeData sfd     
        ON a.StudentId = sfd.StudentId     
     
    LEFT JOIN PaymentData pd     
        ON a.StudentId = pd.StudentId     
     
    GROUP BY     
        c.ClassId,     
        c.ClassName,     
        b.BatchId,     
        b.BatchName     
     
    ORDER BY     
        CASE
            WHEN LTRIM(RTRIM(c.ClassName)) = '5th' THEN 1
            WHEN LTRIM(RTRIM(c.ClassName)) = '6th' THEN 2
            WHEN LTRIM(RTRIM(c.ClassName)) = '7th' THEN 3
            WHEN LTRIM(RTRIM(c.ClassName)) = '8th' THEN 4
            WHEN LTRIM(RTRIM(c.ClassName)) = '9th' THEN 5
            WHEN LTRIM(RTRIM(c.ClassName)) = '10th' THEN 6
            WHEN LTRIM(RTRIM(c.ClassName)) = '11th' THEN 7
            WHEN LTRIM(RTRIM(c.ClassName)) = '12th' THEN 8
            ELSE 99
        END,
        CASE
            WHEN LOWER(LTRIM(RTRIM(b.BatchName))) LIKE '%morning%'
                 AND LOWER(LTRIM(RTRIM(b.BatchName))) NOT LIKE '%evening%'
                THEN 1
            WHEN LOWER(LTRIM(RTRIM(b.BatchName))) LIKE '%afternoon%'
                THEN 2
            WHEN LOWER(LTRIM(RTRIM(b.BatchName))) LIKE '%evening%'
                THEN 3
            ELSE 4
        END,
        b.BatchName;     
     
END
GO
ALTER PROCEDURE [dbo].[sp_GetStudentFees] @StudentId INT AS BEGIN
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
    WHERE sf.StudentId = @StudentId AND sf.IsActive = 1
    ORDER BY sf.FeeTypeId, sf.Month;
END
GO
ALTER PROCEDURE sp_GetStudentsByClassAndBatch
  @ClassName NVARCHAR(50),
  @BatchName NVARCHAR(50)
AS BEGIN
  SET NOCOUNT ON;

  -- Fee columns are required aby FeesRepo.GetClassStudentFeeDetails (Finance > Class Detail drill-down
  -- and its CSV export) — without them every student silently shows ₹0 for fees/discount/collected/balance.
  SELECT s.*,
    c.ClassName,
    sec.SectionName,
    b.BatchName,
    ay.YearName,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName,
    COALESCE(f.TotalFees, 0) AS TotalFees,
    COALESCE(f.Discount, 0) AS Discount,
    COALESCE(f.Collected, 0) AS Collected,
    COALESCE(f.TotalFees, 0) - COALESCE(f.Collected, 0) - COALESCE(f.Discount, 0) AS Balance
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId = s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
  LEFT JOIN Batches b ON b.BatchId = s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId = s.AcademicYearId
  LEFT JOIN Users uc ON uc.UserId = s.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = s.DeletedBy
  LEFT JOIN (
    SELECT StudentId, SUM(Amount) AS TotalFees, SUM(PaidAmount) AS Collected, SUM(Discount) AS Discount
    FROM StudentFees
    WHERE IsActive = 1
    GROUP BY StudentId
  ) f ON f.StudentId = s.StudentId
  WHERE c.ClassName = @ClassName AND b.BatchName = @BatchName
  ORDER BY s.FullName;
END
GO
ALTER PROCEDURE sp_DeleteFeePayment
  @PaymentId INT,
  @DeletedBy INT = NULL
AS BEGIN
  SET NOCOUNT ON;

  -- Soft-delete the payment record
  UPDATE FeePayments
  SET IsDeleted = 1,
      DeletedAt = GETDATE(),
      DeletedBy = @DeletedBy
  WHERE PaymentId = @PaymentId;

  -- Reverse the PaidAmount on the matching StudentFee record
  -- Match by StudentId + FeeTypeId + Month + AcademicYearId (best-effort)
  DECLARE @StudentId     INT,
          @FeeTypeId     INT,
          @Month         NVARCHAR(20),
          @AcYearId      INT,
          @NetAmount     DECIMAL(10,2);

  SELECT @StudentId = StudentId,
         @FeeTypeId = FeeTypeId,
         @Month     = Month,
         @AcYearId  = AcademicYearId,
         @NetAmount = NetAmount
  FROM   FeePayments
  WHERE  PaymentId = @PaymentId;

  -- Deduct from StudentFees.PaidAmount and recalculate status
  UPDATE StudentFees
  SET PaidAmount = CASE WHEN PaidAmount - @NetAmount < 0 THEN 0
                        ELSE PaidAmount - @NetAmount END,
      Status     = CASE
                     WHEN (CASE WHEN PaidAmount-@NetAmount<0 THEN 0 ELSE PaidAmount-@NetAmount END) <= 0
                       THEN 'Pending'
                     WHEN (CASE WHEN PaidAmount-@NetAmount<0 THEN 0 ELSE PaidAmount-@NetAmount END) >= (Amount - Discount + LateFine)
                       THEN 'Paid'
                     ELSE 'Partial'
                   END
  WHERE StudentId     = @StudentId
    AND FeeTypeId     = @FeeTypeId
    AND (Month        = @Month OR (@Month IS NULL AND Month IS NULL))
    AND (AcademicYearId = @AcYearId OR @AcYearId IS NULL)
    AND IsActive = 1;

END
GO
ALTER PROCEDURE sp_RestoreFeePayment
  @PaymentId INT
AS BEGIN
  SET NOCOUNT ON;

  -- Un-delete the payment
  UPDATE FeePayments
  SET IsDeleted = 0,
      DeletedAt = NULL,
      DeletedBy = NULL
  WHERE PaymentId = @PaymentId;

  -- Add back the NetAmount to StudentFees.PaidAmount
  DECLARE @StudentId INT, @FeeTypeId INT, @Month NVARCHAR(20),
          @AcYearId INT, @NetAmount DECIMAL(10,2);

  SELECT @StudentId = StudentId,
         @FeeTypeId = FeeTypeId,
         @Month     = Month,
         @AcYearId  = AcademicYearId,
         @NetAmount = NetAmount
  FROM   FeePayments
  WHERE  PaymentId = @PaymentId;

  UPDATE StudentFees
  SET PaidAmount = PaidAmount + @NetAmount,
      Status     = CASE
                     WHEN (PaidAmount + @NetAmount) >= (Amount - Discount + LateFine) THEN 'Paid'
                     WHEN (PaidAmount + @NetAmount) > 0 THEN 'Partial'
                     ELSE 'Pending'
                   END
  WHERE StudentId     = @StudentId
    AND FeeTypeId     = @FeeTypeId
    AND (Month        = @Month OR (@Month IS NULL AND Month IS NULL))
    AND (AcademicYearId = @AcYearId OR @AcYearId IS NULL)
    AND IsActive = 1;

END
GO
-- ============================================================
-- DATA: switch OFF (not delete) unpaid StudentFees rows copied from ANOTHER section of the class while the
-- student's OWN section already has an active head of the same fee type. Only rows created on/after the 13-Oct
-- audit insert (StudentFeeId > 482) are touched, and only if the student has the correct own-section row.
-- Not touched: students whose own section has NO head (11th Science, 12th Commerce/Science), and the three
-- older mismatches (Raunak 331, Rudra 197, Raj 86) - those need a human decision.
-- ============================================================
UPDATE sf
SET IsActive=0, DeactivatedAt=GETDATE(), DeactivatedReason='Stand-in row from another section; own-section head exists'
FROM StudentFees sf
JOIN Students s       ON s.StudentId=sf.StudentId AND s.Status='Active'
JOIN FeeStructure old ON old.StructureId=sf.StructureId
WHERE sf.IsActive=1 AND sf.StudentFeeId>482
  AND (old.ClassId<>s.ClassId OR old.SectionId<>s.SectionId OR old.AcademicYearId<>s.AcademicYearId)
  AND ISNULL(sf.PaidAmount,0)=0 AND sf.Status='Pending'
  AND EXISTS (SELECT 1 FROM StudentFees ok
              JOIN FeeStructure own ON own.StructureId=ok.StructureId
              WHERE ok.StudentId=s.StudentId AND ok.IsActive=1 AND own.IsActive=1
                AND own.AcademicYearId=s.AcademicYearId AND own.ClassId=s.ClassId AND own.SectionId=s.SectionId
                AND own.FeeTypeId=old.FeeTypeId);
PRINT 'rows switched off: ' + CAST(@@ROWCOUNT AS VARCHAR);
GO
