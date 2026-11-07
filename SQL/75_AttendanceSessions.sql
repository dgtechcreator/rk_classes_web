-- ============================================================
-- 75_AttendanceSessions.sql
-- More than one attendance per day (e.g. a Sunday / holiday when the same class has 2 lectures):
-- every attendance row now belongs to a SessionNo (1 = the first lecture of the day, 2 = the second ...).
--  * Attendance.SessionNo INT NOT NULL DEFAULT 1 - every existing row becomes session 1, nothing else changes.
--  * UNIQUE (StudentId, AttendanceDate)  ->  UNIQUE (StudentId, AttendanceDate, SessionNo).
--  * sp_GetAttendance / sp_SaveAttendance take @SessionNo (default 1, so old callers keep working).
-- Reports / percentages count rows, so a 2-session day simply counts as 2 lectures.
-- Run this BEFORE deploying the web package. Safe to run more than once.
-- Undo: ALTER TABLE Attendance DROP CONSTRAINT UQ_Attendance_Student_Date_Session; delete rows with SessionNo > 1;
--       re-add UNIQUE(StudentId, AttendanceDate); then re-run the sp_GetAttendance body of SQL/69 and sp_SaveAttendance of SQL/14.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'SessionNo')
    ALTER TABLE Attendance ADD SessionNo INT NOT NULL CONSTRAINT DF_Attendance_SessionNo DEFAULT 1;
GO

-- Drop the old one-row-per-student-per-day unique constraint / index (its name is system generated), add the new one.
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE Attendance DROP CONSTRAINT [' + kc.name + N'];' + CHAR(10)
FROM sys.key_constraints kc
WHERE kc.parent_object_id = OBJECT_ID('Attendance') AND kc.type = 'UQ'
  AND kc.name <> 'UQ_Attendance_Student_Date_Session'
  AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id) = 2;
IF LEN(@sql) > 0 EXEC sp_executesql @sql;

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Attendance_Student_Date_Session')
    ALTER TABLE Attendance ADD CONSTRAINT UQ_Attendance_Student_Date_Session UNIQUE (StudentId, AttendanceDate, SessionNo);
GO

-- Attendance sheet of a date + session
CREATE OR ALTER PROCEDURE [dbo].[sp_GetAttendance]
    @AttendanceDate DATE,
    @ClassId        INT = NULL,
    @SectionId      INT = NULL,
    @BatchId        INT = NULL,
    @SessionNo      INT = 1
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
        s.MotherPhone,
        @SessionNo AS SessionNo
    FROM Students s
    LEFT JOIN Classes   c   ON c.ClassId     = s.ClassId
    LEFT JOIN Sections  sec ON sec.SectionId = s.SectionId
    LEFT JOIN Batches   b   ON b.BatchId     = s.BatchId
    LEFT JOIN Attendance a  ON a.StudentId   = s.StudentId
                           AND a.AttendanceDate = @AttendanceDate
                           AND a.SessionNo = @SessionNo
    WHERE s.Status = 'Active'
      AND s.AcademicYearId = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC)
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
    ORDER BY s.RollNo, s.FullName;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_SaveAttendance]
    @StudentId      INT,
    @AttendanceDate DATE,
    @Status         NVARCHAR(10),
    @ClassId        INT          = NULL,
    @SectionId      INT          = NULL,
    @BatchId        INT          = NULL,
    @Remarks        NVARCHAR(200)= NULL,
    @MarkedBy       INT          = NULL,
    @Subject        NVARCHAR(100)= NULL,
    @SirName        NVARCHAR(100)= NULL,
    @StartTime      TIME         = NULL,
    @EndTime        TIME         = NULL,
    @SessionNo      INT          = 1
AS BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Attendance WHERE StudentId=@StudentId AND AttendanceDate=@AttendanceDate AND SessionNo=@SessionNo)
        UPDATE Attendance
        SET Status    = @Status,
            Remarks   = @Remarks,
            MarkedBy  = @MarkedBy,
            MarkedAt  = GETDATE(),
            Subject   = @Subject,
            SirName   = @SirName,
            StartTime = @StartTime,
            EndTime   = @EndTime
        WHERE StudentId=@StudentId AND AttendanceDate=@AttendanceDate AND SessionNo=@SessionNo;
    ELSE
        INSERT INTO Attendance
            (StudentId, AttendanceDate, SessionNo, Status, ClassId, SectionId, BatchId,
             Remarks, MarkedBy, Subject, SirName, StartTime, EndTime)
        VALUES
            (@StudentId, @AttendanceDate, @SessionNo, @Status, @ClassId, @SectionId, @BatchId,
             @Remarks, @MarkedBy, @Subject, @SirName, @StartTime, @EndTime);
END
GO

PRINT 'Attendance sessions ready.';
