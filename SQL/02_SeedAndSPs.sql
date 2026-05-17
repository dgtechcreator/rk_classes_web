-- ============================================================
-- SchoolManagementDB  |  02_SeedAndSPs.sql
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Seed ──────────────────────────────────────────────────────
IF NOT EXISTS(SELECT 1 FROM Roles) INSERT INTO Roles(RoleName) VALUES('Admin'),('Teacher'),('Accountant');
GO
IF NOT EXISTS(SELECT 1 FROM Users)
INSERT INTO Users(FullName,Username,PasswordHash,RoleId,Email,Phone) VALUES
('Administrator','admin','Admin@123',1,'admin@school.com','9900000001'),
('Head Teacher','teacher','Admin@123',2,'teacher@school.com','9900000002'),
('Accountant','accounts','Admin@123',3,'accounts@school.com','9900000003');
GO
IF NOT EXISTS(SELECT 1 FROM AcademicYears)
INSERT INTO AcademicYears(YearName,IsCurrent) VALUES('2024-2025',0),('2025-2026',1);
GO
IF NOT EXISTS(SELECT 1 FROM Classes)
INSERT INTO Classes(ClassName,OrderNo) VALUES('Class 1',1),('Class 2',2),('Class 3',3);
GO
IF NOT EXISTS(SELECT 1 FROM Sections)
INSERT INTO Sections(SectionName) VALUES('Hindi'),('English');
GO
IF NOT EXISTS(SELECT 1 FROM Batches)
INSERT INTO Batches(BatchName) VALUES('Morning'),('Afternoon'),('Evening');
GO
IF NOT EXISTS(SELECT 1 FROM FeeTypes)
INSERT INTO FeeTypes(TypeName) VALUES('Tuition Fee'),('Transport Fee'),('Library Fee'),('Exam Fee'),('Activity Fee'),('Annual Fee');
GO
IF NOT EXISTS(SELECT 1 FROM ExpenseCategories)
INSERT INTO ExpenseCategories(CategoryName) VALUES('Salary'),('Electricity'),('Stationery'),('Maintenance'),('Transport'),('Events'),('Miscellaneous');
GO
IF NOT EXISTS(SELECT 1 FROM Subjects)
INSERT INTO Subjects(SubjectName,SubjectCode,ClassId,MaxMarks,PassMarks) VALUES
('Hindi','HIN',1,100,35),('English','ENG',1,100,35),('Mathematics','MAT',1,100,35),('EVS','EVS',1,100,35),
('Hindi','HIN',2,100,35),('English','ENG',2,100,35),('Mathematics','MAT',2,100,35),('EVS','EVS',2,100,35),
('Hindi','HIN',3,100,35),('English','ENG',3,100,35),('Mathematics','MAT',3,100,35),('Science','SCI',3,100,35),('Social Studies','SST',3,100,35);
GO
IF NOT EXISTS(SELECT 1 FROM Students) BEGIN
  DECLARE @yr INT = (SELECT YearId FROM AcademicYears WHERE IsCurrent=1);
  INSERT INTO Students(AdmissionNo,FullName,DateOfBirth,Gender,FatherName,MotherName,Phone,AcademicYearId,ClassId,SectionId,BatchId,RollNo,BloodGroup,CreatedBy) VALUES
  ('ADM-2026-001','Aarav Sharma','2016-05-14','Male','Rajesh Sharma','Priya Sharma','9811001001',@yr,1,2,1,'01','A+',1),
  ('ADM-2026-002','Ananya Singh','2016-08-22','Female','Vijay Singh','Kavita Singh','9811001002',@yr,1,1,2,'02','B+',1),
  ('ADM-2026-003','Rohan Patel','2015-11-03','Male','Suresh Patel','Meena Patel','9811001003',@yr,2,2,1,'01','O+',1),
  ('ADM-2026-004','Priya Verma','2015-03-19','Female','Anil Verma','Sita Verma','9811001004',@yr,2,1,3,'02','AB+',1),
  ('ADM-2026-005','Arjun Kumar','2014-07-30','Male','Manoj Kumar','Sunita Kumar','9811001005',@yr,3,2,2,'01','B-',1);
