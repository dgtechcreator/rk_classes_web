-- READ-ONLY diagnosis: why Ashish Yadav (RKC-2026-0253, 5th English) shows fee 13000 (= 6000 Hindi + 7000 English).
-- Run in SSMS against SchoolManagementDB and paste all result grids back.
SET NOCOUNT ON;

PRINT '--- 1 student';
SELECT s.StudentId, s.FullName, s.AcademicYearId, s.ClassId, s.SectionId, c.ClassName, sec.SectionName
FROM Students s LEFT JOIN Classes c ON c.ClassId=s.ClassId LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
WHERE s.AdmissionNo='RKC-2026-0253';

PRINT '--- 2 all 5th fee structure rows (active first)';
SELECT fs.StructureId, fs.AcademicYearId, fs.ClassId, c.ClassName, fs.SectionId, sec.SectionName, ft.TypeName, fs.Amount, fs.IsActive
FROM FeeStructure fs LEFT JOIN Classes c ON c.ClassId=fs.ClassId LEFT JOIN Sections sec ON sec.SectionId=fs.SectionId LEFT JOIN FeeTypes ft ON ft.FeeTypeId=fs.FeeTypeId
WHERE c.ClassName='5th' ORDER BY fs.IsActive DESC, fs.SectionId;

PRINT '--- 3 sections';
SELECT SectionId, SectionName FROM Sections ORDER BY SectionName, SectionId;

PRINT '--- 4 his StudentFees rows';
SELECT sf.StudentFeeId, sf.StructureId, sf.Amount, sf.Status
FROM StudentFees sf JOIN Students s ON s.StudentId=sf.StudentId WHERE s.AdmissionNo='RKC-2026-0253';

PRINT '--- 5 active students whose StudentFees total is more than the fee structure of their own section';
SELECT s.StudentId, s.FullName, c.ClassName, sec.SectionName,
       (SELECT ISNULL(SUM(sf.Amount),0) FROM StudentFees sf WHERE sf.StudentId=s.StudentId) AS StudentFeesTotal,
       (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f WHERE f.IsActive=1 AND f.AcademicYearId=s.AcademicYearId AND f.ClassId=s.ClassId AND f.SectionId=s.SectionId) AS OwnSectionStructure
FROM Students s LEFT JOIN Classes c ON c.ClassId=s.ClassId LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
WHERE s.Status='Active' AND s.AcademicYearId=(SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent=1 ORDER BY YearId DESC)
  AND (SELECT ISNULL(SUM(sf.Amount),0) FROM StudentFees sf WHERE sf.StudentId=s.StudentId)
    <> (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f WHERE f.IsActive=1 AND f.AcademicYearId=s.AcademicYearId AND f.ClassId=s.ClassId AND f.SectionId=s.SectionId)
ORDER BY c.ClassName, sec.SectionName, s.FullName;
