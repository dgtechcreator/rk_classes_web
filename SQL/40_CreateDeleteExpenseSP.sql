-- Create sp_DeleteExpense stored procedure
IF OBJECT_ID('sp_DeleteExpense','P') IS NOT NULL DROP PROC sp_DeleteExpense;
GO
CREATE PROCEDURE sp_DeleteExpense
  @ExpenseId INT
AS BEGIN
  DELETE FROM Expenses WHERE ExpenseId=@ExpenseId;
END
GO

PRINT '✅ sp_DeleteExpense created.';
