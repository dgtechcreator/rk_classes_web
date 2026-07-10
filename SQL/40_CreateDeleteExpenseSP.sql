-- Add DeletedBy and DeletedAt columns to Expenses if they don't exist
IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Expenses' AND COLUMN_NAME='DeletedBy')
  ALTER TABLE Expenses ADD DeletedBy INT REFERENCES Users(UserId);
GO

IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Expenses' AND COLUMN_NAME='DeletedAt')
  ALTER TABLE Expenses ADD DeletedAt DATETIME;
GO

-- Create sp_DeleteExpense stored procedure (soft delete)
IF OBJECT_ID('sp_DeleteExpense','P') IS NOT NULL DROP PROC sp_DeleteExpense;
GO
CREATE PROCEDURE sp_DeleteExpense
  @ExpenseId INT,
  @DeletedBy INT
AS BEGIN
  UPDATE Expenses SET DeletedBy=@DeletedBy, DeletedAt=GETDATE() WHERE ExpenseId=@ExpenseId;
END
GO

-- Update sp_GetExpenses to exclude deleted records
IF OBJECT_ID('sp_GetExpenses','P') IS NOT NULL DROP PROC sp_GetExpenses;
GO
CREATE PROCEDURE sp_GetExpenses
  @PageNo INT=1, @PageSize INT=15, @Search NVARCHAR(200)=NULL,
  @CategoryId INT=NULL, @Month INT=NULL, @Year INT=NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  SELECT @TotalCount=COUNT(*) FROM Expenses e
  WHERE (DeletedAt IS NULL)
    AND (@Search IS NULL OR e.Title LIKE '%'+@Search+'%' OR e.VendorName LIKE '%'+@Search+'%' OR e.ExpenseNo LIKE '%'+@Search+'%')
    AND (@CategoryId IS NULL OR e.CategoryId=@CategoryId)
    AND (@Month IS NULL OR MONTH(e.ExpenseDate)=@Month)
    AND (@Year  IS NULL OR YEAR(e.ExpenseDate)=@Year);
  SELECT e.*,ec.CategoryName,u.FullName AS EnteredByName
  FROM Expenses e
  LEFT JOIN ExpenseCategories ec ON ec.CategoryId=e.CategoryId
  LEFT JOIN Users u ON u.UserId=e.EnteredBy
  WHERE (DeletedAt IS NULL)
    AND (@Search IS NULL OR e.Title LIKE '%'+@Search+'%' OR e.VendorName LIKE '%'+@Search+'%' OR e.ExpenseNo LIKE '%'+@Search+'%')
    AND (@CategoryId IS NULL OR e.CategoryId=@CategoryId)
    AND (@Month IS NULL OR MONTH(e.ExpenseDate)=@Month)
    AND (@Year  IS NULL OR YEAR(e.ExpenseDate)=@Year)
  ORDER BY e.ExpenseDate DESC
  OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

PRINT '✅ Soft delete setup complete.';
