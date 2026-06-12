-- ============================================================
-- 28: Expense Categories master
-- Run once in SSMS
-- ============================================================

-- Table (safe if already exists)
IF OBJECT_ID('ExpenseCategories','U') IS NULL
CREATE TABLE ExpenseCategories (
    CategoryId   INT           PRIMARY KEY IDENTITY(1,1),
    CategoryName NVARCHAR(100) NOT NULL,
    IsActive     BIT           NOT NULL DEFAULT 1,
    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- Default seed data (only if table is empty)
IF NOT EXISTS (SELECT 1 FROM ExpenseCategories)
INSERT INTO ExpenseCategories(CategoryName) VALUES
('Electricity'),('Rent'),('Salaries'),('Stationery'),('Maintenance'),('Transport'),('Others');
GO

-- ── sp_GetExpenseCategories ──────────────────────────────────
IF OBJECT_ID('sp_GetExpenseCategories','P') IS NOT NULL DROP PROC sp_GetExpenseCategories;
GO
CREATE PROCEDURE sp_GetExpenseCategories AS
BEGIN
    SET NOCOUNT ON;
    SELECT ec.*,
           (SELECT COUNT(*) FROM Expenses e WHERE e.CategoryId = ec.CategoryId) AS UsageCount
    FROM ExpenseCategories ec
    ORDER BY ec.CategoryName;
END
GO

-- ── sp_SaveExpenseCategory ───────────────────────────────────
IF OBJECT_ID('sp_SaveExpenseCategory','P') IS NOT NULL DROP PROC sp_SaveExpenseCategory;
GO
CREATE PROCEDURE sp_SaveExpenseCategory
    @CategoryId   INT           = 0,
    @CategoryName NVARCHAR(100),
    @IsActive     BIT           = 1,
    @NewId        INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @CategoryId = 0 BEGIN
        INSERT INTO ExpenseCategories(CategoryName, IsActive)
        VALUES(@CategoryName, @IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE ExpenseCategories
        SET CategoryName = @CategoryName, IsActive = @IsActive
        WHERE CategoryId = @CategoryId;
        SET @NewId = @CategoryId;
    END
END
GO

-- ── sp_DeleteExpenseCategory ─────────────────────────────────
IF OBJECT_ID('sp_DeleteExpenseCategory','P') IS NOT NULL DROP PROC sp_DeleteExpenseCategory;
GO
CREATE PROCEDURE sp_DeleteExpenseCategory @CategoryId INT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Expenses WHERE CategoryId = @CategoryId)
        RAISERROR('Cannot delete: this category has existing expense records.',16,1);
    ELSE
        DELETE FROM ExpenseCategories WHERE CategoryId = @CategoryId;
END
GO
