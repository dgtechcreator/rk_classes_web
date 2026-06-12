-- ============================================================
-- 31_FixMarksSave.sql  — Run this in SSMS
-- Root fix for marks not being recorded in test reports.
--
-- Problems fixed:
-- 1. sp_SaveMark was creating DUPLICATE exam records every time
--    the date changed (lookup used Name+Year+Class+TestDate).
--    Now uses Name+Class ONLY → same exam reused every time.
-- 2. sp_GetTestResult was picking an exam with NULL marks if it
--    happened to have a higher ExamId than the real-marks exam.
--    Now merges duplicate exams and picks the one WITH marks.
-- 3. Cleanup: removes erroneous TestMark rows where marks were
--    never entered (MarksObtained IS NULL and Grade != 'AB').
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Step 1: Clean up erroneous NULL-marks records ────────────
-- These are rows where teacher hit Save without entering marks
-- (IsAbsent=0, MarksObtained=NULL, Grade='F'). Safe to delete.
DELETE FROM TestMarks
WHERE  MarksObtained IS NULL
  AND  (Grade IS NULL OR Grade NOT IN ('AB'));
GO

-- ── Step 2: Consolidate duplicate Exam records ───────────────
-- If the same exam name + class has MULTIPLE Exam rows
-- (caused by old TestDate-based lookup), move all TestMark rows
-- to the LATEST ExamId, then soft-delete the older ones.

-- Temp table: for each (ExamName, ClassId) keep only the latest ExamId
IF OBJECT_ID('tempdb..#KeepExam') IS NOT NULL DROP TABLE #KeepExam;
SELECT ExamName, ClassId,
       MAX(ExamId) AS KeepId          -- keep latest
INTO   #KeepExam
FROM   Exams
WHERE  IsActive = 1
GROUP BY ExamName, ClassId
HAVING COUNT(*) > 1;   -- only care about duplicates
GO

-- Re-point TestMark rows from older duplicate exams to the keeper
UPDATE tm
SET    tm.ExamId = ke.KeepId
FROM   TestMarks tm
INNER JOIN Exams       e  ON e.ExamId    = tm.ExamId
INNER JOIN #KeepExam   ke ON ke.ExamName = e.ExamName
                          AND ke.ClassId  = e.ClassId
WHERE  tm.ExamId <> ke.KeepId;
GO

-- Now some (KeepId, StudentId, SubjectId) may have duplicate rows
-- (one from old exam, one already there). Keep only the latest.
DELETE tm
FROM TestMarks tm
WHERE EXISTS (
    SELECT 1 FROM TestMarks tm2
    INNER JOIN #KeepExam ke ON ke.KeepId = tm2.ExamId
    WHERE tm2.ExamId     = tm.ExamId
      AND tm2.StudentId  = tm.StudentId
      AND tm2.SubjectId  = tm.SubjectId
      AND tm2.MarkId     > tm.MarkId   -- keep the higher MarkId (newer)
);
GO

-- Deactivate the now-empty older duplicate exam rows
UPDATE e
SET    e.IsActive = 0
FROM   Exams     e
INNER JOIN #KeepExam ke ON ke.ExamName = e.ExamName
                        AND ke.ClassId  = e.ClassId
WHERE  e.ExamId <> ke.KeepId;
GO

DROP TABLE #KeepExam;
GO