END
GO
IF NOT EXISTS(SELECT 1 FROM Exams) BEGIN
  DECLARE @yr2 INT = (SELECT YearId FROM AcademicYears WHERE IsCurrent=1);
  INSERT INTO Exams(ExamName,AcademicYearId,ClassId) VALUES
  ('Unit Test 1',@yr2,1),('Unit Test 1',@yr2,2),('Unit Test 1',@yr2,3),
  ('Mid Term',@yr2,1),('Mid Term',@yr2,2),('Mid Term',@yr2,3);
END
GO

-- ── Stored Procedures ─────────────────────────────────────────
IF OBJECT_ID('sp_ValidateUser','P') IS NOT NULL DROP PROC sp_ValidateUser; GO
CREATE PROCEDURE sp_ValidateUser @Username NVARCHAR(100) AS BEGIN
  SELECT u.UserId,u.FullName,u.Username,u.PasswordHash,u.RoleId,r.RoleName,u.Email,u.Phone,u.IsActive
  FROM Users u INNER JOIN Roles r ON r.RoleId=u.RoleId
  WHERE u.Username=@Username AND u.IsActive=1;
END
GO

IF OBJECT_ID('sp_UpdateLastLogin','P') IS NOT NULL DROP PROC sp_UpdateLastLogin; GO
CREATE PROCEDURE sp_UpdateLastLogin @UserId INT AS BEGIN
  UPDATE Users SET LastLogin=GETDATE() WHERE UserId=@UserId;
END
GO

IF OBJECT_ID('sp_GetDashboardStats','P') IS NOT NULL DROP PROC sp_GetDashboardStats; GO
CREATE PROCEDURE sp_GetDashboardStats AS BEGIN
  SELECT
    (SELECT COUNT(*) FROM Students WHERE Status='Active')                                                        AS TotalStudents,
    (SELECT COUNT(*) FROM Students WHERE CAST(CreatedAt AS DATE)=CAST(GETDATE() AS DATE))                       AS NewToday,
    (SELECT COUNT(*) FROM Attendance WHERE AttendanceDate=CAST(GETDATE() AS DATE) AND Status='Present')         AS PresentToday,
    (SELECT COUNT(*) FROM Attendance WHERE AttendanceDate=CAST(GETDATE() AS DATE) AND Status='Absent')          AS AbsentToday,
    (SELECT ISNULL(SUM(NetAmount),0) FROM FeePayments WHERE MONTH(PaymentDate)=MONTH(GETDATE()) AND YEAR(PaymentDate)=YEAR(GETDATE())) AS FeesThisMonth,
    (SELECT ISNULL(SUM(Amount),0)   FROM Expenses     WHERE MONTH(ExpenseDate)=MONTH(GETDATE()) AND YEAR(ExpenseDate)=YEAR(GETDATE())) AS ExpensesThisMonth,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND ClassId=1) AS Class1Count,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND ClassId=2) AS Class2Count,
    (SELECT COUNT(*) FROM Students WHERE Status='Active' AND ClassId=3) AS Class3Count,
    (SELECT COUNT(*) FROM Users WHERE IsActive=1)                       AS TotalStaff;
END
GO

IF OBJECT_ID('sp_GetStudents','P') IS NOT NULL DROP PROC sp_GetStudents; GO
CREATE PROCEDURE sp_GetStudents
  @PageNo INT=1, @PageSize INT=15, @Search NVARCHAR(200)=NULL,
  @ClassId INT=NULL, @SectionId INT=NULL, @BatchId INT=NULL,
  @AcademicYearId INT=NULL, @Status NVARCHAR(20)=NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  SELECT @TotalCount=COUNT(*) FROM Students s
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%' OR s.FatherName LIKE '%'+@Search+'%')
    AND (@ClassId IS NULL OR s.ClassId=@ClassId)
    AND (@SectionId IS NULL OR s.SectionId=@SectionId)
    AND (@BatchId IS NULL OR s.BatchId=@BatchId)
    AND (@AcademicYearId IS NULL OR s.AcademicYearId=@AcademicYearId)
    AND (@Status IS NULL OR s.Status=@Status);
  SELECT s.*,c.ClassName,sec.SectionName,b.BatchName,ay.YearName
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
  LEFT JOIN Batches b ON b.BatchId=s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId=s.AcademicYearId
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%' OR s.FatherName LIKE '%'+@Search+'%')
    AND (@ClassId IS NULL OR s.ClassId=@ClassId)
    AND (@SectionId IS NULL OR s.SectionId=@SectionId)
    AND (@BatchId IS NULL OR s.BatchId=@BatchId)
    AND (@AcademicYearId IS NULL OR s.AcademicYearId=@AcademicYearId)
    AND (@Status IS NULL OR s.Status=@Status)
  ORDER BY s.FullName
  OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

