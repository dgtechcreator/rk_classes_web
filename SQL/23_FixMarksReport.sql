-- ============================================================
-- 23_FixMarksReport.sql  — Run this in SSMS
-- Fixes:
-- 1. sp_GetTestResult: finds the Exam by looking at which exam
--    actually has marks for students of the given class, not just
--    by Exams.ClassId — handles case where exam was saved with
--    wrong/null ClassId.
-- 2. sp_GetExamList: includes exams that have marks for the
--    class's students, even if Exams.ClassId is 0 or different.
-- 3. sp_SaveMark: also checks student's class when looking up
--    the exam, as a fallback.
-- ============================================================
USE SchoolManagementDB;
GO

-- ── sp_GetTestResult (robust exam lookup) ────────────────────
IF OBJECT_ID('sp_GetTestResult','P') IS NOT NULL DROP PROC sp_GetTestResult;
GO
CREATE PROCEDURE sp_GetTestResult
    @ExamName       NVARCHAR(100),
    @AcademicYearId INT  = NULL,
    @ClassId        INT  = NULL,
    @SectionId      INT  = NULL,
    @BatchId        INT  = NULL,
    @StudentId      INT  = NULL,
    @TestDate       DATE = NULL
AS BEGIN
    SET NOCOUNT ON;

    -- Step 1: Try to find ExamId by ExamName + Exams.ClassId (exact match)
    DECLARE @ExamId INT;
    SELECT TOP 1 @ExamId = e.ExamId
    FROM Exams e
    WHERE e.ExamName = @ExamName AND e.IsActive = 1
      AND (@ClassId IS NULL OR e.ClassId = @ClassId)
    ORDER BY e.ExamId DESC;

    -- Step 2: Fallback — find exam that actually has marks for
    --         students of the given class (handles ClassId=0 saves)
    IF @ExamId IS NULL AND @ClassId IS NOT NULL
    BEGIN
        SELECT TOP 1 @ExamId = e.ExamId
        FROM Exams e
        INNER JOIN TestMarks tm ON tm.ExamId    = e.ExamId
        INNER JOIN Students  s  ON s.StudentId  = tm.StudentId
        WHERE e.ExamName = @ExamName
          AND e.IsActive = 1
          AND s.ClassId  = @ClassId
        ORDER BY e.ExamId DESC;
    END

    -- Step 3: Last resort — any exam with this name
    IF @ExamId IS NULL
    BEGIN
        SELECT TOP 1 @ExamId = e.ExamId
        FROM Exams e
        WHERE e.ExamName = @ExamName AND e.IsActive = 1
        ORDER BY e.ExamId DESC;
    END

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
    WHERE  tm.ExamId    = @ExamId
      AND  (tm.MarksObtained IS NOT NULL OR tm.Grade = 'AB')  -- skip rows where marks were never entered
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@StudentId IS NULL OR s.StudentId = @StudentId)
    ORDER BY s.RollNo, sub.SubjectName;
END
GO

-- ── sp_GetMarksBySubject (also add ClassId-based fallback) ───
IF OBJECT_ID('sp_GetMarksBySubject','P') IS NOT NULL DROP PROC sp_GetMarksBySubject;
GO
CREATE PROCEDURE sp_GetMarksBySubject
    @SubjectId      INT,
    @ClassId        INT           = NULL,
    @SectionId      INT           = NULL,
    @BatchId        INT           = NULL,
    @ExamName       NVARCHAR(100) = NULL,
    @TestDate       DATE          = NULL,
    @AcademicYearId INT           = NULL
AS BEGIN
    SET NOCOUNT ON;
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
    WHERE  tm.SubjectId = @SubjectId
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@ExamName  IS NULL OR e.ExamName  = @ExamName)
    ORDER BY s.RollNo, s.FullName;
END
GO

-- ── sp_GetExamList (include exams by student class too) ──────
IF OBJECT_ID('sp_GetExamList','P') IS NOT NULL DROP PROC sp_GetExamList;
GO
CREATE PROCEDURE sp_GetExamList
    @AcademicYearId INT = NULL,
    @ClassId        INT = NULL