-- ── sp_SaveMark ───────────────────────────────────────────────
-- Lookup: ExamName + ClassId ONLY (no year, no date in key).
-- This means the same test always maps to the same Exam row,
-- regardless of which day the teacher opens the entry form.
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

    -- Use student's actual ClassId if @ClassId is 0/null
    IF @ClassId = 0 OR @ClassId IS NULL
        SELECT @ClassId = ClassId FROM Students WHERE StudentId = @StudentId;

    -- Find exam by Name + Class ONLY (prevents date-driven duplicates)
    DECLARE @ExamId INT;
    SELECT TOP 1 @ExamId = ExamId FROM Exams
    WHERE ExamName = @ExamName AND ClassId = @ClassId AND IsActive = 1
    ORDER BY ExamId DESC;

    IF @ExamId IS NULL BEGIN
        -- Create new exam
        INSERT INTO Exams(ExamName, AcademicYearId, ClassId, TestDate, IsActive)
        VALUES(@ExamName, @AcademicYearId, @ClassId, @TestDate, 1);
        SET @ExamId = SCOPE_IDENTITY();
    END ELSE BEGIN
        -- Update exam metadata (date + year) without changing the key
        UPDATE Exams
        SET TestDate       = ISNULL(@TestDate, TestDate),
            AcademicYearId = CASE WHEN @AcademicYearId > 0
                                  THEN @AcademicYearId
                                  ELSE AcademicYearId END
        WHERE ExamId = @ExamId;
    END

    -- Skip save if no actual data (not absent, but marks not entered)
    IF @IsAbsent = 0 AND @MarksObtained IS NULL
        RETURN;   -- nothing to save for this student

    -- Grade calculation
    DECLARE @Grade NVARCHAR(5);
    IF @IsAbsent = 1
        SET @Grade = 'AB';
    ELSE BEGIN
        DECLARE @Pct DECIMAL(5,1) = @MarksObtained * 100.0 / NULLIF(@MaxMarks,0);
        SET @Grade = CASE
            WHEN @Pct >= 90 THEN 'A+' WHEN @Pct >= 80 THEN 'A'
            WHEN @Pct >= 70 THEN 'B+' WHEN @Pct >= 60 THEN 'B'
            WHEN @Pct >= 50 THEN 'C'  WHEN @Pct >= 35 THEN 'D'
            ELSE 'F' END;
    END

    IF EXISTS(SELECT 1 FROM TestMarks
              WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId)
        UPDATE TestMarks
        SET MarksObtained = CASE WHEN @IsAbsent=1 THEN NULL ELSE @MarksObtained END,
            MaxMarks      = @MaxMarks,
            Grade         = @Grade,
            EnteredBy     = @EnteredBy,
            EnteredAt     = GETDATE()
        WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId;
    ELSE
        INSERT INTO TestMarks(StudentId, ExamId, SubjectId, MarksObtained, MaxMarks, Grade, EnteredBy)
        VALUES(@StudentId, @ExamId, @SubjectId,
               CASE WHEN @IsAbsent=1 THEN NULL ELSE @MarksObtained END,
               @MaxMarks, @Grade, @EnteredBy);
END
GO

-- ── sp_GetMarksBySubject ──────────────────────────────────────
-- Pre-fill entry form: returns existing marks for the exam.
-- Uses the SAME exam resolution (latest ExamId by Name+Class).
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
    WHERE  tm.SubjectId = @SubjectId
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@ExamId    IS NULL OR tm.ExamId   = @ExamId)
    ORDER BY s.RollNo, s.FullName;
END
GO

-- ── sp_GetTestResult ─────────────────────────────────────────
-- Report view: resolve to the single best exam (latest ExamId
-- that actually HAS marks entries) — no year/date ambiguity.
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

    -- Step 1: Find the exam that actually HAS marks for this class
    DECLARE @ExamId INT;

    -- Prefer exam with the most marks entries for this class
    SELECT TOP 1 @ExamId = e.ExamId
    FROM Exams e
    INNER JOIN TestMarks tm ON tm.ExamId    = e.ExamId
    INNER JOIN Students  s  ON s.StudentId  = tm.StudentId
    WHERE e.ExamName  = @ExamName
      AND e.IsActive  = 1
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
      AND  (tm.MarksObtained IS NOT NULL OR tm.Grade = 'AB')
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@StudentId IS NULL OR s.StudentId = @StudentId)
    ORDER BY s.RollNo, sub.SubjectName;
END
GO

-- ── sp_GetExamList ────────────────────────────────────────────
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
      AND (@ClassId IS NULL OR e.ClassId = @ClassId)
    GROUP BY e.ExamId, e.ExamName, e.AcademicYearId, e.ClassId, e.TestDate, c.ClassName, ay.YearName
    ORDER BY e.ExamId DESC;
END
GO

PRINT '✅ SQL/31 done. Marks SPs fixed, duplicate exams consolidated, null-marks records cleaned.';
GO
