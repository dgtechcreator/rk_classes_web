-- READ-ONLY. Lists active students (current year) whose web Pay-page fee (FeeStructure, same 4-level lookup as
-- sp_GetFeeStructureByStudent) differs from the phone/Finance fee (active StudentFees rows), or who have no fee at all.
SET NOCOUNT ON;
;WITH st AS (
    SELECT s.StudentId, s.AdmissionNo, s.FullName, s.AcademicYearId yr, s.ClassId, s.SectionId, s.BatchId,
           c.ClassName, sec.SectionName, b.BatchName
    FROM Students s
    LEFT JOIN Classes c ON c.ClassId=s.ClassId LEFT JOIN Sections sec ON sec.SectionId=s.SectionId LEFT JOIN Batches b ON b.BatchId=s.BatchId
    WHERE s.Status='Active' AND s.AcademicYearId=(SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent=1 ORDER BY YearId DESC)
), lv AS (
    SELECT st.*,
      (SELECT COUNT(*) FROM FeeStructure f WHERE f.IsActive=1 AND (st.yr IS NULL OR f.AcademicYearId=st.yr) AND (st.ClassId IS NULL OR f.ClassId=st.ClassId) AND (st.SectionId IS NULL OR f.SectionId=st.SectionId)) n1,
      (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f WHERE f.IsActive=1 AND (st.yr IS NULL OR f.AcademicYearId=st.yr) AND (st.ClassId IS NULL OR f.ClassId=st.ClassId) AND (st.SectionId IS NULL OR f.SectionId=st.SectionId)) a1,
      (SELECT COUNT(*) FROM FeeStructure f WHERE f.IsActive=1 AND f.AcademicYearId=st.yr AND f.ClassId=st.ClassId) n2,
      (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f WHERE f.IsActive=1 AND f.AcademicYearId=st.yr AND f.ClassId=st.ClassId) a2
    FROM st
), r AS (
    SELECT lv.*, CASE WHEN n1>0 THEN 1 WHEN n2>0 THEN 2 ELSE 0 END AS LevelUsed,
      CASE WHEN n1>0 THEN a1 WHEN n2>0 THEN a2 ELSE 0 END AS WebFee,
      ISNULL((SELECT SUM(sf.Amount) FROM StudentFees sf WHERE sf.StudentId=lv.StudentId AND sf.IsActive=1),0) AS PhoneFee,
      (SELECT COUNT(*) FROM StudentFees sf WHERE sf.StudentId=lv.StudentId AND sf.IsActive=1) AS PhoneRows,
      ISNULL((SELECT SUM(fp.Amount) FROM FeePayments fp WHERE fp.StudentId=lv.StudentId AND fp.IsDeleted=0),0) AS Paid
    FROM lv
)
SELECT StudentId, AdmissionNo, FullName, ClassName, SectionName, BatchName, LevelUsed, WebFee, PhoneFee, Paid,
       CASE WHEN SectionId IS NULL THEN 'Section/medium not set'
            WHEN BatchName IS NULL THEN 'Batch not set (fee ok)'
            WHEN LevelUsed=0 THEN 'No fee head for this class/year'
            WHEN LevelUsed=2 THEN 'Section has no own fee head (class-level fee used)'
            WHEN WebFee<>PhoneFee THEN 'Web fee <> phone fee'
            ELSE 'OK' END AS Issue
FROM r
ORDER BY CASE WHEN WebFee<>PhoneFee OR LevelUsed IN (0,2) OR SectionId IS NULL THEN 0 ELSE 1 END, ClassName, SectionName, FullName;
