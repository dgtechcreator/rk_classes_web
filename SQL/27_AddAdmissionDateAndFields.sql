USE SchoolManagementDB;
GO
-- Add extra columns if not exist
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='Nationality')
    ALTER TABLE Students ADD Nationality NVARCHAR(50) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='MotherTongue')
    ALTER TABLE Students ADD MotherTongue NVARCHAR(50) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='Religion')
    ALTER TABLE Students ADD Religion NVARCHAR(50) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='PlaceOfBirth')
    ALTER TABLE Students ADD PlaceOfBirth NVARCHAR(100) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='AadhaarNo')
    ALTER TABLE Students ADD AadhaarNo NVARCHAR(20) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='AlternatePhone')
    ALTER TABLE Students ADD AlternatePhone NVARCHAR(20) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='PreviousPercentage')
    ALTER TABLE Students ADD PreviousPercentage NVARCHAR(20) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='PreviousSchool')
    ALTER TABLE Students ADD PreviousSchool NVARCHAR(200) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='Board')
    ALTER TABLE Students ADD Board NVARCHAR(50) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='FatherOccupation')
    ALTER TABLE Students ADD FatherOccupation NVARCHAR(100) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='MotherOccupation')
    ALTER TABLE Students ADD MotherOccupation NVARCHAR(100) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='GuardianName')
    ALTER TABLE Students ADD GuardianName NVARCHAR(150) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='GuardianOccupation')
    ALTER TABLE Students ADD GuardianOccupation NVARCHAR(100) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='GuardianPhone')
    ALTER TABLE Students ADD GuardianPhone NVARCHAR(20) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='City')
    ALTER TABLE Students ADD City NVARCHAR(100) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='State')
    ALTER TABLE Students ADD State NVARCHAR(100) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='District')
    ALTER TABLE Students ADD District NVARCHAR(100) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='Pincode')
    ALTER TABLE Students ADD Pincode NVARCHAR(10) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Students') AND name='PermanentAddress')
    ALTER TABLE Students ADD PermanentAddress NVARCHAR(300) NULL;
GO

