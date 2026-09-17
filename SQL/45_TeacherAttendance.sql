-- ============================================================
-- 45_TeacherAttendance.sql
-- Teacher attendance tracking system
-- ============================================================
USE SchoolManagementDB;
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='TeacherAttendance')
BEGIN
    CREATE TABLE TeacherAttendance (
        AttendanceId    INT IDENTITY PRIMARY KEY,
        FacultyId       INT NOT NULL REFERENCES Faculty(FacultyId),
        ClassId         INT REFERENCES Classes(ClassId),
        BatchId         INT REFERENCES Batches(BatchId),
        SubjectName     NVARCHAR(100),
        Topic           NVARCHAR(500),
        AttendanceDate  DATE NOT NULL,
        InTime          TIME,
        OutTime         TIME,
        TotalHours      DECIMAL(5,2),
        CreatedAt       DATETIME DEFAULT GETDATE(),
        CreatedBy       INT REFERENCES Users(UserId),
        IsDeleted       BIT DEFAULT 0,
        DeletedAt       DATETIME,
        DeletedBy       INT
    );

    CREATE INDEX IX_TeacherAttendance_FacultyId ON TeacherAttendance(FacultyId);
    CREATE INDEX IX_TeacherAttendance_Date ON TeacherAttendance(AttendanceDate);
    CREATE INDEX IX_TeacherAttendance_IsDeleted ON TeacherAttendance(IsDeleted);

    PRINT '✅ TeacherAttendance table created successfully.';
END
ELSE
BEGIN
    -- If table exists with old schema, add SubjectName column if it doesn't exist
    IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('TeacherAttendance') AND name='SubjectName')
    BEGIN
        ALTER TABLE TeacherAttendance ADD SubjectName NVARCHAR(100);
        PRINT '✅ SubjectName column added to TeacherAttendance table.';
    END

    -- Drop SubjectId foreign key constraint if it exists
    IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_TeacherAttendance_Subjects' AND parent_object_id=OBJECT_ID('TeacherAttendance'))
    BEGIN
        ALTER TABLE TeacherAttendance DROP CONSTRAINT FK_TeacherAttendance_Subjects;
        PRINT '✅ SubjectId foreign key constraint removed.';
    END

    -- Drop SubjectId column if it exists
    IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('TeacherAttendance') AND name='SubjectId')
    BEGIN
        ALTER TABLE TeacherAttendance DROP COLUMN SubjectId;
        PRINT '✅ SubjectId column removed from TeacherAttendance table.';
    END

    -- Expand Topic column if needed
    IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('TeacherAttendance') AND name='Topic' AND max_length < 1000)
    BEGIN
        ALTER TABLE TeacherAttendance ALTER COLUMN Topic NVARCHAR(500);
        PRINT '✅ Topic column expanded to NVARCHAR(500).';
    END
END
GO
