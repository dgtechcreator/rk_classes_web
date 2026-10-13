-- READ-ONLY fee audit. Makes no changes. Run in SSMS against SchoolManagementDB.
-- Mirrors what the Fees > Pay page does: fee = SUM(Amount) of the rows sp_GetFeeStructureByStudent would return
-- (level 1: year+class+section, 2: year+class, 3: class+section by name any year, 4: class name any year),
-- then Existing Balance = fee + additional charges - discount - paid.

SET NOCOUNT ON;

-- ============ PART A: Ankita Bhagwan Sahu (StudentId 327) ============
PRINT '--- A1 student';
SELECT s.StudentId, s.FullName, s.AcademicYearId, s.ClassId, c.ClassName, s.SectionId, sec.SectionName, s.BatchId, b.BatchName
FROM Students s
LEFT JOIN Classes c ON c.ClassId=s.ClassId LEFT JOIN Sections sec ON sec.SectionId=s.SectionId LEFT JOIN Batches b ON b.BatchId=s.BatchId
WHERE s.StudentId = 327;

PRINT '--- A2 fee structure rows for her class (ALL years/sections, active) - which ones add up to the inflated total?';
SELECT fs.StructureId, fs.AcademicYearId, ay.YearName, fs.ClassId, c.ClassName, fs.SectionId, sec.SectionName,
       ft.TypeName AS FeeType, fs.Amount, fs.IsMonthly, fs.IsActive
FROM FeeStructure fs
LEFT JOIN AcademicYears ay ON ay.YearId=fs.AcademicYearId
LEFT JOIN Classes c ON c.ClassId=fs.ClassId
LEFT JOIN Sections sec ON sec.SectionId=fs.SectionId
LEFT JOIN FeeTypes ft ON ft.FeeTypeId=fs.FeeTypeId
WHERE fs.ClassId = (SELECT ClassId FROM Students WHERE StudentId=327)
ORDER BY fs.IsActive DESC, fs.AcademicYearId, fs.SectionId, ft.TypeName;

PRINT '--- A3 her payments (incl. deleted)';
SELECT PaymentId, PaymentDate, Amount, Discount, NetAmount, IsDeleted, Remarks FROM FeePayments WHERE StudentId=327 ORDER BY PaymentDate;

PRINT '--- A4 her StudentFees rows (what Finance dashboard uses)';
SELECT * FROM StudentFees WHERE StudentId=327;

