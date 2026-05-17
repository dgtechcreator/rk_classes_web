-- ============================================================
-- 09_Faculty.sql
-- Faculty master - add, edit, view faculty members
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Designations master ──────────────────────────────────────
IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Designations')
CREATE TABLE Designations (
    DesignationId   INT IDENTITY PRIMARY KEY,
    DesignationName NVARCHAR(100) NOT NULL,
    IsActive        BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT 1 FROM Designations)
INSERT INTO Designations(DesignationName) VALUES
('Principal'),('Vice Principal'),('Head Teacher'),
('Senior Teacher'),('Teacher'),('Assistant Teacher'),
('Lab Assistant'),('Librarian'),('Accountant'),('Admin Staff');
GO

-- ── Faculty table ─────────────────────────────────────────────
IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Faculty')
CREATE TABLE Faculty (
    FacultyId       INT IDENTITY PRIMARY KEY,
    EmployeeCode    NVARCHAR(30) NOT NULL UNIQUE,
    FullName        NVARCHAR(150) NOT NULL,
    DesignationId   INT REFERENCES Designations(DesignationId),
    Qualification   NVARCHAR(200),
    Specialization  NVARCHAR(200),     -- subjects they teach
    Gender          NVARCHAR(10),
    DateOfBirth     DATE,
    DateOfJoining   DATE,
    Phone           NVARCHAR(20),
    AlternatePhone  NVARCHAR(20),
    Email           NVARCHAR(150),
    Address         NVARCHAR(300),
    ProfilePicPath  NVARCHAR(500),
    Salary          DECIMAL(10,2),
    BloodGroup      NVARCHAR(5),
    AadharNo        NVARCHAR(20),
    Status          NVARCHAR(20) DEFAULT 'Active',  -- Active/Inactive/Relieved
    Remarks         NVARCHAR(500),
    CreatedBy       INT REFERENCES Users(UserId),
    CreatedAt       DATETIME DEFAULT GETDATE(),
    UpdatedAt       DATETIME DEFAULT GETDATE()
);
GO

-- ── FacultySubjects - which classes/subjects they teach ───────
IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='FacultySubjects')
CREATE TABLE FacultySubjects (
    FacultySubjectId INT IDENTITY PRIMARY KEY,
    FacultyId        INT NOT NULL REFERENCES Faculty(FacultyId),
    ClassId          INT REFERENCES Classes(ClassId),
    SubjectId        INT REFERENCES Subjects(SubjectId),
    SectionId        INT REFERENCES Sections(SectionId),
    IsActive         BIT DEFAULT 1,
    UNIQUE(FacultyId, ClassId, SubjectId, SectionId)
);
GO

-- ── Stored Procedures ─────────────────────────────────────────

IF OBJECT_ID('sp_GetFaculty','P') IS NOT NULL DROP PROC sp_GetFaculty; GO
CREATE PROCEDURE sp_GetFaculty
    @Search   NVARCHAR(200) = NULL,
    @Status   NVARCHAR(20)  = NULL,
    @DesignationId INT       = NULL,
    @PageNo   INT = 1,
    @PageSize INT = 15,
    @TotalCount INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    SELECT @TotalCount = COUNT(*) FROM Faculty f
    WHERE (@Search        IS NULL OR f.FullName LIKE '%'+@Search+'%' OR f.EmployeeCode LIKE '%'+@Search+'%' OR f.Phone LIKE '%'+@Search+'%')
      AND (@Status        IS NULL OR f.Status        = @Status)
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId);

    SELECT f.*, d.DesignationName
    FROM Faculty f
    LEFT JOIN Designations d ON d.DesignationId = f.DesignationId
    WHERE (@Search        IS NULL OR f.FullName LIKE '%'+@Search+'%' OR f.EmployeeCode LIKE '%'+@Search+'%' OR f.Phone LIKE '%'+@Search+'%')
      AND (@Status        IS NULL OR f.Status        = @Status)
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId)
    ORDER BY f.FullName
    OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

IF OBJECT_ID('sp_GetFacultyById','P') IS NOT NULL DROP PROC sp_GetFacultyById; GO
CREATE PROCEDURE sp_GetFacultyById @FacultyId INT AS BEGIN
    SELECT f.*, d.DesignationName
    FROM Faculty f
    LEFT JOIN Designations d ON d.DesignationId = f.DesignationId
    WHERE f.FacultyId = @FacultyId;

    -- Also return assigned subjects
    SELECT fs.*, c.ClassName, sub.SubjectName, sec.SectionName
    FROM FacultySubjects fs
    LEFT JOIN Classes   c   ON c.ClassId   = fs.ClassId
    LEFT JOIN Subjects  sub ON sub.SubjectId= fs.SubjectId
    LEFT JOIN Sections  sec ON sec.SectionId= fs.SectionId
    WHERE fs.FacultyId=@FacultyId AND fs.IsActive=1;
