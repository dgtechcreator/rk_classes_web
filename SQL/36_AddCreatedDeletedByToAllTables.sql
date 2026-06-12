-- ============================================================
-- 36_AddCreatedDeletedByToAllTables.sql
-- Add CreatedBy, CreatedByName, DeletedBy, DeletedByName to all tables
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Add CreatedBy to Classes if not exists ──────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Classes' AND COLUMN_NAME='CreatedBy')
BEGIN
    ALTER TABLE Classes ADD CreatedBy INT REFERENCES Users(UserId);
    PRINT '✅ CreatedBy added to Classes';
END
GO

-- ── Add DeletedBy/DeletedAt to Classes ──────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Classes' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE Classes ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to Classes';
END
GO

-- ── Add CreatedBy to Sections if not exists ────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sections' AND COLUMN_NAME='CreatedBy')
BEGIN
    ALTER TABLE Sections ADD CreatedBy INT REFERENCES Users(UserId);
    PRINT '✅ CreatedBy added to Sections';
END
GO

-- ── Add DeletedBy/DeletedAt to Sections ─────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sections' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE Sections ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to Sections';
END
GO

-- ── Add CreatedBy to Batches if not exists ──────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Batches' AND COLUMN_NAME='CreatedBy')
BEGIN
    ALTER TABLE Batches ADD CreatedBy INT REFERENCES Users(UserId);
    PRINT '✅ CreatedBy added to Batches';
END
GO

-- ── Add DeletedBy/DeletedAt to Batches ──────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Batches' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE Batches ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to Batches';
END
GO

-- ── Add DeletedBy/DeletedAt to Students ─────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Students' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE Students ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to Students';
END
GO

-- ── Add CreatedBy to Subjects if not exists ────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Subjects' AND COLUMN_NAME='CreatedBy')
BEGIN
    ALTER TABLE Subjects ADD CreatedBy INT REFERENCES Users(UserId);
    PRINT '✅ CreatedBy added to Subjects';
END
GO

-- ── Add DeletedBy/DeletedAt to Subjects ─────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Subjects' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE Subjects ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to Subjects';
END
GO

-- ── Add CreatedBy to Expenses if not exists ────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Expenses' AND COLUMN_NAME='CreatedBy')
BEGIN
    ALTER TABLE Expenses ADD CreatedBy INT REFERENCES Users(UserId);
    PRINT '✅ CreatedBy added to Expenses';
END
GO

-- ── Add DeletedBy/DeletedAt to Expenses ─────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Expenses' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE Expenses ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to Expenses';
END
GO

-- ── Add DeletedBy/DeletedAt to Faculty ──────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Faculty' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE Faculty ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to Faculty';
END
GO

-- ── Add CreatedBy to FeeTypes if not exists ────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FeeTypes' AND COLUMN_NAME='CreatedBy')
BEGIN
    ALTER TABLE FeeTypes ADD CreatedBy INT REFERENCES Users(UserId);
    PRINT '✅ CreatedBy added to FeeTypes';
END
GO

-- ── Add DeletedBy/DeletedAt to FeeTypes ─────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FeeTypes' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE FeeTypes ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to FeeTypes';
END
GO

-- ── Add CreatedBy to FeeStructure if not exists ──────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FeeStructure' AND COLUMN_NAME='CreatedBy')
BEGIN
    ALTER TABLE FeeStructure ADD CreatedBy INT REFERENCES Users(UserId);
    PRINT '✅ CreatedBy added to FeeStructure';
END
GO

-- ── Add DeletedBy/DeletedAt to FeeStructure ────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FeeStructure' AND COLUMN_NAME='DeletedBy')
BEGIN
    ALTER TABLE FeeStructure ADD DeletedBy INT REFERENCES Users(UserId), DeletedAt DATETIME;
    PRINT '✅ DeletedBy and DeletedAt added to FeeStructure';
END
GO

PRINT '✅ All tables updated with CreatedBy and DeletedBy columns.';
GO
