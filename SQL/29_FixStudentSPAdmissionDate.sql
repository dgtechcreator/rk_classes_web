-- ============================================================
-- 29: Add AdmissionDate to Students + fix sp_SaveStudent
-- Run this in SSMS (safe to run even if 27 was already run)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='AdmissionDate')
    ALTER TABLE Students ADD AdmissionDate DATE NULL;
GO

IF OBJECT_ID('sp_SaveStudent','P') IS NOT NULL DROP PROC sp_SaveStudent;
GO
CREATE PROCEDURE sp_SaveStudent
  @StudentId      INT             = 0,
  @AdmissionNo    NVARCHAR(30)    = NULL,
  @FullName       NVARCHAR(150),
  @DateOfBirth    DATE            = NULL,
  @Gender         NVARCHAR(10)    = NULL,
  @FatherName     NVARCHAR(150)   = NULL,
  @MotherName     NVARCHAR(150)   = NULL,
  @Phone          NVARCHAR(20)    = NULL,
  @FatherPhone    NVARCHAR(20)    = NULL,
  @MotherPhone    NVARCHAR(20)    = NULL,
  @Email          NVARCHAR(150)   = NULL,
  @Address        NVARCHAR(300)   = NULL,
  @ProfilePicPath NVARCHAR(500)   = NULL,
  @AcademicYearId INT             = NULL,
  @ClassId        INT             = NULL,
  @SectionId      INT             = NULL,
  @BatchId        INT             = NULL,
  @RollNo         NVARCHAR(20)    = NULL,
  @BloodGroup     NVARCHAR(5)     = NULL,
  @Status         NVARCHAR(20)    = 'Active',
  @AdmissionDate  DATE            = NULL,
  @CreatedBy      INT             = NULL,
  @NewStudentId   INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  IF @StudentId = 0 BEGIN
    IF @AdmissionNo IS NULL
      SET @AdmissionNo = 'RKC-' + CAST(YEAR(GETDATE()) AS NVARCHAR) + '-'
        + RIGHT('000' + CAST((SELECT ISNULL(MAX(StudentId),0)+1 FROM Students) AS NVARCHAR), 4);
    IF @AdmissionDate IS NULL SET @AdmissionDate = CAST(GETDATE() AS DATE);
    INSERT INTO Students(AdmissionNo, FullName, DateOfBirth, Gender,
      FatherName, MotherName, Phone, FatherPhone, MotherPhone,
      Email, Address, ProfilePicPath,
      AcademicYearId, ClassId, SectionId, BatchId, RollNo, BloodGroup,
      Status, AdmissionDate, CreatedBy)
    VALUES(@AdmissionNo, @FullName, @DateOfBirth, @Gender,
      @FatherName, @MotherName, @Phone, @FatherPhone, @MotherPhone,
      @Email, @Address, @ProfilePicPath,
      @AcademicYearId, @ClassId, @SectionId, @BatchId, @RollNo, @BloodGroup,
      @Status, @AdmissionDate, @CreatedBy);
    SET @NewStudentId = SCOPE_IDENTITY();
  END ELSE BEGIN
    UPDATE Students SET
      FullName       = @FullName,
      DateOfBirth    = @DateOfBirth,
      Gender         = @Gender,
      FatherName     = @FatherName,
      MotherName     = @MotherName,
      Phone          = @Phone,
      FatherPhone    = @FatherPhone,
      MotherPhone    = @MotherPhone,
      Email          = @Email,
      Address        = @Address,
      AcademicYearId = @AcademicYearId,
      ClassId        = @ClassId,
      SectionId      = @SectionId,
      BatchId        = @BatchId,
      RollNo         = @RollNo,
      BloodGroup     = @BloodGroup,
      Status         = @Status,
      AdmissionDate  = ISNULL(@AdmissionDate, AdmissionDate),
      ProfilePicPath = ISNULL(@ProfilePicPath, ProfilePicPath)
    WHERE StudentId = @StudentId;
    SET @NewStudentId = @StudentId;
  END
END
GO

PRINT 'Done. sp_SaveStudent updated with AdmissionDate + ProfilePicPath.';
GO
