-- ============================================================
-- 05_MarksAndAttendance.sql
-- Marks: by Subject/Class/Section/Batch, max configurable
-- Attendance: date-wise grid report
-- ============================================================
USE SchoolManagementDB;
GO

-- Drop old marks SP and create new flexible one
IF OBJECT_ID('sp_GetStudentsForMarks','P') IS NOT NULL DROP PROC sp_GetStudentsForMarks; GO
CREATE PROCEDURE sp_GetStudentsForMarks
    @ClassId        INT = NULL,
    @SectionId      INT = NULL,
    @BatchId        INT = NULL,
    @AcademicYearId INT = NULL
AS BEGIN
    DECLARE @YearId INT = ISNULL(@AcademicYearId, (SELECT YearId FROM AcademicYears WHERE IsCurrent=1));
    SELECT s.StudentId, s.FullName, s.AdmissionNo, s.RollNo,
           c.ClassName, sec.SectionName, b.BatchName
    FROM   Students s
    LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
    LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
    LEFT JOIN Batches  b   ON b.BatchId    = s.BatchId
    WHERE  s.Status = 'Active'
      AND  s.AcademicYearId = @YearId
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
    ORDER BY s.RollNo, s.FullName;
END
GO

-- Get existing marks for a subject+class+section+batch (for pre-filling the grid)
IF OBJECT_ID('sp_GetMarksBySubject','P') IS NOT NULL DROP PROC sp_GetMarksBySubject; GO
CREATE PROCEDURE sp_GetMarksBySubject
    @SubjectId INT,
    @ClassId   INT = NULL,
    @SectionId INT = NULL,
    @BatchId   INT = NULL,
    @ExamName  NVARCHAR(100) = NULL
AS BEGIN
    SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade, tm.Remarks,
           s.FullName, s.AdmissionNo, s.RollNo,
           sub.SubjectName, sub.SubjectCode,
           e.ExamName, e.ExamId,
           c.ClassName, sec.SectionName, b.BatchName
    FROM   TestMarks tm
    INNER JOIN Students s   ON s.StudentId   = tm.StudentId
    INNER JOIN Subjects sub ON sub.SubjectId  = tm.SubjectId
    INNER JOIN Exams    e   ON e.ExamId       = tm.ExamId
    INNER JOIN Classes  c   ON c.ClassId      = s.ClassId
    LEFT  JOIN Sections sec ON sec.SectionId  = s.SectionId
    LEFT  JOIN Batches  b   ON b.BatchId      = s.BatchId
    WHERE  tm.SubjectId = @SubjectId
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@ExamName  IS NULL OR e.ExamName  = @ExamName)
    ORDER BY s.RollNo, s.FullName;
END
GO

-- Save mark with configurable MaxMarks
IF OBJECT_ID('sp_SaveMark','P') IS NOT NULL DROP PROC sp_SaveMark; GO
CREATE PROCEDURE sp_SaveMark
    @StudentId    INT,
    @SubjectId    INT,
    @ExamName     NVARCHAR(100),   -- create exam on fly if not exists
    @AcademicYearId INT,
    @ClassId      INT,
    @MarksObtained DECIMAL(6,2),
    @MaxMarks     INT = 30,
    @EnteredBy    INT = NULL
AS BEGIN
    SET NOCOUNT ON;
    -- Get or create exam
    DECLARE @ExamId INT;
    SELECT @ExamId = ExamId FROM Exams
    WHERE ExamName=@ExamName AND AcademicYearId=@AcademicYearId AND ClassId=@ClassId AND IsActive=1;
    IF @ExamId IS NULL BEGIN
        INSERT INTO Exams(ExamName, AcademicYearId, ClassId, IsActive)
        VALUES(@ExamName, @AcademicYearId, @ClassId, 1);
        SET @ExamId = SCOPE_IDENTITY();
    END
    -- Grade calculation
    DECLARE @Pct DECIMAL(5,1) = @MarksObtained * 100.0 / NULLIF(@MaxMarks,0);
    DECLARE @Grade NVARCHAR(5) = CASE
        WHEN @Pct >= 90 THEN 'A+' WHEN @Pct >= 80 THEN 'A'
        WHEN @Pct >= 70 THEN 'B+' WHEN @Pct >= 60 THEN 'B'
        WHEN @Pct >= 50 THEN 'C'  WHEN @Pct >= 35 THEN 'D' ELSE 'F' END;
    IF EXISTS(SELECT 1 FROM TestMarks WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId)
        UPDATE TestMarks SET MarksObtained=@MarksObtained, MaxMarks=@MaxMarks,
               Grade=@Grade, EnteredBy=@EnteredBy, EnteredAt=GETDATE()
        WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId;
    ELSE
        INSERT INTO TestMarks(StudentId,ExamId,SubjectId,MarksObtained,MaxMarks,Grade,EnteredBy)
        VALUES(@StudentId,@ExamId,@SubjectId,@MarksObtained,@MaxMarks,@Grade,@EnteredBy);
END
GO

-- Get test result for print (student-wise or class-wise)
IF OBJECT_ID('sp_GetTestResult','P') IS NOT NULL DROP PROC sp_GetTestResult; GO
CREATE PROCEDURE sp_GetTestResult
    @ExamName      NVARCHAR(100),
    @AcademicYearId INT,
    @ClassId       INT = NULL,
    @SectionId     INT = NULL,
    @BatchId       INT = NULL,
    @StudentId     INT = NULL
