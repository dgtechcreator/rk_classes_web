-- ============================================================
-- 16_FixTestResultSP.sql  — Run this in SSMS
-- Makes AcademicYearId optional in sp_GetTestResult so
-- results appear even when year filter is null/0
-- ============================================================
USE SchoolManagementDB;
GO

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
    WHERE  e.ExamName = @ExamName
      AND (@AcademicYearId IS NULL OR e.AcademicYearId = @AcademicYearId)
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
      AND (@StudentId IS NULL OR s.StudentId = @StudentId)
      AND (@TestDate  IS NULL OR e.TestDate  = @TestDate)
    ORDER BY s.RollNo, sub.SubjectName;
END
GO

-- Also fix sp_GetMarksBySubject to make AcademicYearId + TestDate optional
IF OBJECT_ID('sp_GetMarksBySubject','P') IS NOT NULL DROP PROC sp_GetMarksBySubject;
GO
CREATE PROCEDURE sp_GetMarksBySubject
    @SubjectId      INT,
    @ClassId        INT          = NULL,
    @SectionId      INT          = NULL,
    @BatchId        INT          = NULL,
    @ExamName       NVARCHAR(100)= NULL,
    @TestDate       DATE         = NULL,
    @AcademicYearId INT          = NULL
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
      AND (@ClassId        IS NULL OR s.ClassId        = @ClassId)
      AND (@SectionId      IS NULL OR s.SectionId      = @SectionId)
      AND (@BatchId        IS NULL OR s.BatchId        = @BatchId)
      AND (@ExamName       IS NULL OR e.ExamName       = @ExamName)
      AND (@TestDate       IS NULL OR e.TestDate       = @TestDate)
      AND (@AcademicYearId IS NULL OR e.AcademicYearId = @AcademicYearId)
    ORDER BY s.RollNo, s.FullName;
END
GO

PRINT 'Done. sp_GetTestResult and sp_GetMarksBySubject now have optional AcademicYearId/TestDate filters.';
GO
