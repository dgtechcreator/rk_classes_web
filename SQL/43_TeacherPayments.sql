-- Teacher Payments Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'TeacherPayments')
BEGIN
    CREATE TABLE TeacherPayments (
        TeacherPaymentId INT PRIMARY KEY IDENTITY(1,1),
        FacultyId INT NOT NULL,
        PaymentType NVARCHAR(50) NOT NULL, -- 'Hourly', 'Topic', 'Fixed'
        Rate DECIMAL(10, 2) NOT NULL,
        Quantity DECIMAL(10, 2) NULL, -- Hours or Topics (null if Fixed)
        TotalAmount DECIMAL(10, 2) NOT NULL,
        PaymentMonth INT NOT NULL, -- 1-12
        PaymentYear INT NOT NULL,
        IsPaid BIT DEFAULT 0,
        PaymentDate DATETIME NULL,
        PaymentMode NVARCHAR(50) NULL,
        TransactionRef NVARCHAR(100) NULL,
        ReceiptNo NVARCHAR(50) NULL,
        Remarks NVARCHAR(500) NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CreatedBy INT,
        UpdatedAt DATETIME NULL,
        UpdatedBy INT NULL,
        DeletedAt DATETIME NULL,
        DeletedBy INT NULL,
        IsDeleted BIT DEFAULT 0,
        FOREIGN KEY (FacultyId) REFERENCES Faculty(FacultyId)
    );

    CREATE INDEX IX_TeacherPayments_FacultyId ON TeacherPayments(FacultyId);
    CREATE INDEX IX_TeacherPayments_IsPaid ON TeacherPayments(IsPaid);
    CREATE INDEX IX_TeacherPayments_Month_Year ON TeacherPayments(PaymentMonth, PaymentYear);
END
GO
