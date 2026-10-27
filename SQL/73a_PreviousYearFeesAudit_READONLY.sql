-- 73a: READ-ONLY audit. Changes nothing - only SELECTs. Run it on the live database BEFORE 73_PreviousYearFeesStayDue.sql.
-- It shows the fee rows of EARLIER academic years that the old "student moved up" rule switched off, i.e. the old dues that
-- disappeared from a student's balance. Review it, then decide whether to restore (73 with @Restore = 1).

-- 1) Summary: how many students / rows / rupees are affected
SELECT COUNT(DISTINCT s.StudentId) AS StudentsAffected,
       COUNT(*)                    AS FeeRowsSwitchedOff,
       SUM(sf.Amount)              AS TotalAmountSwitchedOff
FROM StudentFees sf
JOIN FeeStructure fs ON fs.StructureId = sf.StructureId
JOIN Students s      ON s.StudentId    = sf.StudentId
WHERE sf.IsActive = 0 AND sf.DeactivatedReason = 'Student moved to another class/section'
  AND s.Status = 'Active' AND fs.AcademicYearId <> s.AcademicYearId;

-- 2) Per student: what is switched off, what they have paid in total, and what their balance would be if restored.
--    "BalanceNow" is what the app / web show today; "BalanceIfRestored" adds the switched-off rows back.
;WITH switched AS (
    SELECT sf.StudentId, SUM(sf.Amount) AS OffAmount, COUNT(*) AS OffRows,
           STRING_AGG(CONCAT(y.YearName, ' ', c.ClassName, ' = ', CAST(sf.Amount AS VARCHAR(20))), ' | ') AS Detail
    FROM StudentFees sf
    JOIN FeeStructure fs ON fs.StructureId = sf.StructureId
    JOIN Students s      ON s.StudentId    = sf.StudentId
    LEFT JOIN AcademicYears y ON y.YearId  = fs.AcademicYearId
    LEFT JOIN Classes c       ON c.ClassId = fs.ClassId
    WHERE sf.IsActive = 0 AND sf.DeactivatedReason = 'Student moved to another class/section'
      AND s.Status = 'Active' AND fs.AcademicYearId <> s.AcademicYearId
    GROUP BY sf.StudentId
)
SELECT s.StudentId, s.AdmissionNo, s.FullName, c.ClassName AS NowInClass,
       o.OffRows, o.OffAmount, o.Detail AS SwitchedOffRows,
       ISNULL(f.ActiveFees, 0)   AS ActiveFeesNow,
       ISNULL(p.Paid, 0)         AS PaidTotal,
       ISNULL(p.Disc, 0) + ISNULL(s.AgreedDiscount, 0) AS DiscountTotal,
       ISNULL(f.ActiveFees, 0) - ISNULL(p.Paid, 0) - ISNULL(p.Disc, 0) - ISNULL(s.AgreedDiscount, 0)               AS BalanceNow,
       ISNULL(f.ActiveFees, 0) + o.OffAmount - ISNULL(p.Paid, 0) - ISNULL(p.Disc, 0) - ISNULL(s.AgreedDiscount, 0) AS BalanceIfRestored
FROM switched o
JOIN Students s ON s.StudentId = o.StudentId
LEFT JOIN Classes c ON c.ClassId = s.ClassId
OUTER APPLY (SELECT SUM(Amount) AS ActiveFees FROM StudentFees WHERE StudentId = s.StudentId AND IsActive = 1) f
OUTER APPLY (SELECT SUM(Amount) AS Paid, SUM(Discount) AS Disc FROM FeePayments WHERE StudentId = s.StudentId AND IsDeleted = 0) p
ORDER BY o.OffAmount DESC, s.FullName;

-- 3) Rows switched off for the same reason but belonging to the student's CURRENT year (these are NOT touched by 73's restore):
SELECT COUNT(*) AS SameYearRowsSwitchedOff_NotRestored
FROM StudentFees sf
JOIN FeeStructure fs ON fs.StructureId = sf.StructureId
JOIN Students s      ON s.StudentId    = sf.StudentId
WHERE sf.IsActive = 0 AND sf.DeactivatedReason = 'Student moved to another class/section'
  AND s.Status = 'Active' AND fs.AcademicYearId = s.AcademicYearId;
