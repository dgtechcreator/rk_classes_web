-- Finance Dashboard Summary SP
-- Single query to get all finance metrics for dashboard

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetFinanceDashboardSummary')
    DROP PROCEDURE sp_GetFinanceDashboardSummary;
GO

CREATE PROCEDURE sp_GetFinanceDashboardSummary
    @AcademicYearId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to current year if not specified
    IF @AcademicYearId IS NULL
        SELECT TOP 1 @AcademicYearId = YearId
        FROM AcademicYears
        WHERE IsCurrent = 1
        ORDER BY YearId DESC;

    -- Main summary totals
    DECLARE @TotalStudents INT = 0;
    DECLARE @TotalFees DECIMAL(18, 2) = 0;
    DECLARE @TotalCollected DECIMAL(18, 2) = 0;
    DECLARE @TotalDiscount DECIMAL(18, 2) = 0;

    -- Count active students
    SELECT @TotalStudents = COUNT(DISTINCT s.StudentId)
    FROM Students s
    WHERE s.Status = 'Active'
      AND s.AcademicYearId = @AcademicYearId;

    -- Total fees from fee structures
    SELECT @TotalFees = ISNULL(SUM(fs.Amount), 0)
    FROM FeeStructure fs
    WHERE fs.AcademicYearId = @AcademicYearId;

    -- Total collected and discount from fee payments
    SELECT @TotalCollected = ISNULL(SUM(fp.NetAmount), 0),
           @TotalDiscount = ISNULL(SUM(fp.Discount), 0)
    FROM FeePayments fp
    WHERE YEAR(fp.PaymentDate) = YEAR(GETDATE())
      AND fp.IsDeleted = 0;

    -- Calculate collection percentage
    DECLARE @CollectionPercentage DECIMAL(5, 2) = 0;
    IF @TotalFees > 0
        SET @CollectionPercentage = ROUND((@TotalCollected / @TotalFees) * 100, 2);

    -- Return summary
    SELECT
        @TotalStudents AS TotalStudents,
        @TotalFees AS TotalFees,
        @TotalCollected AS TotalCollected,
        @TotalDiscount AS TotalDiscount,
        (@TotalFees - @TotalCollected) AS TotalBalance,
        @CollectionPercentage AS CollectionPercentage;

    -- Return academic breakdown
    SELECT
        c.ClassName,
        b.BatchName,
        COUNT(DISTINCT s.StudentId) AS StudentCount,
        ISNULL(SUM(fs.Amount), 0) AS TotalFees,
        ROUND(ISNULL(SUM(fs.Amount), 0) * (@TotalCollected / NULLIF(@TotalFees, 0)), 2) AS EstimatedCollected,
        ROUND(ISNULL(SUM(fs.Amount), 0) * (@TotalDiscount / NULLIF(@TotalFees, 0)), 2) AS EstimatedDiscount
    FROM Students s
    LEFT JOIN Classes c ON s.ClassId = c.ClassId
    LEFT JOIN Batches b ON s.BatchId = b.BatchId
    LEFT JOIN FeeStructure fs ON s.ClassId = fs.ClassId
        AND s.SectionId = fs.SectionId
        AND fs.AcademicYearId = @AcademicYearId
    WHERE s.Status = 'Active'
      AND s.AcademicYearId = @AcademicYearId
    GROUP BY c.ClassId, c.ClassName, b.BatchId, b.BatchName
    ORDER BY c.ClassName, b.BatchName;
END
GO
