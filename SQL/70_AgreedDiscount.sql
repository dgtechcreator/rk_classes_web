-- ============================================================
-- 70_AgreedDiscount.sql
-- "Agreed discount" given at admission (e.g. class fee 16,000, agreed 15,000 -> discount 1,000 with a reason).
-- Until now a discount could only be entered on the first payment, so a student's total stayed 16,000 until the first
-- receipt. Now it is stored on the student and counted as Discount in every fee figure (Finance, Pay page, receipts,
-- parent portal, apps). The Pay page's own "Additional Discount" stays and is added on top of it.
--   * Students.AgreedDiscount / AgreedDiscountReason / AgreedDiscountBy / AgreedDiscountAt
--   * sp_SetStudentAgreedDiscount (validated; reason required when amount > 0)
--   * Finance procedures re-created WITH the agreed discount: sp_GetFinanceDashboardSummary,
--     sp_GetFinanceDashboardAcademic, sp_GetStudentsByClassAndBatch (bodies = SQL/55 + SQL/62 live versions)
-- Safe to run more than once. RUN THIS BEFORE deploying the web build that uses it.
-- ============================================================

IF COL_LENGTH('Students','AgreedDiscount') IS NULL
    ALTER TABLE Students ADD AgreedDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Students_AgreedDiscount DEFAULT 0;
IF COL_LENGTH('Students','AgreedDiscountReason') IS NULL
    ALTER TABLE Students ADD AgreedDiscountReason NVARCHAR(200) NULL;
IF COL_LENGTH('Students','AgreedDiscountBy') IS NULL
    ALTER TABLE Students ADD AgreedDiscountBy INT NULL;
IF COL_LENGTH('Students','AgreedDiscountAt') IS NULL
    ALTER TABLE Students ADD AgreedDiscountAt DATETIME NULL;
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_SetStudentAgreedDiscount]
    @StudentId INT,
    @Amount    DECIMAL(18,2),
    @Reason    NVARCHAR(200) = NULL,
    @By        INT = NULL
AS BEGIN
    SET NOCOUNT ON;
    SET @Amount = ISNULL(@Amount, 0);
    SET @Reason = NULLIF(LTRIM(RTRIM(ISNULL(@Reason, N''))), N'');
    IF @Amount < 0
        BEGIN RAISERROR('Agreed discount cannot be negative.', 16, 1); RETURN; END
    IF @Amount > 0 AND @Reason IS NULL
        BEGIN RAISERROR('Please enter the reason for the agreed discount.', 16, 1); RETURN; END
    IF NOT EXISTS (SELECT 1 FROM Students WHERE StudentId = @StudentId)
        BEGIN RAISERROR('Student not found.', 16, 1); RETURN; END

    UPDATE Students
       SET AgreedDiscount       = @Amount,
           AgreedDiscountReason = CASE WHEN @Amount > 0 THEN @Reason ELSE NULL END,
           AgreedDiscountBy     = CASE WHEN @Amount > 0 THEN @By ELSE NULL END,
           AgreedDiscountAt     = CASE WHEN @Amount > 0 THEN GETDATE() ELSE NULL END
     WHERE StudentId = @StudentId
       AND (AgreedDiscount <> @Amount OR ISNULL(AgreedDiscountReason, N'') <> ISNULL(CASE WHEN @Amount > 0 THEN @Reason END, N''));
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetFinanceDashboardSummary]
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
    -- + the discount agreed at admission (Students.AgreedDiscount), so it counts from day one
    SELECT @TotalDiscount = @TotalDiscount + ISNULL(SUM(ISNULL(s.AgreedDiscount, 0)), 0)
    FROM Students s
    WHERE s.Status = 'Active'
      AND s.AcademicYearId = @AcademicYearId;

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
            BatchId,
            ISNULL(AgreedDiscount, 0) AS AgreedDiscount
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

        ISNULL(SUM(pd.Discount), 0) + ISNULL(SUM(a.AgreedDiscount), 0) AS Discount

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

CREATE OR ALTER PROCEDURE [dbo].[sp_GetFinanceDashboardAcademic]
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
            SectionId,
            ISNULL(AgreedDiscount, 0) AS AgreedDiscount
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

        ISNULL(SUM(pd.Discount), 0) + ISNULL(SUM(a.AgreedDiscount), 0) AS EstimatedDiscount

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

-- Finance > class drill-down
CREATE OR ALTER PROCEDURE [dbo].[sp_GetStudentsByClassAndBatch]
  @ClassName NVARCHAR(50),
  @BatchName NVARCHAR(50)
AS BEGIN
  SET NOCOUNT ON;
  -- Fee columns are required by FeesRepo.GetClassStudentFeeDetails (Finance > Class Detail drill-down and its CSV export).
  SELECT s.*,
    c.ClassName,
    sec.SectionName,
    b.BatchName,
    ay.YearName,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName,
    COALESCE(f.TotalFees, 0) AS TotalFees,
    COALESCE(f.Discount, 0) + ISNULL(s.AgreedDiscount, 0) AS Discount,
    COALESCE(f.Collected, 0) AS Collected,
    COALESCE(f.TotalFees, 0) - COALESCE(f.Collected, 0) - COALESCE(f.Discount, 0) - ISNULL(s.AgreedDiscount, 0) AS Balance
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
  WHERE c.ClassName = @ClassName AND b.BatchName = @BatchName AND s.Status = 'Active'
  ORDER BY s.FullName;
END
GO

PRINT 'Agreed discount ready.';
