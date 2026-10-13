-- ============================================================
-- 58_NoBorrowedFees.sql   (run after 57)
-- Rule: a student's fee is ONLY his own active StudentFees rows. Nothing is borrowed from another class / medium.
--  1. sp_GetFeeStructureByStudent no longer falls back to class-level / any-section lookups (that is what summed
--     Hindi + English heads for one student). No active row = empty = "fee not set" (see web page /DataHealth).
--  2. The stand-in rows copied from ANOTHER section for students whose own section has no fee head
--     (inserted by the 13-Oct audit, StudentFeeId > 482) are switched OFF (IsActive=0, nothing deleted).
--     Those students then show "Fee not set" until the fee head is added in Fee Setup.
--     Older rows (StudentFeeId <= 482) are left alone - the Data Health page lists them for a human decision.
-- ============================================================
ALTER PROCEDURE sp_GetFeeStructureByStudent
    @StudentId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT fs.StructureId, fs.AcademicYearId, fs.ClassId, fs.SectionId, fs.FeeTypeId,
           sf.Amount AS Amount, fs.DueDay, fs.IsMonthly, fs.Remarks, fs.IsActive,
           fs.CreatedBy, fs.CreatedAt, fs.DeletedBy, fs.DeletedAt,
           ay.YearName, c.ClassName, s.SectionName,
           ft.TypeName AS FeeTypeName,
           u.FullName  AS CreatedByName
    FROM   StudentFees sf
    INNER JOIN FeeStructure  fs ON fs.StructureId = sf.StructureId
    LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
    LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
    LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
    LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
    LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
    WHERE sf.StudentId = @StudentId AND sf.IsActive = 1
    ORDER BY ft.TypeName;
END
GO

UPDATE sf
SET IsActive=0, DeactivatedAt=GETDATE(), DeactivatedReason='Borrowed from another section; own section has no fee head - fee left empty'
FROM StudentFees sf
JOIN Students s       ON s.StudentId=sf.StudentId AND s.Status='Active'
JOIN FeeStructure old ON old.StructureId=sf.StructureId
WHERE sf.IsActive=1 AND sf.StudentFeeId>482
  AND ISNULL(sf.PaidAmount,0)=0 AND sf.Status='Pending'
  AND (old.ClassId<>s.ClassId OR old.SectionId<>s.SectionId OR old.AcademicYearId<>s.AcademicYearId)
  AND NOT EXISTS (SELECT 1 FROM FeeStructure own WHERE own.IsActive=1 AND own.AcademicYearId=s.AcademicYearId
                  AND own.ClassId=s.ClassId AND own.SectionId=s.SectionId);
PRINT 'borrowed rows switched off: ' + CAST(@@ROWCOUNT AS VARCHAR);
GO
