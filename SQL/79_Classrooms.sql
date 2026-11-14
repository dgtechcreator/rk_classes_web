-- 79: Classrooms (rooms lectures are held in) + the room of a lecture.
-- Safe to run more than once. The website also creates these by itself the first time it needs them
-- (ClassroomRepo.CreateSchema), so running it up front is optional.
IF OBJECT_ID('dbo.Classrooms', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Classrooms (
        ClassroomId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Classrooms PRIMARY KEY,
        Name        NVARCHAR(80)  NOT NULL,
        Capacity    INT           NULL,
        Location    NVARCHAR(120) NULL,        -- floor / building
        IsActive    BIT           NOT NULL CONSTRAINT DF_Classrooms_IsActive DEFAULT 1,
        IsDeleted   BIT           NOT NULL CONSTRAINT DF_Classrooms_IsDeleted DEFAULT 0,
        CreatedBy   INT           NULL,
        CreatedAt   DATETIME      NOT NULL CONSTRAINT DF_Classrooms_CreatedAt DEFAULT GETDATE(),
        DeletedBy   INT           NULL,
        DeletedAt   DATETIME      NULL
    );
END
GO
IF COL_LENGTH('dbo.Lectures', 'ClassroomId') IS NULL ALTER TABLE dbo.Lectures ADD ClassroomId INT NULL;
GO
PRINT 'Classrooms ready.';
