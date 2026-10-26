-- ============================================================
-- 69_CurrentYearEverywhere.sql
-- The year marked "Current" in Masters > Academic Years must drive every student count / list, not only the Finance
-- pages. Until now the Home dashboard, the attendance sheet / grid / report and the fee-payment list counted the
-- active students of ALL years, so switching Current to 2025-2026 still showed the 2026-2027 numbers.
-- Bodies below are the live versions (see 62 / 48 / 05) with only the current-year filter added.
-- Safe to run more than once.
-- ============================================================

-- Home dashboard cards
CREATE OR ALTER PROCEDURE [dbo].[sp_GetDashboardStats] AS BEGIN
  DECLARE @Yr INT = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC);
  SELECT
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND AcademicYearId=@Yr)                                 AS TotalStudents,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND AcademicYearId=@Yr AND CAST(CreatedAt AS DATE)=CAST(GETDATE() AS DATE)) AS NewToday,
    (SELECT COUNT(*) FROM Attendance a INNER JOIN Students s ON s.StudentId=a.StudentId
       WHERE s.Status='Active' AND s.AcademicYearId=@Yr AND a.AttendanceDate=CAST(GETDATE() AS DATE) AND a.Status='Present') AS PresentToday,
    (SELECT COUNT(*) FROM Attendance a INNER JOIN Students s ON s.StudentId=a.StudentId
       WHERE s.Status='Active' AND s.AcademicYearId=@Yr AND a.AttendanceDate=CAST(GETDATE() AS DATE) AND a.Status='Absent')  AS AbsentToday,
    (SELECT ISNULL(SUM(fp.NetAmount),0) FROM FeePayments fp INNER JOIN Students s ON s.StudentId=fp.StudentId
       WHERE s.Status='Active' AND s.AcademicYearId=@Yr AND fp.IsDeleted=0
         AND MONTH(fp.PaymentDate)=MONTH(GETDATE()) AND YEAR(fp.PaymentDate)=YEAR(GETDATE()))                   AS FeesThisMonth,
    (SELECT ISNULL(SUM(Amount),0)   FROM Expenses
       WHERE DeletedAt IS NULL AND MONTH(ExpenseDate)=MONTH(GETDATE()) AND YEAR(ExpenseDate)=YEAR(GETDATE()))    AS ExpensesThisMonth,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND AcademicYearId=@Yr AND ClassId=1) AS Class1Count,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND AcademicYearId=@Yr AND ClassId=2) AS Class2Count,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND AcademicYearId=@Yr AND ClassId=3) AS Class3Count,
    (SELECT COUNT(*) FROM Users WHERE IsActive=1 AND ISNULL(IsDeleted,0)=0)                                      AS TotalStaff;
END
GO

