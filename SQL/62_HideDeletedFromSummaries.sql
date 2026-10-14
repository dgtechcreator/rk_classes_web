-- ============================================================
-- 62_HideDeletedFromSummaries.sql
-- A deleted / inactive student must disappear from every summary, list and report (it shows up on
-- Deleted Records, where the admin can restore it). A deleted expense must not be counted either.
-- Bodies below are the LIVE versions (checked 2026-10-14) with only the Active / not-deleted filter added.
-- Safe to run more than once.
-- ============================================================

-- Dashboard cards: only active students count (new today, present, absent, fees this month), deleted expenses and
-- deleted / inactive staff are not counted.
CREATE OR ALTER PROCEDURE [dbo].[sp_GetDashboardStats] AS BEGIN
  SELECT
    (SELECT COUNT(*) FROM Students WHERE Status='Active')                                                        AS TotalStudents,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND CAST(CreatedAt AS DATE)=CAST(GETDATE() AS DATE))   AS NewToday,
    (SELECT COUNT(*) FROM Attendance a INNER JOIN Students s ON s.StudentId=a.StudentId
       WHERE s.Status='Active' AND a.AttendanceDate=CAST(GETDATE() AS DATE) AND a.Status='Present')              AS PresentToday,
    (SELECT COUNT(*) FROM Attendance a INNER JOIN Students s ON s.StudentId=a.StudentId
       WHERE s.Status='Active' AND a.AttendanceDate=CAST(GETDATE() AS DATE) AND a.Status='Absent')               AS AbsentToday,
    (SELECT ISNULL(SUM(fp.NetAmount),0) FROM FeePayments fp INNER JOIN Students s ON s.StudentId=fp.StudentId
       WHERE s.Status='Active' AND fp.IsDeleted=0
         AND MONTH(fp.PaymentDate)=MONTH(GETDATE()) AND YEAR(fp.PaymentDate)=YEAR(GETDATE()))                   AS FeesThisMonth,
    (SELECT ISNULL(SUM(Amount),0)   FROM Expenses
       WHERE DeletedAt IS NULL AND MONTH(ExpenseDate)=MONTH(GETDATE()) AND YEAR(ExpenseDate)=YEAR(GETDATE()))    AS ExpensesThisMonth,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND ClassId=1) AS Class1Count,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND ClassId=2) AS Class2Count,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND ClassId=3) AS Class3Count,
    (SELECT COUNT(*) FROM Users WHERE IsActive=1 AND ISNULL(IsDeleted,0)=0)                                      AS TotalStaff;
END
GO

-- Fee payments list (+ its total count): receipts of deleted / inactive students are not listed.
CREATE OR ALTER PROCEDURE [dbo].[sp_GetFeePayments]
  @PageNo     INT           = 1,
  @PageSize   INT           = 15,
  @Search     NVARCHAR(100) = NULL,
  @Month      NVARCHAR(20)  = NULL,
  @FeeTypeId  INT           = NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;

  SELECT @TotalCount = COUNT(*)
  FROM FeePayments fp
  LEFT JOIN Students s ON s.StudentId = fp.StudentId
  WHERE fp.IsDeleted = 0 AND s.Status = 'Active'
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
  WHERE fp.IsDeleted = 0 AND s.Status = 'Active'
    AND (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR fp.ReceiptNo LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%')
    AND (@Month IS NULL OR fp.Month = @Month)
    AND (@FeeTypeId IS NULL OR fp.FeeTypeId = @FeeTypeId)
  ORDER BY fp.CreatedAt DESC
  OFFSET ((@PageNo - 1) * @PageSize) ROWS
  FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- Fees due ledger: deleted / inactive students are not listed (nor counted).
CREATE OR ALTER PROCEDURE [dbo].[sp_GetFeesDue]
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
    WHERE sf.IsActive=1 AND s.Status='Active'
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
    WHERE sf.IsActive=1 AND s.Status='Active'
      AND (@AcademicYearId IS NULL OR sf.AcademicYearId=@AcademicYearId)
      AND (@ClassId  IS NULL OR s.ClassId=@ClassId)
      AND (@SectionId IS NULL OR s.SectionId=@SectionId)
      AND (@FeeTypeId IS NULL OR sf.FeeTypeId=@FeeTypeId)
      AND (@Status   IS NULL OR sf.Status=@Status)
    ORDER BY sf.Status, s.FullName
    OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- Marks / test reports: only active students.
CREATE OR ALTER PROCEDURE [dbo].[sp_GetMarks] @ExamId INT, @ClassId INT=NULL AS BEGIN
  SELECT tm.*,s.FullName,s.AdmissionNo,s.RollNo,
    sub.SubjectName,sub.MaxMarks AS SubjectMaxMarks,sub.PassMarks,
    e.ExamName,c.ClassName
  FROM TestMarks tm
  INNER JOIN Students s ON s.StudentId=tm.StudentId
  INNER JOIN Subjects sub ON sub.SubjectId=tm.SubjectId
  INNER JOIN Exams e ON e.ExamId=tm.ExamId
  INNER JOIN Classes c ON c.ClassId=s.ClassId
  WHERE s.Status='Active' AND tm.ExamId=@ExamId AND (@ClassId IS NULL OR s.ClassId=@ClassId)
  ORDER BY s.RollNo,sub.SubjectName;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetMarksBySubject]
    @SubjectId      INT,
    @ClassId        INT           = NULL,
    @SectionId      INT           = NULL,
    @BatchId        INT           = NULL,
    @ExamName       NVARCHAR(100) = NULL,
    @TestDate       DATE          = NULL,
    @AcademicYearId INT           = NULL
