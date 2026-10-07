-- ============================================================
-- 48_AttendancePhonesAndReportMarker.sql
-- 1) sp_GetAttendance now also returns Phone / FatherPhone / MotherPhone (live copy lacked them).
-- 2) sp_GetAttendanceReport now returns CreatedByName = the user who marked the student's LATEST
--    attendance in the period. (SQL/35 added u.FullName to GROUP BY, which would split one student into
--    several rows when more than one user marked them — this version keeps exactly one row per student.)
-- Existing columns, filters and ordering are unchanged. Previous live definitions are saved in SQL/backup/.
-- ============================================================
USE SchoolManagementDB;
GO

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
      AND (@ClassId   IS NULL OR s.ClassId   = @ClassId)
      AND (@SectionId IS NULL OR s.SectionId = @SectionId)
      AND (@BatchId   IS NULL OR s.BatchId   = @BatchId)
    ORDER BY s.RollNo, s.FullName;
END
GO

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
  WHERE s.Status='Active' AND (@ClassId IS NULL OR s.ClassId=@ClassId)
  GROUP BY s.StudentId,s.FullName,s.AdmissionNo,c.ClassName
  ORDER BY s.FullName;
END
GO
