-- ============================================================================
-- 80_RollNumbers.sql
-- Roll No = a simple per-batch number (1, 2, 3 ...) inside one Class + Medium
-- (Students.ClassId + SectionId). It is NOT the admission number (RKC-2026-0333 stays as it is).
-- Only Students.RollNo is changed. Admission no, fees, marks and attendance are not touched.
--
-- STEP 0 saves the current RollNo of every student, so the change can be reverted (STEP 3).
-- STEP 1 shows the current state (read-only).
-- STEP 2 numbers every ACTIVE student 1..N per Class + Medium, alphabetical by FullName.
-- STEP 3 (only if needed) puts the old RollNo back from the backup table.
-- Run STEP 0 and STEP 1 first, check the numbers, then STEP 2.
-- ============================================================================

-- STEP 0 — backup of the current RollNo (run once, before STEP 2)
IF OBJECT_ID('dbo.Students_RollBackup_2026_11_15') IS NULL
    SELECT StudentId, RollNo AS OldRollNo, GETDATE() AS SavedAt
    INTO   dbo.Students_RollBackup_2026_11_15
    FROM   Students;

-- STEP 1 — current state (read-only)
SELECT Status,
       COUNT(*) AS Total,
       SUM(CASE WHEN RollNo IS NULL OR LTRIM(RTRIM(RollNo)) = '' THEN 1 ELSE 0 END) AS BlankRoll,
       SUM(CASE WHEN RollNo IS NOT NULL AND LTRIM(RTRIM(RollNo)) <> '' THEN 1 ELSE 0 END) AS HasRoll
FROM   Students
GROUP BY Status;

-- STEP 2 — number all ACTIVE students 1..N in each Class + Medium
BEGIN TRANSACTION;

;WITH Numbered AS (
    SELECT StudentId,
           ROW_NUMBER() OVER (PARTITION BY ClassId, SectionId ORDER BY FullName, StudentId) AS Rn
    FROM   Students
    WHERE  Status = 'Active' AND ClassId IS NOT NULL AND SectionId IS NOT NULL
)
UPDATE s
SET    s.RollNo = CAST(n.Rn AS NVARCHAR(20))
FROM   Students s
JOIN   Numbered n ON n.StudentId = s.StudentId;

-- Check: no duplicates should be returned
SELECT ClassId, SectionId, RollNo, COUNT(*) AS Cnt
FROM   Students
WHERE  Status = 'Active' AND RollNo IS NOT NULL
GROUP BY ClassId, SectionId, RollNo
HAVING COUNT(*) > 1;

-- If the check above is empty and the numbers look right:
-- COMMIT;
-- If not:
-- ROLLBACK;

-- STEP 3 — revert every student's roll to the value saved in STEP 0 (only if needed, after ROLLBACK or a later mistake)
-- UPDATE s SET s.RollNo = b.OldRollNo
-- FROM Students s JOIN dbo.Students_RollBackup_2026_11_15 b ON b.StudentId = s.StudentId;