-- Recreate sp_SaveStudent with all new fields including AdmissionDate
IF OBJECT_ID('sp_SaveStudent','P') IS NOT NULL DROP PROC sp_SaveStudent;
GO
CREATE PROCEDURE sp_SaveStudent
  @StudentId          INT            = 0,
  @AdmissionNo        NVARCHAR(30)   = NULL,
  @FullName           NVARCHAR(150),
  @DateOfBirth        DATE           = NULL,
  @Gender             NVARCHAR(10)   = NULL,
  @FatherName         NVARCHAR(150)  = NULL,
  @MotherName         NVARCHAR(150)  = NULL,
  @Phone              NVARCHAR(20)   = NULL,
  @FatherPhone        NVARCHAR(20)   = NULL,
  @MotherPhone        NVARCHAR(20)   = NULL,
  @Email              NVARCHAR(150)  = NULL,
  @Address            NVARCHAR(300)  = NULL,
  @ProfilePicPath     NVARCHAR(500)  = NULL,
  @AcademicYearId     INT            = NULL,
  @ClassId            INT            = NULL,
  @SectionId          INT            = NULL,
  @BatchId            INT            = NULL,
  @RollNo             NVARCHAR(20)   = NULL,
  @BloodGroup         NVARCHAR(5)    = NULL,
  @Status             NVARCHAR(20)   = 'Active',
  @AdmissionDate      DATE           = NULL,
  @Nationality        NVARCHAR(50)   = NULL,
  @MotherTongue       NVARCHAR(50)   = NULL,
  @Religion           NVARCHAR(50)   = NULL,
  @PlaceOfBirth       NVARCHAR(100)  = NULL,
  @AadhaarNo          NVARCHAR(20)   = NULL,
  @AlternatePhone     NVARCHAR(20)   = NULL,
  @PreviousPercentage NVARCHAR(20)   = NULL,
  @PreviousSchool     NVARCHAR(200)  = NULL,
  @Board              NVARCHAR(50)   = NULL,
  @FatherOccupation   NVARCHAR(100)  = NULL,
  @MotherOccupation   NVARCHAR(100)  = NULL,
  @GuardianName       NVARCHAR(150)  = NULL,
  @GuardianOccupation NVARCHAR(100)  = NULL,
  @GuardianPhone      NVARCHAR(20)   = NULL,
  @City               NVARCHAR(100)  = NULL,
  @State              NVARCHAR(100)  = NULL,
  @District           NVARCHAR(100)  = NULL,
  @Pincode            NVARCHAR(10)   = NULL,
  @PermanentAddress   NVARCHAR(300)  = NULL,
  @CreatedBy          INT            = NULL,
  @NewStudentId       INT            OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  IF @StudentId = 0 BEGIN
    IF @AdmissionNo IS NULL
      SET @AdmissionNo = 'ADM-' + CAST(YEAR(GETDATE()) AS NVARCHAR) + '-'
        + RIGHT('000' + CAST((SELECT ISNULL(MAX(StudentId),0)+1 FROM Students) AS NVARCHAR), 4);
    IF @AdmissionDate IS NULL SET @AdmissionDate = CAST(GETDATE() AS DATE);
    INSERT INTO Students(AdmissionNo,FullName,DateOfBirth,Gender,FatherName,MotherName,
      Phone,FatherPhone,MotherPhone,Email,Address,ProfilePicPath,
      AcademicYearId,ClassId,SectionId,BatchId,RollNo,BloodGroup,Status,AdmissionDate,
      Nationality,MotherTongue,Religion,PlaceOfBirth,AadhaarNo,AlternatePhone,
      PreviousPercentage,PreviousSchool,Board,FatherOccupation,MotherOccupation,
      GuardianName,GuardianOccupation,GuardianPhone,City,State,District,Pincode,PermanentAddress,CreatedBy)
    VALUES(@AdmissionNo,@FullName,@DateOfBirth,@Gender,@FatherName,@MotherName,
      @Phone,@FatherPhone,@MotherPhone,@Email,@Address,@ProfilePicPath,
      @AcademicYearId,@ClassId,@SectionId,@BatchId,@RollNo,@BloodGroup,@Status,@AdmissionDate,
      @Nationality,@MotherTongue,@Religion,@PlaceOfBirth,@AadhaarNo,@AlternatePhone,
      @PreviousPercentage,@PreviousSchool,@Board,@FatherOccupation,@MotherOccupation,
      @GuardianName,@GuardianOccupation,@GuardianPhone,@City,@State,@District,@Pincode,@PermanentAddress,@CreatedBy);
    SET @NewStudentId = SCOPE_IDENTITY();
  END ELSE BEGIN
    UPDATE Students SET
      FullName=@FullName, DateOfBirth=@DateOfBirth, Gender=@Gender,
      FatherName=@FatherName, MotherName=@MotherName, Phone=@Phone,
      FatherPhone=@FatherPhone, MotherPhone=@MotherPhone, Email=@Email,
      Address=@Address, AcademicYearId=@AcademicYearId, ClassId=@ClassId,
      SectionId=@SectionId, BatchId=@BatchId, RollNo=@RollNo, BloodGroup=@BloodGroup,
      Status=@Status, AdmissionDate=ISNULL(@AdmissionDate,AdmissionDate),
      ProfilePicPath=ISNULL(@ProfilePicPath,ProfilePicPath),
      Nationality=@Nationality, MotherTongue=@MotherTongue, Religion=@Religion,
      PlaceOfBirth=@PlaceOfBirth, AadhaarNo=@AadhaarNo, AlternatePhone=@AlternatePhone,
      PreviousPercentage=@PreviousPercentage, PreviousSchool=@PreviousSchool, Board=@Board,
      FatherOccupation=@FatherOccupation, MotherOccupation=@MotherOccupation,
      GuardianName=@GuardianName, GuardianOccupation=@GuardianOccupation, GuardianPhone=@GuardianPhone,
      City=@City, State=@State, District=@District, Pincode=@Pincode, PermanentAddress=@PermanentAddress
    WHERE StudentId=@StudentId;
    SET @NewStudentId=@StudentId;
  END
END
GO
PRINT 'Done.';
GO
