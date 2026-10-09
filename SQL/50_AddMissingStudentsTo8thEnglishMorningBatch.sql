-- ============================================================
-- 50_AddMissingStudentsTo8thEnglishMorningBatch.sql  — run in SSMS on SchoolManagementDB
--
-- Why: the attendance batch "8th English Morning" holds a fixed list of students (AttendanceBatchStudents).
-- Class 8th / English / Morning has 18 ACTIVE students but the batch only lists 16, so the batch card and the
-- attendance entry screen show 16. Two students were never added to the batch:
--     344  Aditya Jayprakash Yadav
--     496  Ankush Omprakash Yadav
-- (the inactive duplicates 497 and 345 are intentionally NOT added).
--
-- Safe to run more than once. Alternative without SQL: Masters > Attendance Batches > edit "8th English Morning"
-- and tick the two students.
-- ============================================================
USE SchoolManagementDB;
GO

DECLARE @BatchId INT = (SELECT TOP 1 BatchId FROM AttendanceBatches WHERE BatchName = '8th English Morning' AND IsActive = 1 ORDER BY BatchId);

IF @BatchId IS NULL
    PRINT 'Attendance batch "8th English Morning" not found - nothing done.';
ELSE
BEGIN
    INSERT INTO AttendanceBatchStudents (BatchId, StudentId)
    SELECT @BatchId, s.StudentId
    FROM Students s
    WHERE s.StudentId IN (344, 496)
      AND s.Status = 'Active'
      AND NOT EXISTS (SELECT 1 FROM AttendanceBatchStudents x WHERE x.BatchId = @BatchId AND x.StudentId = s.StudentId);

    PRINT 'Students added: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

    -- Expect 18
    SELECT COUNT(*) AS ActiveStudentsInBatch
    FROM AttendanceBatchStudents bs
    JOIN Students s ON s.StudentId = bs.StudentId AND s.Status = 'Active'
    WHERE bs.BatchId = @BatchId;
END
GO