IF OBJECT_ID('sp_GetStudentById','P') IS NOT NULL DROP PROC sp_GetStudentById; GO
CREATE PROCEDURE sp_GetStudentById @StudentId INT AS BEGIN
  SELECT s.*,c.ClassName,sec.SectionName,b.BatchName,ay.YearName
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
  LEFT JOIN Batches b ON b.BatchId=s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId=s.AcademicYearId
  WHERE s.StudentId=@StudentId;
END
GO

IF OBJECT_ID('sp_SaveStudent','P') IS NOT NULL DROP PROC sp_SaveStudent; GO
CREATE PROCEDURE sp_SaveStudent
  @StudentId INT=0, @AdmissionNo NVARCHAR(30)=NULL, @FullName NVARCHAR(150),
  @DateOfBirth DATE=NULL, @Gender NVARCHAR(10)=NULL, @FatherName NVARCHAR(150)=NULL,
  @MotherName NVARCHAR(150)=NULL, @Phone NVARCHAR(20)=NULL, @Email NVARCHAR(150)=NULL,
  @Address NVARCHAR(300)=NULL, @ProfilePicPath NVARCHAR(500)=NULL,
  @AcademicYearId INT=NULL, @ClassId INT=NULL, @SectionId INT=NULL, @BatchId INT=NULL,
  @RollNo NVARCHAR(20)=NULL, @BloodGroup NVARCHAR(5)=NULL, @Status NVARCHAR(20)='Active',
  @CreatedBy INT=NULL, @NewStudentId INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  IF @StudentId=0 BEGIN
    IF @AdmissionNo IS NULL
      SET @AdmissionNo='ADM-'+CAST(YEAR(GETDATE()) AS NVARCHAR)+'-'
        +RIGHT('000'+CAST((SELECT ISNULL(MAX(StudentId),0)+1 FROM Students) AS NVARCHAR),4);
    INSERT INTO Students(AdmissionNo,FullName,DateOfBirth,Gender,FatherName,MotherName,Phone,Email,Address,
      ProfilePicPath,AcademicYearId,ClassId,SectionId,BatchId,RollNo,BloodGroup,Status,CreatedBy)
    VALUES(@AdmissionNo,@FullName,@DateOfBirth,@Gender,@FatherName,@MotherName,@Phone,@Email,@Address,
      @ProfilePicPath,@AcademicYearId,@ClassId,@SectionId,@BatchId,@RollNo,@BloodGroup,@Status,@CreatedBy);
    SET @NewStudentId=SCOPE_IDENTITY();
  END ELSE BEGIN
    UPDATE Students SET FullName=@FullName,DateOfBirth=@DateOfBirth,Gender=@Gender,FatherName=@FatherName,
      MotherName=@MotherName,Phone=@Phone,Email=@Email,Address=@Address,AcademicYearId=@AcademicYearId,
      ClassId=@ClassId,SectionId=@SectionId,BatchId=@BatchId,RollNo=@RollNo,BloodGroup=@BloodGroup,Status=@Status,
      ProfilePicPath=ISNULL(@ProfilePicPath,ProfilePicPath)
    WHERE StudentId=@StudentId;
    SET @NewStudentId=@StudentId;
  END
END
GO

IF OBJECT_ID('sp_GetAttendance','P') IS NOT NULL DROP PROC sp_GetAttendance; GO
CREATE PROCEDURE sp_GetAttendance
  @AttendanceDate DATE, @ClassId INT=NULL, @SectionId INT=NULL, @BatchId INT=NULL