AS BEGIN
    SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade,
           s.FullName, s.AdmissionNo, s.RollNo,
           sub.SubjectName, sub.SubjectCode,
           e.ExamName, e.ExamId,
           c.ClassName, sec.SectionName, b.BatchName,
           ay.YearName
    FROM   TestMarks tm
    INNER JOIN Students      s   ON s.StudentId    = tm.StudentId
    INNER JOIN Subjects      sub ON sub.SubjectId  = tm.SubjectId
    INNER JOIN Exams         e   ON e.ExamId       = tm.ExamId
    INNER JOIN Classes       c   ON c.ClassId      = s.ClassId
    INNER JOIN AcademicYears ay  ON ay.YearId      = e.AcademicYearId
    LEFT  JOIN Sections      sec ON sec.SectionId  = s.SectionId
    LEFT  JOIN Batches       b   ON b.BatchId      = s.BatchId
    WHERE  e.ExamName       = @ExamName
      AND  e.AcademicYearId = @AcademicYearId
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@StudentId IS NULL OR s.StudentId = @StudentId)
    ORDER BY s.RollNo, sub.SubjectName;
END
GO

-- ── Date-wise Attendance Grid ────────────────────────────────
IF OBJECT_ID('sp_GetAttendanceDateGrid','P') IS NOT NULL DROP PROC sp_GetAttendanceDateGrid; GO
CREATE PROCEDURE sp_GetAttendanceDateGrid
    @ClassId   INT = NULL,
    @SectionId INT = NULL,
    @BatchId   INT = NULL,
    @FromDate  DATE,
    @ToDate    DATE
AS BEGIN
    SET NOCOUNT ON;
    -- Students matching filter
    SELECT s.StudentId, s.FullName, s.AdmissionNo, s.RollNo,
           c.ClassName, sec.SectionName, b.BatchName
    FROM Students s
    LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
    LEFT JOIN Sections sec ON sec.SectionId= s.SectionId
    LEFT JOIN Batches  b   ON b.BatchId    = s.BatchId
    WHERE s.Status = 'Active'
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
    ORDER BY s.RollNo, s.FullName;

    -- All attendance in date range
    SELECT a.StudentId, a.AttendanceDate, a.Status
    FROM Attendance a
    INNER JOIN Students s ON s.StudentId = a.StudentId
    WHERE a.AttendanceDate BETWEEN @FromDate AND @ToDate
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId);
END
GO

PRINT '✅ Marks and Attendance SPs updated.';
GO

-- ── Update sp_SaveMark to support Absent flag ────────────────
IF OBJECT_ID('sp_SaveMark','P') IS NOT NULL DROP PROC sp_SaveMark; GO
CREATE PROCEDURE sp_SaveMark
    @StudentId     INT,
    @SubjectId     INT,
    @ExamName      NVARCHAR(100),
    @AcademicYearId INT,
    @ClassId       INT,
    @MarksObtained DECIMAL(6,2) = NULL,
    @MaxMarks      INT = 30,
    @IsAbsent      BIT = 0,
    @EnteredBy     INT = NULL
AS BEGIN
    SET NOCOUNT ON;
    -- Get or create exam
    DECLARE @ExamId INT;
    SELECT @ExamId = ExamId FROM Exams
    WHERE ExamName=@ExamName AND AcademicYearId=@AcademicYearId AND ClassId=@ClassId AND IsActive=1;
    IF @ExamId IS NULL BEGIN
        INSERT INTO Exams(ExamName, AcademicYearId, ClassId, IsActive)
        VALUES(@ExamName, @AcademicYearId, @ClassId, 1);
        SET @ExamId = SCOPE_IDENTITY();
    END

    DECLARE @Grade NVARCHAR(5);
    IF @IsAbsent = 1
        SET @Grade = 'AB'
    ELSE BEGIN
        DECLARE @Pct DECIMAL(5,1) = @MarksObtained * 100.0 / NULLIF(@MaxMarks,0);
        SET @Grade = CASE
            WHEN @Pct >= 90 THEN 'A+' WHEN @Pct >= 80 THEN 'A'
            WHEN @Pct >= 70 THEN 'B+' WHEN @Pct >= 60 THEN 'B'
            WHEN @Pct >= 50 THEN 'C'  WHEN @Pct >= 35 THEN 'D' ELSE 'F' END;
    END

    IF EXISTS(SELECT 1 FROM TestMarks WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId)
        UPDATE TestMarks
        SET MarksObtained = CASE WHEN @IsAbsent=1 THEN NULL ELSE @MarksObtained END,
            MaxMarks=@MaxMarks, Grade=@Grade, EnteredBy=@EnteredBy, EnteredAt=GETDATE()
        WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId;
    ELSE
        INSERT INTO TestMarks(StudentId,ExamId,SubjectId,MarksObtained,MaxMarks,Grade,EnteredBy)
        VALUES(@StudentId,@ExamId,@SubjectId,
               CASE WHEN @IsAbsent=1 THEN NULL ELSE @MarksObtained END,
               @MaxMarks,@Grade,@EnteredBy);
END
GO
PRINT '✅ sp_SaveMark updated with IsAbsent support.';
GO