END
GO

IF OBJECT_ID('sp_SaveFaculty','P') IS NOT NULL DROP PROC sp_SaveFaculty; GO
CREATE PROCEDURE sp_SaveFaculty
    @FacultyId      INT = 0,
    @EmployeeCode   NVARCHAR(30) = NULL,
    @FullName       NVARCHAR(150),
    @DesignationId  INT = NULL,
    @Qualification  NVARCHAR(200) = NULL,
    @Specialization NVARCHAR(200) = NULL,
    @Gender         NVARCHAR(10) = NULL,
    @DateOfBirth    DATE = NULL,
    @DateOfJoining  DATE = NULL,
    @Phone          NVARCHAR(20) = NULL,
    @AlternatePhone NVARCHAR(20) = NULL,
    @Email          NVARCHAR(150) = NULL,
    @Address        NVARCHAR(300) = NULL,
    @ProfilePicPath NVARCHAR(500) = NULL,
    @Salary         DECIMAL(10,2) = NULL,
    @BloodGroup     NVARCHAR(5) = NULL,
    @AadharNo       NVARCHAR(20) = NULL,
    @Status         NVARCHAR(20) = 'Active',
    @Remarks        NVARCHAR(500) = NULL,
    @CreatedBy      INT = NULL,
    @NewFacultyId   INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @FacultyId = 0 BEGIN
        IF @EmployeeCode IS NULL
            SET @EmployeeCode = 'FAC-'+RIGHT('000'+CAST((SELECT ISNULL(MAX(FacultyId),0)+1 FROM Faculty) AS NVARCHAR),4);
        INSERT INTO Faculty(EmployeeCode,FullName,DesignationId,Qualification,Specialization,
            Gender,DateOfBirth,DateOfJoining,Phone,AlternatePhone,Email,Address,
            ProfilePicPath,Salary,BloodGroup,AadharNo,Status,Remarks,CreatedBy)
        VALUES(@EmployeeCode,@FullName,@DesignationId,@Qualification,@Specialization,
            @Gender,@DateOfBirth,@DateOfJoining,@Phone,@AlternatePhone,@Email,@Address,
            @ProfilePicPath,@Salary,@BloodGroup,@AadharNo,@Status,@Remarks,@CreatedBy);
        SET @NewFacultyId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE Faculty SET
            FullName=@FullName, DesignationId=@DesignationId, Qualification=@Qualification,
            Specialization=@Specialization, Gender=@Gender, DateOfBirth=@DateOfBirth,
            DateOfJoining=@DateOfJoining, Phone=@Phone, AlternatePhone=@AlternatePhone,
            Email=@Email, Address=@Address, Salary=@Salary, BloodGroup=@BloodGroup,
            AadharNo=@AadharNo, Status=@Status, Remarks=@Remarks, UpdatedAt=GETDATE(),
            ProfilePicPath = ISNULL(@ProfilePicPath, ProfilePicPath)
        WHERE FacultyId=@FacultyId;
        SET @NewFacultyId = @FacultyId;
    END
END
GO

IF OBJECT_ID('sp_GetDesignations','P') IS NOT NULL DROP PROC sp_GetDesignations; GO
CREATE PROCEDURE sp_GetDesignations AS BEGIN
    SELECT * FROM Designations WHERE IsActive=1 ORDER BY DesignationId;
END
GO

IF OBJECT_ID('sp_SaveDesignation','P') IS NOT NULL DROP PROC sp_SaveDesignation; GO
CREATE PROCEDURE sp_SaveDesignation
    @DesignationId   INT = 0,
    @DesignationName NVARCHAR(100),
    @IsActive        BIT = 1,
    @NewId           INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @DesignationId = 0 BEGIN
        INSERT INTO Designations(DesignationName,IsActive) VALUES(@DesignationName,@IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE Designations SET DesignationName=@DesignationName,IsActive=@IsActive WHERE DesignationId=@DesignationId;
        SET @NewId = @DesignationId;
    END
END
GO

PRINT '✅ Faculty module created.';
GO
