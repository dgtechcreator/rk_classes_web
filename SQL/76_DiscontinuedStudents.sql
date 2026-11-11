-- 76: Discontinued Students - students who left the classes or were removed, with the reason, who recorded it and a snapshot of what
-- was paid / pending that day. The student row itself is set Inactive (same switch as Deleted Records), so the student vanishes from
-- every count, list and report; nothing is erased. The website creates this table and the permission module by itself on start,
-- so running this script is optional. Safe to run more than once.
IF OBJECT_ID('dbo.DiscontinuedStudents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscontinuedStudents (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscontinuedStudents PRIMARY KEY,
        StudentId      INT            NOT NULL,
        Kind           NVARCHAR(20)   NOT NULL,          -- Left (by student / parents) | Removed (by institute)
        Reason         NVARCHAR(1000) NOT NULL,
        LeftOn         DATE           NOT NULL,
        ClassName      NVARCHAR(100)  NULL,
        SectionName    NVARCHAR(100)  NULL,
        BatchName      NVARCHAR(100)  NULL,
        TotalFees      DECIMAL(18,2)  NOT NULL DEFAULT 0,  -- fee + additional charges - discount
        Discount       DECIMAL(18,2)  NOT NULL DEFAULT 0,
        Paid           DECIMAL(18,2)  NOT NULL DEFAULT 0,
        Pending        DECIMAL(18,2)  NOT NULL DEFAULT 0,
        AttendancePct  DECIMAL(9,2)   NOT NULL DEFAULT 0,
        MarksPct       DECIMAL(9,2)   NOT NULL DEFAULT 0,
        RecordedBy     INT            NULL,
        RecordedByName NVARCHAR(150)  NULL,
        RecordedAt     DATETIME       NOT NULL CONSTRAINT DF_DiscontinuedStudents_At DEFAULT GETDATE()
    );
    CREATE INDEX IX_DiscontinuedStudents_Student ON dbo.DiscontinuedStudents (StudentId);
END
GO
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'discontinued_students')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Discontinued Students', 'discontinued_students', 'fas fa-user-slash', 'Students', 32, 1);
GO
PRINT 'DiscontinuedStudents ready.';
