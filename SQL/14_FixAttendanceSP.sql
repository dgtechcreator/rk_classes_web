-- ============================================================
-- 14_FixAttendanceSP.sql  — Run this in SSMS
-- Fixes attendance so students always show by Class/Section/Batch
-- ============================================================
USE SchoolManagementDB;
GO

-- Step 1: Add extra columns if not already present
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Attendance') AND name='Subject')
    ALTER TABLE Attendance ADD Subject NVARCHAR(100) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Attendance') AND name='SirName')
    ALTER TABLE Attendance ADD SirName NVARCHAR(100) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Attendance') AND name='StartTime')
    ALTER TABLE Attendance ADD StartTime TIME NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Attendance') AND name='EndTime')
    ALTER TABLE Attendance ADD EndTime TIME NULL;
GO

-- Step 2: Fix sp_GetAttendance — NO AcademicYear filter, just Class/Section/Batch
IF OBJECT_ID('sp_GetAttendance','P') IS NOT NULL DROP PROC sp_GetAttendance;
GO
CREATE PROCEDURE sp_GetAttendance
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
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
    ORDER BY s.RollNo, s.FullName;
END
GO

-- Step 3: Fix sp_SaveAttendance — include new fields
IF OBJECT_ID('sp_SaveAttendance','P') IS NOT NULL DROP PROC sp_SaveAttendance;
GO
CREATE PROCEDURE sp_SaveAttendance
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
    @EndTime        TIME         = NULL
AS BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Attendance WHERE StudentId=@StudentId AND AttendanceDate=@AttendanceDate)
        UPDATE Attendance
        SET Status    = @Status,
            Remarks   = @Remarks,
            MarkedBy  = @MarkedBy,
            MarkedAt  = GETDATE(),
            Subject   = @Subject,
            SirName   = @SirName,
            StartTime = @StartTime,
            EndTime   = @EndTime
        WHERE StudentId=@StudentId AND AttendanceDate=@AttendanceDate;
    ELSE
        INSERT INTO Attendance
            (StudentId, AttendanceDate, Status, ClassId, SectionId, BatchId,
             Remarks, MarkedBy, Subject, SirName, StartTime, EndTime)
        VALUES
            (@StudentId, @AttendanceDate, @Status, @ClassId, @SectionId, @BatchId,
             @Remarks, @MarkedBy, @Subject, @SirName, @StartTime, @EndTime);
END
GO

-- Quick verify: should show Aarav Sharma when ClassId/SectionId/BatchId match
-- SELECT * FROM Students WHERE Status='Active' ORDER BY FullName;

PRINT '✅ Done. Attendance SPs fixed — students now load by Class/Section/Batch only.';
GO