AS BEGIN
  DECLARE @CurrentYearId INT = (SELECT YearId FROM AcademicYears WHERE IsCurrent=1);
  SELECT s.StudentId,s.FullName,s.AdmissionNo,s.RollNo,s.ProfilePicPath,
         c.ClassName,sec.SectionName,b.BatchName,
         ISNULL(a.Status,'Present') AS AttendanceStatus,
         a.AttendanceId,a.Remarks
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
  LEFT JOIN Batches b ON b.BatchId=s.BatchId
  LEFT JOIN Attendance a ON a.StudentId=s.StudentId AND a.AttendanceDate=@AttendanceDate
  WHERE s.Status='Active'
    AND s.AcademicYearId = @CurrentYearId
    AND (@ClassId IS NULL OR s.ClassId=@ClassId)
    AND (@SectionId IS NULL OR s.SectionId=@SectionId)
    AND (@BatchId IS NULL OR s.BatchId=@BatchId)
  ORDER BY s.RollNo, s.FullName;
END
GO

IF OBJECT_ID('sp_SaveAttendance','P') IS NOT NULL DROP PROC sp_SaveAttendance; GO
CREATE PROCEDURE sp_SaveAttendance
  @StudentId INT, @AttendanceDate DATE, @Status NVARCHAR(10),
  @ClassId INT=NULL, @SectionId INT=NULL, @BatchId INT=NULL,
  @Remarks NVARCHAR(200)=NULL, @MarkedBy INT=NULL
AS BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM Attendance WHERE StudentId=@StudentId AND AttendanceDate=@AttendanceDate)
    UPDATE Attendance SET Status=@Status,Remarks=@Remarks,MarkedBy=@MarkedBy,MarkedAt=GETDATE()
    WHERE StudentId=@StudentId AND AttendanceDate=@AttendanceDate;
  ELSE
    INSERT INTO Attendance(StudentId,AttendanceDate,Status,ClassId,SectionId,BatchId,Remarks,MarkedBy)
    VALUES(@StudentId,@AttendanceDate,@Status,@ClassId,@SectionId,@BatchId,@Remarks,@MarkedBy);
END
GO

IF OBJECT_ID('sp_GetAttendanceReport','P') IS NOT NULL DROP PROC sp_GetAttendanceReport; GO
CREATE PROCEDURE sp_GetAttendanceReport @ClassId INT=NULL, @Month INT=NULL, @Year INT=NULL AS BEGIN
  SELECT s.StudentId,s.FullName,s.AdmissionNo,c.ClassName,
    COUNT(CASE WHEN a.Status='Present' THEN 1 END) AS PresentDays,
    COUNT(CASE WHEN a.Status='Absent'  THEN 1 END) AS AbsentDays,
    COUNT(CASE WHEN a.Status='Late'    THEN 1 END) AS LateDays,
    COUNT(a.AttendanceId) AS TotalDays,
    CAST(COUNT(CASE WHEN a.Status='Present' THEN 1 END)*100.0/NULLIF(COUNT(a.AttendanceId),0) AS DECIMAL(5,1)) AS AttendancePct
  FROM Students s
  LEFT JOIN Attendance a ON a.StudentId=s.StudentId
    AND (@Month IS NULL OR MONTH(a.AttendanceDate)=@Month)
    AND (@Year  IS NULL OR YEAR(a.AttendanceDate)=@Year)
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  WHERE s.Status='Active' AND (@ClassId IS NULL OR s.ClassId=@ClassId)
  GROUP BY s.StudentId,s.FullName,s.AdmissionNo,c.ClassName
  ORDER BY s.FullName;
END
GO

IF OBJECT_ID('sp_GetMarks','P') IS NOT NULL DROP PROC sp_GetMarks; GO
CREATE PROCEDURE sp_GetMarks @ExamId INT, @ClassId INT=NULL AS BEGIN
  SELECT tm.*,s.FullName,s.AdmissionNo,s.RollNo,
    sub.SubjectName,sub.MaxMarks AS SubjectMaxMarks,sub.PassMarks,
    e.ExamName,c.ClassName
  FROM TestMarks tm
  INNER JOIN Students s ON s.StudentId=tm.StudentId
  INNER JOIN Subjects sub ON sub.SubjectId=tm.SubjectId
  INNER JOIN Exams e ON e.ExamId=tm.ExamId
  INNER JOIN Classes c ON c.ClassId=s.ClassId
  WHERE tm.ExamId=@ExamId AND (@ClassId IS NULL OR s.ClassId=@ClassId)
  ORDER BY s.RollNo,sub.SubjectName;
