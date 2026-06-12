-- Add DueDate column to FeePayments table if it doesn't exist
USE SchoolManagementDB;
GO

IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FeePayments' AND COLUMN_NAME='DueDate')
BEGIN
    ALTER TABLE FeePayments ADD DueDate DATE NULL;
    PRINT '✅ DueDate column added to FeePayments table';
END
ELSE
BEGIN
    PRINT '⚠️ DueDate column already exists in FeePayments table';
END
GO

-- Update stored procedure to accept and save DueDate
IF OBJECT_ID('sp_SaveFeePayment','P') IS NOT NULL DROP PROC sp_SaveFeePayment; GO
CREATE PROCEDURE sp_SaveFeePayment
  @StudentId INT, @FeeTypeId INT, @Amount DECIMAL(10,2),
  @Discount DECIMAL(10,2)=0, @LateFine DECIMAL(10,2)=0,
  @PaymentDate DATE, @PaymentMode NVARCHAR(30)='Cash',
  @TransactionRef NVARCHAR(100)=NULL, @AcademicYearId INT=NULL,
  @Month NVARCHAR(20)=NULL, @Remarks NVARCHAR(300)=NULL,
  @CollectedBy INT=NULL, @DueDate DATE=NULL, @NewPaymentId INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  DECLARE @RNo NVARCHAR(30), @Counter INT=1, @MaxAttempts INT=100;

  WHILE @Counter <= @MaxAttempts BEGIN
    SET @RNo = 'RCP-'+CAST(YEAR(GETDATE()) AS NVARCHAR)+'-'
      +RIGHT('0000'+CAST(@Counter AS NVARCHAR),4);

    IF NOT EXISTS(SELECT 1 FROM FeePayments WHERE ReceiptNo=@RNo) BREAK;
    SET @Counter = @Counter + 1;
  END

  INSERT INTO FeePayments(ReceiptNo,StudentId,FeeTypeId,Amount,Discount,LateFine,NetAmount,
    PaymentDate,PaymentMode,TransactionRef,AcademicYearId,Month,Remarks,CollectedBy,DueDate)
  VALUES(@RNo,@StudentId,@FeeTypeId,@Amount,@Discount,@LateFine,@Amount+@LateFine,
    @PaymentDate,@PaymentMode,@TransactionRef,@AcademicYearId,@Month,@Remarks,@CollectedBy,@DueDate);
  SET @NewPaymentId=SCOPE_IDENTITY();
END
GO

PRINT '✅ Stored procedure updated with DueDate parameter';
GO
