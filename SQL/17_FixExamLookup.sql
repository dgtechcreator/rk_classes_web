-- ============================================================
-- 17_FixExamLookup.sql  — Run this in SSMS
-- Root fix: exam lookup by (ExamName + ClassId) only, removing
-- AcademicYearId from the key so saves and reports always
-- resolve to the SAME exam record and avoid duplicate ExamIds.
-- ============================================================
USE SchoolManagementDB;
GO

-- ── sp_SaveMark ──────────────────────────────────────────────
-- Lookup exam by (ExamName, ClassId) only — no year in key
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
    -- Find exam by name + class only (ignore year — prevents duplicate ExamIds)
    DECLARE @ExamId INT;
    SELECT TOP 1 @ExamId = ExamId FROM Exams
    WHERE ExamName=@ExamName AND ClassId=@ClassId AND IsActive=1
    ORDER BY ExamId DESC;

    IF @ExamId IS NULL BEGIN
        INSERT INTO Exams(ExamName, AcademicYearId, ClassId, TestDate, IsActive)
        VALUES(@ExamName, @AcademicYearId, @ClassId, @TestDate, 1);
        SET @ExamId = SCOPE_IDENTITY();
    END ELSE BEGIN
        -- Update date if provided; also keep year in sync
        UPDATE Exams
        SET TestDate       = ISNULL(@TestDate, TestDate),
            AcademicYearId = ISNULL(NULLIF(@AcademicYearId,0), AcademicYearId)
        WHERE ExamId = @ExamId;
    END

    DECLARE @Grade NVARCHAR(5);
    IF @IsAbsent = 1
        SET @Grade = 'AB';
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

-- ── sp_GetMarksBySubject ─────────────────────────────────────
-- No AcademicYearId or TestDate filter in main lookup — always
-- returns marks for the exam by name + class
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

-- ── sp_GetTestResult ─────────────────────────────────────────
-- Resolve exam by (ExamName, ClassId) — no year in key
-- Deduplicate: use highest ExamId if duplicates exist
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
    -- Resolve to single ExamId (latest) to avoid duplicates from year mismatch
    DECLARE @ExamId INT;
    SELECT TOP 1 @ExamId = e.ExamId
    FROM Exams e
    WHERE e.ExamName = @ExamName AND e.IsActive = 1
      AND (@ClassId IS NULL OR e.ClassId = @ClassId)
    ORDER BY e.ExamId DESC;

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
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@StudentId IS NULL OR s.StudentId = @StudentId)
    ORDER BY s.RollNo, sub.SubjectName;
END
GO

-- ── sp_GetExamList ───────────────────────────────────────────
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

PRINT 'Done. Exam lookup now uses (ExamName + ClassId) only — no more duplicate ExamIds per year.';
GO