AS BEGIN
    SET NOCOUNT ON;
    SELECT e.ExamId, e.ExamName, e.AcademicYearId, e.ClassId, e.TestDate,
           c.ClassName, ay.YearName,
           COUNT(tm.MarkId) AS EntryCount
    FROM   Exams e
    LEFT JOIN Classes       c  ON c.ClassId   = e.ClassId
    LEFT JOIN AcademicYears ay ON ay.YearId   = e.AcademicYearId
    LEFT JOIN TestMarks     tm ON tm.ExamId   = e.ExamId
    WHERE  e.IsActive = 1
      AND (
        @ClassId IS NULL
        OR e.ClassId = @ClassId
        -- also include exams whose marks are for this class's students
        OR EXISTS (
            SELECT 1 FROM TestMarks tm2
            INNER JOIN Students s2 ON s2.StudentId = tm2.StudentId
            WHERE tm2.ExamId = e.ExamId AND s2.ClassId = @ClassId
        )
      )
    GROUP BY e.ExamId, e.ExamName, e.AcademicYearId, e.ClassId, e.TestDate, c.ClassName, ay.YearName
    ORDER BY e.ExamId DESC;
END
GO

-- ── sp_SaveMark (also repair exam ClassId if it was saved as 0) ─
IF OBJECT_ID('sp_SaveMark','P') IS NOT NULL DROP PROC sp_SaveMark;
GO
CREATE PROCEDURE sp_SaveMark
    @StudentId      INT,
    @SubjectId      INT,
    @ExamName       NVARCHAR(100),
    @AcademicYearId INT,
    @ClassId        INT,
    @TestDate       DATE         = NULL,
    @MarksObtained  DECIMAL(6,2) = NULL,
    @MaxMarks       INT          = 30,
    @IsAbsent       BIT          = 0,
    @EnteredBy      INT          = NULL
AS BEGIN
    SET NOCOUNT ON;

    -- Get this student's actual class (in case ClassId arg is wrong)
    DECLARE @StudentClassId INT;
    SELECT @StudentClassId = ClassId FROM Students WHERE StudentId = @StudentId;
    -- Use student's class if @ClassId is 0 or mismatched
    IF @ClassId = 0 OR @ClassId IS NULL
        SET @ClassId = @StudentClassId;

    -- Find exam by name + class only
    DECLARE @ExamId INT;
    SELECT TOP 1 @ExamId = ExamId FROM Exams
    WHERE ExamName=@ExamName AND ClassId=@ClassId AND IsActive=1
    ORDER BY ExamId DESC;

    -- Fallback: find by name + student's actual class
    IF @ExamId IS NULL AND @StudentClassId IS NOT NULL AND @StudentClassId <> @ClassId
    BEGIN
        SELECT TOP 1 @ExamId = ExamId FROM Exams
        WHERE ExamName=@ExamName AND ClassId=@StudentClassId AND IsActive=1
        ORDER BY ExamId DESC;
        IF @ExamId IS NOT NULL SET @ClassId = @StudentClassId;
    END

    IF @ExamId IS NULL BEGIN
        INSERT INTO Exams(ExamName, AcademicYearId, ClassId, TestDate, IsActive)
        VALUES(@ExamName, @AcademicYearId, @ClassId, @TestDate, 1);
        SET @ExamId = SCOPE_IDENTITY();
    END ELSE BEGIN
        -- Update date; repair ClassId=0 if it was saved wrong
        UPDATE Exams
        SET TestDate       = ISNULL(@TestDate, TestDate),
            AcademicYearId = ISNULL(NULLIF(@AcademicYearId,0), AcademicYearId),
            ClassId        = CASE WHEN ClassId = 0 THEN @ClassId ELSE ClassId END
        WHERE ExamId = @ExamId;
    END

    DECLARE @Grade NVARCHAR(5);
    IF @IsAbsent = 1
        SET @Grade = 'AB';
    ELSE IF @MarksObtained IS NULL
        SET @Grade = NULL;   -- not entered yet, no grade
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

-- ── Quick data cleanup: fix any Exams with ClassId=0 ─────────
-- Find the class from the TestMarks → Students join and patch it
UPDATE e
SET e.ClassId = (
    SELECT TOP 1 s.ClassId
    FROM TestMarks tm
    INNER JOIN Students s ON s.StudentId = tm.StudentId
    WHERE tm.ExamId = e.ExamId
    ORDER BY s.ClassId
)
FROM Exams e
WHERE e.ClassId = 0
  AND EXISTS (SELECT 1 FROM TestMarks WHERE ExamId = e.ExamId);
GO

PRINT 'Done. Report now finds marks even if exam was saved with wrong ClassId.';
GO
