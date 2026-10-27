-- 72: class notes + daily practice papers, and the "class_notes" module right.
-- The files themselves live in App_Data/class-notes on the web server (not in wwwroot) and are served only through the notes API / pages.
-- Safe to run more than once. The website also creates all of this by itself at start-up (ClassNotesRepo.EnsureSchema), so running it
-- up front is optional.
IF OBJECT_ID('dbo.ClassNotes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClassNotes (
        NoteId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassNotes PRIMARY KEY,
        Kind          NVARCHAR(20)  NOT NULL,          -- Notes | Practice
        Title         NVARCHAR(200) NOT NULL,
        Description   NVARCHAR(1000) NULL,
        ClassId       INT NOT NULL,
        SectionId     INT NULL,                        -- NULL = every medium
        BatchId       INT NULL,                        -- NULL = every batch
        SubjectName   NVARCHAR(100) NULL,
        FileName      NVARCHAR(260) NOT NULL,
        StoredName    NVARCHAR(100) NOT NULL,
        FileSize      BIGINT NOT NULL DEFAULT 0,
        PaperDate     DATE NOT NULL,
        CreatedBy     INT NULL,
        CreatedByName NVARCHAR(150) NULL,
        CreatedAt     DATETIME NOT NULL CONSTRAINT DF_ClassNotes_CreatedAt DEFAULT GETDATE(),
        IsDeleted     BIT NOT NULL CONSTRAINT DF_ClassNotes_IsDeleted DEFAULT 0
    );
    CREATE INDEX IX_ClassNotes_Class ON dbo.ClassNotes (ClassId, IsDeleted, PaperDate DESC);
END
GO
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'class_notes')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Notes & Practice Papers', 'class_notes', 'fas fa-file-lines', 'Academics', 70, 1);
GO
PRINT 'ClassNotes + class_notes module ready.';