AS BEGIN
    SET NOCOUNT ON;
    -- Resolve to single exam (latest by Name+Class)
    DECLARE @ExamId INT = NULL;
    IF @ExamName IS NOT NULL
        SELECT TOP 1 @ExamId = e.ExamId
        FROM Exams e
        WHERE e.ExamName = @ExamName AND e.IsActive = 1
          AND (@ClassId IS NULL OR e.ClassId = @ClassId)
        ORDER BY e.ExamId DESC;
    SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade,
           e.TestDate,
           s.FullName, s.AdmissionNo, s.RollNo,
           sub.SubjectName, sub.SubjectCode,
           e.ExamName, e.ExamId,
           c.ClassName, sec.SectionName, b.BatchName
    FROM   TestMarks tm
    INNER JOIN Students s   ON s.StudentId   = tm.StudentId
    INNER JOIN Subjects sub ON sub.SubjectId = tm.SubjectId
    INNER JOIN Exams    e   ON e.ExamId      = tm.ExamId
    INNER JOIN Classes  c   ON c.ClassId     = s.ClassId
    LEFT  JOIN Sections sec ON sec.SectionId = s.SectionId
    LEFT  JOIN Batches  b   ON b.BatchId     = s.BatchId
    WHERE  s.Status = 'Active'
      AND  tm.SubjectId = @SubjectId
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@ExamId    IS NULL OR tm.ExamId   = @ExamId)
    ORDER BY s.RollNo, s.FullName;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetTestResult]
    @ExamName       NVARCHAR(100),
    @AcademicYearId INT  = NULL,
    @ClassId        INT  = NULL,
    @SectionId      INT  = NULL,
    @BatchId        INT  = NULL,
    @StudentId      INT  = NULL,
    @TestDate       DATE = NULL
AS BEGIN
    SET NOCOUNT ON;
    -- Step 1: Find the exam that actually HAS marks for this class
    DECLARE @ExamId INT;
    -- Prefer exam with the most marks entries for this class
    SELECT TOP 1 @ExamId = e.ExamId
    FROM Exams e
    INNER JOIN TestMarks tm ON tm.ExamId    = e.ExamId
    INNER JOIN Students  s  ON s.StudentId  = tm.StudentId
    WHERE e.ExamName  = @ExamName
      AND e.IsActive  = 1
      AND s.Status    = 'Active'
      AND (tm.MarksObtained IS NOT NULL OR tm.Grade = 'AB')
      AND (@ClassId IS NULL OR s.ClassId  = @ClassId)
      AND (@ClassId IS NULL OR e.ClassId  = @ClassId)
    GROUP BY e.ExamId
    ORDER BY COUNT(*) DESC, e.ExamId DESC;
    -- Fallback: any active exam with this name + class (even if no marks yet)
    IF @ExamId IS NULL
        SELECT TOP 1 @ExamId = e.ExamId
        FROM Exams e
        WHERE e.ExamName = @ExamName AND e.IsActive = 1
          AND (@ClassId IS NULL OR e.ClassId = @ClassId)
        ORDER BY e.ExamId DESC;
    IF @ExamId IS NULL RETURN;  -- no exam found
    SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade,
           s.FullName, s.AdmissionNo, s.RollNo,
           sub.SubjectName, sub.SubjectCode, sub.SubjectId,
           e.ExamName, e.ExamId, e.TestDate,
           c.ClassName, sec.SectionName, b.BatchName,
           ay.YearName
    FROM   TestMarks tm
    INNER JOIN Students      s   ON s.StudentId    = tm.StudentId
    INNER JOIN Subjects      sub ON sub.SubjectId  = tm.SubjectId
    INNER JOIN Exams         e   ON e.ExamId       = tm.ExamId
    INNER JOIN Classes       c   ON c.ClassId      = s.ClassId
    LEFT  JOIN AcademicYears ay  ON ay.YearId      = e.AcademicYearId
    LEFT  JOIN Sections      sec ON sec.SectionId  = s.SectionId
    LEFT  JOIN Batches       b   ON b.BatchId      = s.BatchId
    WHERE  tm.ExamId = @ExamId
      AND  s.Status = 'Active'
      AND  (tm.MarksObtained IS NOT NULL OR tm.Grade = 'AB')
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@StudentId IS NULL OR s.StudentId = @StudentId)
    ORDER BY s.RollNo, sub.SubjectName;
END
GO

-- Finance > class drill-down: the Finance totals already count active students only, so the list must too.
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
  WHERE c.ClassName = @ClassName AND b.BatchName = @BatchName AND s.Status = 'Active'
  ORDER BY s.FullName;
END
GO

PRINT 'Summaries now ignore deleted / inactive records.';
GO
