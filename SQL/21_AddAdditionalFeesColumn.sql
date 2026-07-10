-- Add AdditionalFees column to StudentFees table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'StudentFees' AND COLUMN_NAME = 'AdditionalFees')
BEGIN
    ALTER TABLE StudentFees ADD AdditionalFees DECIMAL(10, 2) DEFAULT 0;
    PRINT 'AdditionalFees column added to StudentFees table';
END
ELSE
BEGIN
    PRINT 'AdditionalFees column already exists in StudentFees table';
END