END
GO

IF OBJECT_ID('sp_SaveMark','P') IS NOT NULL DROP PROC sp_SaveMark; GO
CREATE PROCEDURE sp_SaveMark
  @StudentId INT, @ExamId INT, @SubjectId INT,
  @MarksObtained DECIMAL(6,2), @MaxMarks INT=100, @EnteredBy INT=NULL
AS BEGIN
  SET NOCOUNT ON;
  DECLARE @Grade NVARCHAR(5) = CASE
    WHEN @MarksObtained>=90 THEN 'A+' WHEN @MarksObtained>=80 THEN 'A'
    WHEN @MarksObtained>=70 THEN 'B+' WHEN @MarksObtained>=60 THEN 'B'
    WHEN @MarksObtained>=50 THEN 'C'  WHEN @MarksObtained>=35 THEN 'D' ELSE 'F' END;
  IF EXISTS(SELECT 1 FROM TestMarks WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId)
    UPDATE TestMarks SET MarksObtained=@MarksObtained,MaxMarks=@MaxMarks,Grade=@Grade,EnteredBy=@EnteredBy,EnteredAt=GETDATE()
    WHERE StudentId=@StudentId AND ExamId=@ExamId AND SubjectId=@SubjectId;
  ELSE
    INSERT INTO TestMarks(StudentId,ExamId,SubjectId,MarksObtained,MaxMarks,Grade,EnteredBy)
    VALUES(@StudentId,@ExamId,@SubjectId,@MarksObtained,@MaxMarks,@Grade,@EnteredBy);
END
GO

IF OBJECT_ID('sp_GetFeePayments','P') IS NOT NULL DROP PROC sp_GetFeePayments; GO
CREATE PROCEDURE sp_GetFeePayments
  @PageNo INT=1, @PageSize INT=15, @Search NVARCHAR(200)=NULL,
  @Month NVARCHAR(20)=NULL, @FeeTypeId INT=NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  SELECT @TotalCount=COUNT(*) FROM FeePayments fp
  LEFT JOIN Students s ON s.StudentId=fp.StudentId
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR fp.ReceiptNo LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%')
    AND (@Month IS NULL OR fp.Month=@Month) AND (@FeeTypeId IS NULL OR fp.FeeTypeId=@FeeTypeId);
  SELECT fp.*,s.FullName AS StudentName,s.AdmissionNo,c.ClassName,sec.SectionName,
    ft.TypeName AS FeeTypeName,u.FullName AS CollectorName
  FROM FeePayments fp
  LEFT JOIN Students s ON s.StudentId=fp.StudentId
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
  LEFT JOIN FeeTypes ft ON ft.FeeTypeId=fp.FeeTypeId
  LEFT JOIN Users u ON u.UserId=fp.CollectedBy
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR fp.ReceiptNo LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%')
    AND (@Month IS NULL OR fp.Month=@Month) AND (@FeeTypeId IS NULL OR fp.FeeTypeId=@FeeTypeId)
  ORDER BY fp.CreatedAt DESC
  OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

IF OBJECT_ID('sp_SaveFeePayment','P') IS NOT NULL DROP PROC sp_SaveFeePayment; GO
CREATE PROCEDURE sp_SaveFeePayment
  @StudentId INT, @FeeTypeId INT, @Amount DECIMAL(10,2),
  @Discount DECIMAL(10,2)=0, @LateFine DECIMAL(10,2)=0,
  @PaymentDate DATE, @PaymentMode NVARCHAR(30)='Cash',
  @TransactionRef NVARCHAR(100)=NULL, @AcademicYearId INT=NULL,
  @Month NVARCHAR(20)=NULL, @Remarks NVARCHAR(300)=NULL,
  @CollectedBy INT=NULL, @NewPaymentId INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  DECLARE @RNo NVARCHAR(30)='RCP-'+CAST(YEAR(GETDATE()) AS NVARCHAR)+'-'
    +RIGHT('0000'+CAST((SELECT ISNULL(MAX(PaymentId),0)+1 FROM FeePayments) AS NVARCHAR),4);
  INSERT INTO FeePayments(ReceiptNo,StudentId,FeeTypeId,Amount,Discount,LateFine,NetAmount,
    PaymentDate,PaymentMode,TransactionRef,AcademicYearId,Month,Remarks,CollectedBy)
  VALUES(@RNo,@StudentId,@FeeTypeId,@Amount,@Discount,@LateFine,@Amount-@Discount+@LateFine,
    @PaymentDate,@PaymentMode,@TransactionRef,@AcademicYearId,@Month,@Remarks,@CollectedBy);
  SET @NewPaymentId=SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('sp_GetExpenses','P') IS NOT NULL DROP PROC sp_GetExpenses; GO
