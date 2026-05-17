-- ============================================================
-- SchoolManagementDB  |  01_Schema.sql
-- Run in SSMS before starting the application
-- ============================================================
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'SchoolManagementDB')
    CREATE DATABASE SchoolManagementDB;
GO
USE SchoolManagementDB;
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Roles')
CREATE TABLE Roles (
    RoleId   INT IDENTITY PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE,
    IsActive BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Users')
CREATE TABLE Users (
    UserId       INT IDENTITY PRIMARY KEY,
    FullName     NVARCHAR(150) NOT NULL,
    Username     NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(300) NOT NULL,
    RoleId       INT REFERENCES Roles(RoleId),
    Email        NVARCHAR(150),
    Phone        NVARCHAR(20),
    IsActive     BIT DEFAULT 1,
    CreatedAt    DATETIME DEFAULT GETDATE(),
    LastLogin    DATETIME
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='AcademicYears')
CREATE TABLE AcademicYears (
    YearId    INT IDENTITY PRIMARY KEY,
    YearName  NVARCHAR(20) NOT NULL,
    IsCurrent BIT DEFAULT 0,
    IsActive  BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Classes')
CREATE TABLE Classes (
    ClassId   INT IDENTITY PRIMARY KEY,
    ClassName NVARCHAR(50) NOT NULL,
    OrderNo   INT DEFAULT 0,
    IsActive  BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Sections')
CREATE TABLE Sections (
    SectionId   INT IDENTITY PRIMARY KEY,
    SectionName NVARCHAR(50) NOT NULL,
    IsActive    BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Batches')
CREATE TABLE Batches (
    BatchId   INT IDENTITY PRIMARY KEY,
    BatchName NVARCHAR(50) NOT NULL,
    IsActive  BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Students')
CREATE TABLE Students (
    StudentId      INT IDENTITY PRIMARY KEY,
    AdmissionNo    NVARCHAR(30) NOT NULL UNIQUE,
    FullName       NVARCHAR(150) NOT NULL,
    DateOfBirth    DATE,
    Gender         NVARCHAR(10),
    FatherName     NVARCHAR(150),
    MotherName     NVARCHAR(150),
    Phone          NVARCHAR(20),
    Email          NVARCHAR(150),
    Address        NVARCHAR(300),
    ProfilePicPath NVARCHAR(500),
    AcademicYearId INT REFERENCES AcademicYears(YearId),
    ClassId        INT REFERENCES Classes(ClassId),
    SectionId      INT REFERENCES Sections(SectionId),
    BatchId        INT REFERENCES Batches(BatchId),
    RollNo         NVARCHAR(20),
    BloodGroup     NVARCHAR(5),
    Status         NVARCHAR(20) DEFAULT 'Active',
    AdmissionDate  DATE DEFAULT GETDATE(),
    CreatedAt      DATETIME DEFAULT GETDATE(),
    CreatedBy      INT REFERENCES Users(UserId)
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Subjects')
CREATE TABLE Subjects (
    SubjectId   INT IDENTITY PRIMARY KEY,
    SubjectName NVARCHAR(100) NOT NULL,
    SubjectCode NVARCHAR(20),
    ClassId     INT REFERENCES Classes(ClassId),
    MaxMarks    INT DEFAULT 100,
    PassMarks   INT DEFAULT 35,
    IsActive    BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Exams')
CREATE TABLE Exams (
    ExamId         INT IDENTITY PRIMARY KEY,
    ExamName       NVARCHAR(100) NOT NULL,
    AcademicYearId INT REFERENCES AcademicYears(YearId),
    ClassId        INT REFERENCES Classes(ClassId),
    IsActive       BIT DEFAULT 1,
    CreatedAt      DATETIME DEFAULT GETDATE()
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Attendance')
CREATE TABLE Attendance (
    AttendanceId   INT IDENTITY PRIMARY KEY,
    StudentId      INT NOT NULL REFERENCES Students(StudentId),
    AttendanceDate DATE NOT NULL,
    Status         NVARCHAR(10) DEFAULT 'Present',
    ClassId        INT REFERENCES Classes(ClassId),
    SectionId      INT REFERENCES Sections(SectionId),
    BatchId        INT REFERENCES Batches(BatchId),
    Remarks        NVARCHAR(200),
    MarkedBy       INT REFERENCES Users(UserId),
    MarkedAt       DATETIME DEFAULT GETDATE(),
    UNIQUE(StudentId, AttendanceDate)
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='TestMarks')
CREATE TABLE TestMarks (
    MarkId         INT IDENTITY PRIMARY KEY,
    StudentId      INT NOT NULL REFERENCES Students(StudentId),
    ExamId         INT NOT NULL REFERENCES Exams(ExamId),
    SubjectId      INT NOT NULL REFERENCES Subjects(SubjectId),
    MarksObtained  DECIMAL(6,2),
    MaxMarks       INT DEFAULT 100,
    Grade          NVARCHAR(5),
    EnteredBy      INT REFERENCES Users(UserId),
    EnteredAt      DATETIME DEFAULT GETDATE(),
    UNIQUE(StudentId, ExamId, SubjectId)
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='FeeTypes')
CREATE TABLE FeeTypes (
    FeeTypeId INT IDENTITY PRIMARY KEY,
    TypeName  NVARCHAR(100) NOT NULL,
    IsActive  BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='FeePayments')
CREATE TABLE FeePayments (
    PaymentId      INT IDENTITY PRIMARY KEY,
    ReceiptNo      NVARCHAR(30) NOT NULL UNIQUE,
    StudentId      INT NOT NULL REFERENCES Students(StudentId),
    FeeTypeId      INT REFERENCES FeeTypes(FeeTypeId),
    Amount         DECIMAL(10,2) NOT NULL,
    Discount       DECIMAL(10,2) DEFAULT 0,
    LateFine       DECIMAL(10,2) DEFAULT 0,
    NetAmount      DECIMAL(10,2),
    PaymentDate    DATE DEFAULT GETDATE(),
    PaymentMode    NVARCHAR(30) DEFAULT 'Cash',
    TransactionRef NVARCHAR(100),
    AcademicYearId INT REFERENCES AcademicYears(YearId),
    Month          NVARCHAR(20),
    Remarks        NVARCHAR(300),
    CollectedBy    INT REFERENCES Users(UserId),
    CreatedAt      DATETIME DEFAULT GETDATE()
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='ExpenseCategories')
CREATE TABLE ExpenseCategories (
    CategoryId   INT IDENTITY PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL,
    IsActive     BIT DEFAULT 1
);
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='Expenses')
CREATE TABLE Expenses (
    ExpenseId   INT IDENTITY PRIMARY KEY,
    ExpenseNo   NVARCHAR(30) NOT NULL UNIQUE,
    CategoryId  INT REFERENCES ExpenseCategories(CategoryId),
    Title       NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    Amount      DECIMAL(10,2) NOT NULL,
    ExpenseDate DATE NOT NULL,
    PaymentMode NVARCHAR(30) DEFAULT 'Cash',
    BillNo      NVARCHAR(50),
    VendorName  NVARCHAR(150),
    EnteredBy   INT REFERENCES Users(UserId),
    CreatedAt   DATETIME DEFAULT GETDATE()
);
GO

PRINT '✅ Schema created successfully.';
GO
