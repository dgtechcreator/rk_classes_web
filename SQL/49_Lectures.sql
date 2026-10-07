-- ============================================================
-- 49_Lectures.sql
-- Lecture schedule module: admin schedules lectures (class / medium / batch + subject + teacher),
-- teachers see their own, parents see their child's, and summaries count lectures actually held.
-- Purely additive (new table + two permission modules). Nothing is ever hard-deleted: IsDeleted/DeletedAt/DeletedBy.
-- ============================================================
SET QUOTED_IDENTIFIER ON;   -- required for the filtered indexes below
GO
USE SchoolManagementDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Lectures')
BEGIN
    CREATE TABLE Lectures (
        LectureId    INT IDENTITY(1,1) PRIMARY KEY,
        LectureDate  DATE          NOT NULL,
        StartTime    TIME(0)       NOT NULL,
        EndTime      TIME(0)       NOT NULL,
        ClassId      INT           NOT NULL REFERENCES Classes(ClassId),
        SectionId    INT           NULL     REFERENCES Sections(SectionId),   -- NULL = every medium of the class
        BatchId      INT           NULL     REFERENCES Batches(BatchId),      -- NULL = every batch (Morning/Evening...)
        SubjectName  NVARCHAR(100) NOT NULL,
        FacultyId    INT           NOT NULL REFERENCES Faculty(FacultyId),
        Topic        NVARCHAR(200) NULL,
        Remarks      NVARCHAR(300) NULL,
        Status       NVARCHAR(20)  NOT NULL CONSTRAINT DF_Lectures_Status DEFAULT 'Scheduled',   -- Scheduled | Completed | Cancelled
        StatusNote   NVARCHAR(200) NULL,                                                          -- e.g. cancel reason
        SeriesId     UNIQUEIDENTIFIER NULL,                                                       -- groups a recurring schedule
        CreatedBy    INT           NULL,
        CreatedAt    DATETIME      NOT NULL CONSTRAINT DF_Lectures_CreatedAt DEFAULT GETDATE(),
        UpdatedBy    INT           NULL,
        UpdatedAt    DATETIME      NULL,
        IsDeleted    BIT           NOT NULL CONSTRAINT DF_Lectures_IsDeleted DEFAULT 0,
        DeletedBy    INT           NULL,
        DeletedAt    DATETIME      NULL,
        CONSTRAINT CK_Lectures_Status CHECK (Status IN ('Scheduled','Completed','Cancelled')),
        CONSTRAINT CK_Lectures_Time   CHECK (EndTime > StartTime)
    );
    PRINT 'Lectures table created.';
END
GO

SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Lectures_Date' AND object_id = OBJECT_ID('Lectures'))
    CREATE INDEX IX_Lectures_Date    ON Lectures(LectureDate) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Lectures_Faculty' AND object_id = OBJECT_ID('Lectures'))
    CREATE INDEX IX_Lectures_Faculty ON Lectures(FacultyId, LectureDate) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Lectures_Class' AND object_id = OBJECT_ID('Lectures'))
    CREATE INDEX IX_Lectures_Class   ON Lectures(ClassId, LectureDate) WHERE IsDeleted = 0;
GO

-- Permission modules (appear in Users -> Permissions). "Lecture Schedule": View = see all lectures,
-- Edit = create / edit / cancel / mark done / delete. "Lecture Summary": View = counts per teacher / class / subject.
-- Teachers' own "My Lectures" and parents' lecture view need no module permission.
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'lecture_schedule')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Lecture Schedule', 'lecture_schedule', 'fas fa-chalkboard-teacher', 'Academics', 95, 1);
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'lecture_summary')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Lecture Summary', 'lecture_summary', 'fas fa-chart-pie', 'Academics', 96, 1);
GO