CREATE PROCEDURE sp_GetExpenses
  @PageNo INT=1, @PageSize INT=15, @Search NVARCHAR(200)=NULL,
  @CategoryId INT=NULL, @Month INT=NULL, @Year INT=NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  SELECT @TotalCount=COUNT(*) FROM Expenses e
  WHERE (@Search IS NULL OR e.Title LIKE '%'+@Search+'%' OR e.VendorName LIKE '%'+@Search+'%' OR e.ExpenseNo LIKE '%'+@Search+'%')
    AND (@CategoryId IS NULL OR e.CategoryId=@CategoryId)
    AND (@Month IS NULL OR MONTH(e.ExpenseDate)=@Month)
    AND (@Year  IS NULL OR YEAR(e.ExpenseDate)=@Year);
  SELECT e.*,ec.CategoryName,u.FullName AS EnteredByName
  FROM Expenses e
  LEFT JOIN ExpenseCategories ec ON ec.CategoryId=e.CategoryId
  LEFT JOIN Users u ON u.UserId=e.EnteredBy
  WHERE (@Search IS NULL OR e.Title LIKE '%'+@Search+'%' OR e.VendorName LIKE '%'+@Search+'%' OR e.ExpenseNo LIKE '%'+@Search+'%')
    AND (@CategoryId IS NULL OR e.CategoryId=@CategoryId)
    AND (@Month IS NULL OR MONTH(e.ExpenseDate)=@Month)
    AND (@Year  IS NULL OR YEAR(e.ExpenseDate)=@Year)
  ORDER BY e.ExpenseDate DESC
  OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

IF OBJECT_ID('sp_SaveExpense','P') IS NOT NULL DROP PROC sp_SaveExpense; GO
CREATE PROCEDURE sp_SaveExpense
  @ExpenseId INT=0, @CategoryId INT=NULL, @Title NVARCHAR(200),
  @Description NVARCHAR(MAX)=NULL, @Amount DECIMAL(10,2),
  @ExpenseDate DATE, @PaymentMode NVARCHAR(30)='Cash',
  @BillNo NVARCHAR(50)=NULL, @VendorName NVARCHAR(150)=NULL,
  @EnteredBy INT=NULL, @NewExpenseId INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  IF @ExpenseId=0 BEGIN
    DECLARE @ENo NVARCHAR(30)='EXP-'+CAST(YEAR(GETDATE()) AS NVARCHAR)+'-'
      +RIGHT('0000'+CAST((SELECT ISNULL(MAX(ExpenseId),0)+1 FROM Expenses) AS NVARCHAR),4);
    INSERT INTO Expenses(ExpenseNo,CategoryId,Title,Description,Amount,ExpenseDate,PaymentMode,BillNo,VendorName,EnteredBy)
    VALUES(@ENo,@CategoryId,@Title,@Description,@Amount,@ExpenseDate,@PaymentMode,@BillNo,@VendorName,@EnteredBy);
    SET @NewExpenseId=SCOPE_IDENTITY();
  END ELSE BEGIN
    UPDATE Expenses SET CategoryId=@CategoryId,Title=@Title,Description=@Description,
      Amount=@Amount,ExpenseDate=@ExpenseDate,PaymentMode=@PaymentMode,BillNo=@BillNo,VendorName=@VendorName
    WHERE ExpenseId=@ExpenseId;
    SET @NewExpenseId=@ExpenseId;
  END
END
GO

PRINT '✅ Seed data and stored procedures created.';
PRINT 'Login: admin / Admin@123';
GO
