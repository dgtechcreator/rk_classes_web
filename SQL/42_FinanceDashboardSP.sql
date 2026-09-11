-- Finance Dashboard Summary SP
-- KPI summary metrics only

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

    -- Total fees from StudentFees
    SELECT @TotalFees = ISNULL(SUM(Amount), 0)
    FROM StudentFees;

    -- Total collected from FeePayments
    SELECT @TotalCollected = ISNULL(SUM(Amount), 0)
    FROM FeePayments
    WHERE IsDeleted = 0;

    -- Total discount from FeePayments
    SELECT @TotalDiscount = ISNULL(SUM(Discount), 0)
    FROM FeePayments
    WHERE IsDeleted = 0;

    -- Calculate collection percentage
    DECLARE @CollectionPercentage DECIMAL(5, 2) = 0;
    IF @TotalFees > 0
        SET @CollectionPercentage = ROUND((@TotalCollected / @TotalFees) * 100, 2);

    -- Return summary only
    SELECT
        @TotalStudents AS TotalStudents,
        @TotalFees AS TotalFees,
        @TotalCollected AS TotalCollected,
        @TotalDiscount AS TotalDiscount,
        (@TotalFees - @TotalCollected) AS TotalBalance,
        @CollectionPercentage AS CollectionPercentage;
END
GO

-- Finance Dashboard Academic Breakdown SP
-- Class/Batch breakdown with collected and discount amounts

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetFinanceDashboardAcademic')
    DROP PROCEDURE sp_GetFinanceDashboardAcademic;
GO

CREATE PROCEDURE sp_GetFinanceDashboardAcademic
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

    -- Return academic breakdown with actual collected/discount per batch
    SELECT
        c.ClassName,
        b.BatchName,
        ISNULL(b.SequenceNo, 0) AS SequenceNo,
        COUNT(DISTINCT s.StudentId) AS StudentCount,
        ISNULL(SUM(sf.TotalFees), 0) AS TotalFees,
        ISNULL(SUM(CASE WHEN fp.IsDeleted = 0 THEN fp.Amount ELSE 0 END), 0) AS EstimatedCollected,
        ISNULL(SUM(CASE WHEN fp.IsDeleted = 0 THEN fp.Discount ELSE 0 END), 0) AS EstimatedDiscount
    FROM Students s
    LEFT JOIN Classes c ON s.ClassId = c.ClassId
    LEFT JOIN Batches b ON s.BatchId = b.BatchId
    LEFT JOIN StudentFees sf ON s.StudentId = sf.StudentId
    LEFT JOIN FeePayments fp ON s.StudentId = fp.StudentId
        AND fp.IsDeleted = 0
    WHERE s.Status = 'Active'
      AND s.AcademicYearId = @AcademicYearId
    GROUP BY c.ClassId, c.ClassName, b.BatchId, b.BatchName, b.SequenceNo
    ORDER BY c.ClassId, ISNULL(b.SequenceNo, 0), b.BatchName;
END
GO
