-- ============================================================
-- 35_AddCreatedByNameToFacultyAndAttendance.sql
-- Add CreatedByName to Faculty and MarkedByName to Attendance reports
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Update sp_GetFaculty to include CreatedByName ─────────────
IF OBJECT_ID('sp_GetFaculty','P') IS NOT NULL DROP PROC sp_GetFaculty; GO
CREATE PROCEDURE sp_GetFaculty
    @Search   NVARCHAR(200) = NULL,
    @Status   NVARCHAR(20)  = NULL,
    @DesignationId INT       = NULL,
    @PageNo   INT = 1,
    @PageSize INT = 15,
    @TotalCount INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    SELECT @TotalCount = COUNT(*) FROM Faculty f
    WHERE (@Search        IS NULL OR f.FullName LIKE '%'+@Search+'%' OR f.EmployeeCode LIKE '%'+@Search+'%' OR f.Phone LIKE '%'+@Search+'%')
      AND (@Status        IS NULL OR f.Status        = @Status)
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId);

    SELECT f.*, d.DesignationName, u.FullName AS CreatedByName
    FROM Faculty f
    LEFT JOIN Designations d ON d.DesignationId = f.DesignationId
    LEFT JOIN Users u ON u.UserId = f.CreatedBy
    WHERE (@Search        IS NULL OR f.FullName LIKE '%'+@Search+'%' OR f.EmployeeCode LIKE '%'+@Search+'%' OR f.Phone LIKE '%'+@Search+'%')
      AND (@Status        IS NULL OR f.Status        = @Status)
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId)
    ORDER BY f.FullName
    OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ── Update sp_GetFacultyById to include CreatedByName ────────
IF OBJECT_ID('sp_GetFacultyById','P') IS NOT NULL DROP PROC sp_GetFacultyById; GO
CREATE PROCEDURE sp_GetFacultyById @FacultyId INT AS BEGIN
    SELECT f.*, d.DesignationName, u.FullName AS CreatedByName
    FROM Faculty f
    LEFT JOIN Designations d ON d.DesignationId = f.DesignationId
    LEFT JOIN Users u ON u.UserId = f.CreatedBy
    WHERE f.FacultyId = @FacultyId;

    -- Also return assigned subjects
    SELECT fs.*, c.ClassName, sub.SubjectName, sec.SectionName
    FROM FacultySubjects fs
    LEFT JOIN Classes   c   ON c.ClassId   = fs.ClassId
    LEFT JOIN Subjects  sub ON sub.SubjectId= fs.SubjectId
    LEFT JOIN Sections  sec ON sec.SectionId= fs.SectionId
    WHERE fs.FacultyId=@FacultyId AND fs.IsActive=1;
END
GO

-- ── Update sp_GetAttendanceReport to include MarkedByName ─────
IF OBJECT_ID('sp_GetAttendanceReport','P') IS NOT NULL DROP PROC sp_GetAttendanceReport; GO
CREATE PROCEDURE sp_GetAttendanceReport @ClassId INT=NULL, @Month INT=NULL, @Year INT=NULL AS BEGIN
  SELECT s.StudentId,s.FullName,s.AdmissionNo,c.ClassName,
    COUNT(CASE WHEN a.Status='Present' THEN 1 END) AS PresentDays,
    COUNT(CASE WHEN a.Status='Absent'  THEN 1 END) AS AbsentDays,
    COUNT(CASE WHEN a.Status='Late'    THEN 1 END) AS LateDays,
    COUNT(a.AttendanceId) AS TotalDays,
    CAST(COUNT(CASE WHEN a.Status='Present' THEN 1 END)*100.0/NULLIF(COUNT(a.AttendanceId),0) AS DECIMAL(5,1)) AS AttendancePct,
    u.FullName AS CreatedByName
  FROM Students s
  LEFT JOIN Attendance a ON a.StudentId=s.StudentId
    AND (@Month IS NULL OR MONTH(a.AttendanceDate)=@Month)
    AND (@Year  IS NULL OR YEAR(a.AttendanceDate)=@Year)
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  LEFT JOIN Users u ON u.UserId=a.MarkedBy
  WHERE s.Status='Active' AND (@ClassId IS NULL OR s.ClassId=@ClassId)
  GROUP BY s.StudentId,s.FullName,s.AdmissionNo,c.ClassName,u.FullName
  ORDER BY s.FullName;
END
GO

PRINT '✅ CreatedByName added to Faculty and Attendance reports.';
GO
