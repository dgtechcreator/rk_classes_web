-- ============================================================
-- 08_MarksUpdate.sql
-- Add TestDate to Exams, update SPs for date-based unique tests
-- ============================================================
USE SchoolManagementDB;
GO

-- Add TestDate column to Exams if not exists
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Exams') AND name='TestDate')
    ALTER TABLE Exams ADD TestDate DATE NULL;
GO

-- Update unique key: ExamName + AcademicYearId + ClassId + TestDate
-- (allows same subject to have multiple tests on different dates)

-- ── sp_GetStudentsWithMarks ───────────────────────────────────
IF OBJECT_ID('sp_GetMarksBySubject','P') IS NOT NULL DROP PROC sp_GetMarksBySubject; GO
CREATE PROCEDURE sp_GetMarksBySubject
    @SubjectId     INT,
    @ClassId       INT = NULL,
    @SectionId     INT = NULL,
    @BatchId       INT = NULL,
    @ExamName      NVARCHAR(100) = NULL,
    @TestDate      DATE = NULL,
    @AcademicYearId INT = NULL
AS BEGIN
    SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade,
           s.FullName, s.AdmissionNo, s.RollNo,
           sub.SubjectName, sub.SubjectCode,
           e.ExamName, e.ExamId, e.TestDate,
           c.ClassName, sec.SectionName, b.BatchName
    FROM   TestMarks tm
    INNER JOIN Students s   ON s.StudentId    = tm.StudentId
    INNER JOIN Subjects sub ON sub.SubjectId  = tm.SubjectId
    INNER JOIN Exams    e   ON e.ExamId       = tm.ExamId
    INNER JOIN Classes  c   ON c.ClassId      = s.ClassId
    LEFT  JOIN Sections sec ON sec.SectionId  = s.SectionId
    LEFT  JOIN Batches  b   ON b.BatchId      = s.BatchId
    WHERE  tm.SubjectId = @SubjectId
      AND (@ClassId        IS NULL OR s.ClassId          = @ClassId)
      AND (@SectionId      IS NULL OR s.SectionId        = @SectionId)
      AND (@BatchId        IS NULL OR s.BatchId          = @BatchId)
      AND (@ExamName       IS NULL OR e.ExamName         = @ExamName)
      AND (@TestDate       IS NULL OR e.TestDate         = @TestDate)
      AND (@AcademicYearId IS NULL OR e.AcademicYearId   = @AcademicYearId)
    ORDER BY s.RollNo, s.FullName;
END
GO

-- ── sp_SaveMark (with TestDate) ───────────────────────────────
IF OBJECT_ID('sp_SaveMark','P') IS NOT NULL DROP PROC sp_SaveMark; GO
CREATE PROCEDURE sp_SaveMark
    @StudentId      INT,
    @SubjectId      INT,
    @ExamName       NVARCHAR(100),
    @AcademicYearId INT,
    @ClassId        INT,
    @TestDate       DATE = NULL,
    @MarksObtained  DECIMAL(6,2) = NULL,
    @MaxMarks       INT = 30,
    @IsAbsent       BIT = 0,
    @EnteredBy      INT = NULL
AS BEGIN
    SET NOCOUNT ON;
    -- Get or create exam (unique by Name + Year + Class + Date)
    DECLARE @ExamId INT;
    SELECT @ExamId = ExamId FROM Exams
    WHERE ExamName=@ExamName AND AcademicYearId=@AcademicYearId
      AND ClassId=@ClassId AND IsActive=1
      AND ((@TestDate IS NULL AND TestDate IS NULL) OR TestDate=@TestDate);
    IF @ExamId IS NULL BEGIN
        INSERT INTO Exams(ExamName, AcademicYearId, ClassId, TestDate, IsActive)
        VALUES(@ExamName, @AcademicYearId, @ClassId, @TestDate, 1);
        SET @ExamId = SCOPE_IDENTITY();
    END
    -- Grade
    DECLARE @Grade NVARCHAR(5);
    IF @IsAbsent = 1
        SET @Grade = 'AB'
    ELSE BEGIN
        DECLARE @Pct DECIMAL(5,1) = @MarksObtained * 100.0 / NULLIF(@MaxMarks,0);
        SET @Grade = CASE
            WHEN @Pct>=90 THEN 'A+' WHEN @Pct>=80 THEN 'A'
            WHEN @Pct>=70 THEN 'B+' WHEN @Pct>=60 THEN 'B'
            WHEN @Pct>=50 THEN 'C'  WHEN @Pct>=35 THEN 'D' ELSE 'F' END;
    END
    IF EXISTS(SELECT 1 FROM TestMarks WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId)
        UPDATE TestMarks
        SET MarksObtained=CASE WHEN @IsAbsent=1 THEN NULL ELSE @MarksObtained END,
            MaxMarks=@MaxMarks, Grade=@Grade, EnteredBy=@EnteredBy, EnteredAt=GETDATE()
        WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId;
    ELSE
        INSERT INTO TestMarks(StudentId,ExamId,SubjectId,MarksObtained,MaxMarks,Grade,EnteredBy)
        VALUES(@StudentId,@ExamId,@SubjectId,
               CASE WHEN @IsAbsent=1 THEN NULL ELSE @MarksObtained END,
               @MaxMarks,@Grade,@EnteredBy);
END
GO

-- ── sp_GetTestResult (with TestDate filter) ───────────────────
IF OBJECT_ID('sp_GetTestResult','P') IS NOT NULL DROP PROC sp_GetTestResult; GO
CREATE PROCEDURE sp_GetTestResult
    @ExamName       NVARCHAR(100),
    @AcademicYearId INT,
    @ClassId        INT = NULL,
    @SectionId      INT = NULL,
    @BatchId        INT = NULL,
    @StudentId      INT = NULL,
    @TestDate       DATE = NULL
AS BEGIN
    SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade,
           s.FullName, s.AdmissionNo, s.RollNo,
           sub.SubjectName, sub.SubjectCode,
           e.ExamName, e.ExamId, e.TestDate,
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
      AND (@TestDate  IS NULL OR e.TestDate  = @TestDate)
    ORDER BY s.RollNo, sub.SubjectName;
END
GO

-- ── sp_GetExamList (for dropdown of past tests) ───────────────
IF OBJECT_ID('sp_GetExamList','P') IS NOT NULL DROP PROC sp_GetExamList; GO
CREATE PROCEDURE sp_GetExamList
    @AcademicYearId INT = NULL,
    @ClassId        INT = NULL
AS BEGIN
    SELECT DISTINCT e.ExamId, e.ExamName, e.TestDate, e.AcademicYearId,
           e.ClassId, c.ClassName, ay.YearName,
           COUNT(tm.MarkId) AS EntryCount
    FROM Exams e
    LEFT JOIN Classes       c  ON c.ClassId  = e.ClassId
    LEFT JOIN AcademicYears ay ON ay.YearId  = e.AcademicYearId
    LEFT JOIN TestMarks     tm ON tm.ExamId  = e.ExamId
    WHERE e.IsActive = 1
      AND (@AcademicYearId IS NULL OR e.AcademicYearId=@AcademicYearId)
      AND (@ClassId        IS NULL OR e.ClassId=@ClassId)
    GROUP BY e.ExamId, e.ExamName, e.TestDate, e.AcademicYearId, e.ClassId, c.ClassName, ay.YearName
    ORDER BY e.TestDate DESC, e.ExamName;
END
GO

PRINT '✅ Marks SPs updated with TestDate support.';
GO
