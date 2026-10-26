-- Academic Years page: the "students" badge counted every student of the year (left / inactive included),
-- so it showed 502 while the dashboards (active only) showed 438. Count active students only, like everywhere else.
CREATE OR ALTER PROCEDURE sp_GetAcademicYears AS BEGIN
    SELECT *, (SELECT COUNT(*) FROM Students WHERE AcademicYearId=AcademicYears.YearId AND Status='Active') AS StudentCount
    FROM AcademicYears WHERE IsActive=1 ORDER BY YearId DESC;
END
GO