-- ============ PART B: every active student of the current year ============
PRINT '--- B all students: web-Pay fee vs Finance fee';
;WITH st AS (
    SELECT s.StudentId, s.FullName, s.AcademicYearId yr, s.ClassId, s.SectionId, s.BatchId,
           c.ClassName, sec.SectionName, b.BatchName
    FROM Students s
    LEFT JOIN Classes c ON c.ClassId=s.ClassId
    LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
    LEFT JOIN Batches b ON b.BatchId=s.BatchId
    WHERE s.Status='Active' AND s.AcademicYearId = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent=1 ORDER BY YearId DESC)
), lv AS (
    SELECT st.*,
      (SELECT COUNT(*) FROM FeeStructure f WHERE f.IsActive=1 AND (st.yr IS NULL OR f.AcademicYearId=st.yr) AND (st.ClassId IS NULL OR f.ClassId=st.ClassId) AND (st.SectionId IS NULL OR f.SectionId=st.SectionId)) n1,
      (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f WHERE f.IsActive=1 AND (st.yr IS NULL OR f.AcademicYearId=st.yr) AND (st.ClassId IS NULL OR f.ClassId=st.ClassId) AND (st.SectionId IS NULL OR f.SectionId=st.SectionId)) a1,
      (SELECT COUNT(*) FROM FeeStructure f WHERE f.IsActive=1 AND f.AcademicYearId=st.yr AND f.ClassId=st.ClassId) n2,
      (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f WHERE f.IsActive=1 AND f.AcademicYearId=st.yr AND f.ClassId=st.ClassId) a2,
      (SELECT COUNT(*) FROM FeeStructure f JOIN Classes c ON c.ClassId=f.ClassId JOIN Sections s2 ON s2.SectionId=f.SectionId WHERE f.IsActive=1 AND c.ClassName=st.ClassName AND (st.SectionName IS NULL OR s2.SectionName=st.SectionName)) n3,
      (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f JOIN Classes c ON c.ClassId=f.ClassId JOIN Sections s2 ON s2.SectionId=f.SectionId WHERE f.IsActive=1 AND c.ClassName=st.ClassName AND (st.SectionName IS NULL OR s2.SectionName=st.SectionName)) a3,
      (SELECT COUNT(*) FROM FeeStructure f JOIN Classes c ON c.ClassId=f.ClassId WHERE f.IsActive=1 AND c.ClassName=st.ClassName) n4,
      (SELECT ISNULL(SUM(f.Amount),0) FROM FeeStructure f JOIN Classes c ON c.ClassId=f.ClassId WHERE f.IsActive=1 AND c.ClassName=st.ClassName) a4
    FROM st
), r AS (
    SELECT lv.*,
      CASE WHEN n1>0 THEN 1 WHEN n2>0 THEN 2 WHEN n3>0 THEN 3 WHEN n4>0 THEN 4 ELSE 0 END AS LevelUsed,
      CASE WHEN n1>0 THEN n1 WHEN n2>0 THEN n2 WHEN n3>0 THEN n3 WHEN n4>0 THEN n4 ELSE 0 END AS HeadsUsed,
      CASE WHEN n1>0 THEN a1 WHEN n2>0 THEN a2 WHEN n3>0 THEN a3 WHEN n4>0 THEN a4 ELSE 0 END AS PayPageFee,
      ISNULL((SELECT SUM(sf.Amount) FROM StudentFees sf WHERE sf.StudentId=lv.StudentId),0) AS FinanceFee,
      ISNULL((SELECT SUM(fp.NetAmount) FROM FeePayments fp WHERE fp.StudentId=lv.StudentId AND fp.IsDeleted=0),0) AS Paid,
      ISNULL((SELECT SUM(fp.Discount)  FROM FeePayments fp WHERE fp.StudentId=lv.StudentId AND fp.IsDeleted=0),0) AS Disc
    FROM lv
)
SELECT StudentId, FullName, ClassName, SectionName, BatchName, LevelUsed, HeadsUsed,
       PayPageFee, FinanceFee, PayPageFee-FinanceFee AS FeeDiff, Paid, Disc,
       PayPageFee-Disc-Paid AS PayPageBalance, FinanceFee-Disc-Paid AS FinanceBalance,
       CASE WHEN LevelUsed IN (0,3,4) THEN 'fallback/no structure for this year'
            WHEN PayPageFee<>FinanceFee THEN 'Pay-page fee <> StudentFees'
            ELSE '' END AS Reason
FROM r
WHERE PayPageFee<>FinanceFee OR LevelUsed IN (0,3,4)
ORDER BY ABS(PayPageFee-FinanceFee) DESC, ClassName, FullName;

PRINT '--- C duplicate / overlapping fee heads for one class+section+year (would double count)';
SELECT f.AcademicYearId, f.ClassId, c.ClassName, f.SectionId, sec.SectionName, f.FeeTypeId, ft.TypeName, COUNT(*) AS Rows, SUM(f.Amount) AS Total
FROM FeeStructure f
LEFT JOIN Classes c ON c.ClassId=f.ClassId LEFT JOIN Sections sec ON sec.SectionId=f.SectionId LEFT JOIN FeeTypes ft ON ft.FeeTypeId=f.FeeTypeId
WHERE f.IsActive=1
GROUP BY f.AcademicYearId, f.ClassId, c.ClassName, f.SectionId, sec.SectionName, f.FeeTypeId, ft.TypeName
HAVING COUNT(*)>1;
