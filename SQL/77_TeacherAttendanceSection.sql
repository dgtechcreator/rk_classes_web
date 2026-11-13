-- ============================================================
-- 77_TeacherAttendanceSection.sql
-- Teacher attendance now records the Stream / Medium of the lecture (Section master: Science, Commerce, Hindi, English ...),
-- next to Class + Batch, so a teacher's hours can be split class-wise and medium-wise (rates differ per class + medium).
-- Nullable: every existing entry stays as it is (shown without a medium).
-- The web app also adds this column by itself on first use - running this script is optional. Safe to run more than once.
-- Undo: ALTER TABLE TeacherAttendance DROP COLUMN SectionId;
-- ============================================================
IF COL_LENGTH('dbo.TeacherAttendance', 'SectionId') IS NULL
    ALTER TABLE dbo.TeacherAttendance ADD SectionId INT NULL;
GO