-- Fee payments list (+ its total count): only receipts of the current year's active students.
CREATE OR ALTER PROCEDURE [dbo].[sp_GetFeePayments]
  @PageNo     INT           = 1,
  @PageSize   INT           = 15,
  @Search     NVARCHAR(100) = NULL,
  @Month      NVARCHAR(20)  = NULL,
  @FeeTypeId  INT           = NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  DECLARE @Yr INT = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC);

  SELECT @TotalCount = COUNT(*)
  FROM FeePayments fp
  LEFT JOIN Students s ON s.StudentId = fp.StudentId
  WHERE fp.IsDeleted = 0 AND s.Status = 'Active' AND s.AcademicYearId = @Yr
    AND (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR fp.ReceiptNo LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%')
    AND (@Month IS NULL OR fp.Month = @Month)
    AND (@FeeTypeId IS NULL OR fp.FeeTypeId = @FeeTypeId);

  SELECT fp.*, s.FullName AS StudentName, s.AdmissionNo,
         c.ClassName, sec.SectionName, b.BatchName,
         ft.TypeName AS FeeTypeName, u.FullName AS CollectorName,
         NULL AS StudentPhone, NULL AS FatherPhone,
         NULL AS StudentEmail, NULL AS StudentAddress, NULL AS RollNo
  FROM FeePayments fp
  LEFT JOIN Students s   ON s.StudentId   = fp.StudentId
  LEFT JOIN Classes  c   ON c.ClassId     = s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
  LEFT JOIN Batches  b   ON b.BatchId     = s.BatchId
  LEFT JOIN FeeTypes ft  ON ft.FeeTypeId  = fp.FeeTypeId
  LEFT JOIN Users    u   ON u.UserId      = fp.CollectedBy
  WHERE fp.IsDeleted = 0 AND s.Status = 'Active' AND s.AcademicYearId = @Yr
    AND (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR fp.ReceiptNo LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%')
    AND (@Month IS NULL OR fp.Month = @Month)
    AND (@FeeTypeId IS NULL OR fp.FeeTypeId = @FeeTypeId)
  ORDER BY fp.CreatedAt DESC
  OFFSET ((@PageNo - 1) * @PageSize) ROWS
  FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- Attendance sheet of a date
CREATE OR ALTER PROCEDURE [dbo].[sp_GetAttendance]
    @AttendanceDate DATE,
    @ClassId        INT = NULL,
    @SectionId      INT = NULL,
    @BatchId        INT = NULL
AS BEGIN
    SET NOCOUNT ON;
    SELECT
        s.StudentId,
        s.FullName,
        s.AdmissionNo,
        s.RollNo,
        s.ProfilePicPath,
        c.ClassName,
        sec.SectionName,
        b.BatchName,
        ISNULL(a.Status, 'Present') AS AttendanceStatus,
        a.AttendanceId,
        a.Remarks,
        a.Subject,
        a.SirName,
        a.StartTime,
        a.EndTime,
        s.Phone,
        s.FatherPhone,
        s.MotherPhone
    FROM Students s
    LEFT JOIN Classes   c   ON c.ClassId     = s.ClassId
    LEFT JOIN Sections  sec ON sec.SectionId = s.SectionId
    LEFT JOIN Batches   b   ON b.BatchId     = s.BatchId
    LEFT JOIN Attendance a  ON a.StudentId   = s.StudentId
                           AND a.AttendanceDate = @AttendanceDate
    WHERE s.Status = 'Active'
      AND s.AcademicYearId = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC)
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
    ORDER BY s.RollNo, s.FullName;
END
GO

-- Date-wise attendance grid (student rows)
CREATE OR ALTER PROCEDURE [dbo].[sp_GetAttendanceDateGrid]
    @ClassId   INT = NULL,
    @SectionId INT = NULL,
    @BatchId   INT = NULL,
    @FromDate  DATE,
    @ToDate    DATE
AS BEGIN
    SET NOCOUNT ON;
    SELECT s.StudentId, s.FullName, s.AdmissionNo, s.RollNo,
           c.ClassName, sec.SectionName, b.BatchName
    FROM Students s
    LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
    LEFT JOIN Sections sec ON sec.SectionId= s.SectionId
    LEFT JOIN Batches  b   ON b.BatchId    = s.BatchId
    WHERE s.Status = 'Active'
      AND s.AcademicYearId = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC)
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
    ORDER BY s.RollNo, s.FullName;

    SELECT a.StudentId, a.AttendanceDate, a.Status
    FROM Attendance a
    INNER JOIN Students s ON s.StudentId = a.StudentId
    WHERE a.AttendanceDate BETWEEN @FromDate AND @ToDate
      AND s.AcademicYearId = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC)
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId);
END
GO

-- Monthly attendance report
CREATE OR ALTER PROCEDURE [dbo].[sp_GetAttendanceReport] @ClassId INT=NULL, @Month INT=NULL, @Year INT=NULL AS BEGIN
  SELECT s.StudentId,s.FullName,s.AdmissionNo,c.ClassName,
    COUNT(CASE WHEN a.Status='Present' THEN 1 END) AS PresentDays,
    COUNT(CASE WHEN a.Status='Absent'  THEN 1 END) AS AbsentDays,
    COUNT(CASE WHEN a.Status='Late'    THEN 1 END) AS LateDays,
    COUNT(a.AttendanceId) AS TotalDays,
    CAST(COUNT(CASE WHEN a.Status='Present' THEN 1 END)*100.0/NULLIF(COUNT(a.AttendanceId),0) AS DECIMAL(5,1)) AS AttendancePct,
    (SELECT TOP 1 u.FullName
       FROM Attendance a2
       JOIN Users u ON u.UserId = a2.MarkedBy
      WHERE a2.StudentId = s.StudentId
        AND (@Month IS NULL OR MONTH(a2.AttendanceDate)=@Month)
        AND (@Year  IS NULL OR YEAR(a2.AttendanceDate)=@Year)
      ORDER BY a2.AttendanceDate DESC, a2.AttendanceId DESC) AS CreatedByName
  FROM Students s
  LEFT JOIN Attendance a ON a.StudentId=s.StudentId
    AND (@Month IS NULL OR MONTH(a.AttendanceDate)=@Month)
    AND (@Year  IS NULL OR YEAR(a.AttendanceDate)=@Year)
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  WHERE s.Status='Active'
    AND s.AcademicYearId = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC)
    AND (@ClassId IS NULL OR s.ClassId=@ClassId)
  GROUP BY s.StudentId,s.FullName,s.AdmissionNo,c.ClassName
  ORDER BY s.FullName;
END
GO
