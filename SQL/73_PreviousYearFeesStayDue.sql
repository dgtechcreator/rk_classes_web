-- ============================================================
-- 73_PreviousYearFeesStayDue.sql
-- A student who is in 12th now but still owes fees of 9th / 10th / 11th must show ALL of it (admin, Finance, parent).
-- Two things were hiding the old years' dues:
--  1) sp_SyncStudentFees (runs after every student save) switched off every unpaid fee row that belonged to another
--     class / section / YEAR the moment the student moved up - and StudentFees.PaidAmount is never kept up to date, so every
--     old row looked "unpaid" and the earlier years' dues simply vanished. Now only rows of the SAME academic year but a
--     different class / section are switched off (a wrong-class correction); earlier years' rows always stay.
--  2) The class-wise Finance procedure added up only the Current year's fee rows. It now adds all active rows, like the
--     Finance totals above it already did.
-- Part 3 lists (and, on request, restores) rows that part 1 already switched off. Nothing is restored unless you set @Restore = 1.
-- Safe to run more than once.
-- ============================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_SyncStudentFees] @StudentId INT AS BEGIN
    SET NOCOUNT ON;
    DECLARE @YearId INT, @ClassId INT, @SectionId INT;
    SELECT @YearId=AcademicYearId, @ClassId=ClassId, @SectionId=SectionId FROM Students WHERE StudentId=@StudentId;

    -- Only when the (new) section has its own heads: switch off unpaid rows of THIS year that belong to another class / section.
    -- Rows of earlier academic years are arrears - they are never switched off here.
    IF @SectionId IS NOT NULL AND EXISTS (SELECT 1 FROM FeeStructure WHERE IsActive=1
           AND AcademicYearId=@YearId AND ClassId=@ClassId AND SectionId=@SectionId)
    BEGIN
        UPDATE sf
        SET IsActive=0, DeactivatedAt=GETDATE(), DeactivatedReason='Student moved to another class/section'
        FROM StudentFees sf
        JOIN FeeStructure old ON old.StructureId=sf.StructureId
        WHERE sf.StudentId=@StudentId AND sf.IsActive=1 AND sf.Status='Pending' AND ISNULL(sf.PaidAmount,0)=0
          AND old.AcademicYearId=@YearId
          AND (old.ClassId<>@ClassId OR old.SectionId<>@SectionId);
    END

    EXEC sp_ApplyFeesToStudent @StudentId;
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
                      AND sf.IsActive = 1

                )
                THEN
                (
                    SELECT
                        SUM(ISNULL(sf.Amount, 0))
                    FROM StudentFees sf
                    WHERE sf.StudentId = a.StudentId
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

-- ---- 3) rows an earlier run of the old rule switched off (earlier years' dues) ------------------------------------------
DECLARE @Restore BIT = 0;   -- <<< set to 1 to bring those fee rows back

SELECT s.StudentId, s.AdmissionNo, s.FullName, y.YearName AS FeeYear, c.ClassName AS FeeClass, fs.SectionId,
       sf.Amount, sf.DeactivatedAt
FROM StudentFees sf
JOIN FeeStructure fs ON fs.StructureId = sf.StructureId
JOIN Students s      ON s.StudentId    = sf.StudentId
LEFT JOIN AcademicYears y ON y.YearId  = fs.AcademicYearId
LEFT JOIN Classes c       ON c.ClassId = fs.ClassId
WHERE sf.IsActive = 0 AND sf.DeactivatedReason = 'Student moved to another class/section'
  AND s.Status = 'Active' AND fs.AcademicYearId <> s.AcademicYearId
ORDER BY s.FullName, y.YearName;

IF @Restore = 1
BEGIN
    UPDATE sf SET IsActive = 1, DeactivatedAt = NULL, DeactivatedReason = NULL
    FROM StudentFees sf
    JOIN FeeStructure fs ON fs.StructureId = sf.StructureId
    JOIN Students s      ON s.StudentId    = sf.StudentId
    WHERE sf.IsActive = 0 AND sf.DeactivatedReason = 'Student moved to another class/section'
      AND s.Status = 'Active' AND fs.AcademicYearId <> s.AcademicYearId;
    PRINT 'Earlier years'' fee rows restored: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
END
ELSE PRINT 'Nothing restored. Review the list above, then run again with @Restore = 1.';
GO
