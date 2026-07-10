-- Query to sum all Additional Charges from FeePayments table

-- 1. Sum Additional Charges (Total)
SELECT SUM(CAST(SUBSTRING(Remarks, CHARINDEX('₹', Remarks) + 1, LEN(Remarks)) AS DECIMAL(10, 2))) as TotalAdditionalCharges
FROM FeePayments
WHERE Remarks LIKE '%Additional Charges:%'
AND IsDeleted = 0;

-- 2. Additional Charges by Student
SELECT
    s.StudentId,
    s.FullName,
    s.AdmissionNo,
    c.ClassName,
    SUM(CAST(SUBSTRING(fp.Remarks, CHARINDEX('₹', fp.Remarks) + 1, LEN(fp.Remarks)) AS DECIMAL(10, 2))) as AdditionalCharges
FROM FeePayments fp
INNER JOIN Students s ON s.StudentId = fp.StudentId
INNER JOIN Classes c ON c.ClassId = s.ClassId
WHERE fp.Remarks LIKE '%Additional Charges:%'
AND fp.IsDeleted = 0
GROUP BY s.StudentId, s.FullName, s.AdmissionNo, c.ClassName
ORDER BY AdditionalCharges DESC;

-- 3. Additional Charges by Class
SELECT
    c.ClassName,
    COUNT(DISTINCT s.StudentId) as StudentsWithCharges,
    SUM(CAST(SUBSTRING(fp.Remarks, CHARINDEX('₹', fp.Remarks) + 1, LEN(fp.Remarks)) AS DECIMAL(10, 2))) as TotalCharges
FROM FeePayments fp
INNER JOIN Students s ON s.StudentId = fp.StudentId
INNER JOIN Classes c ON c.ClassId = s.ClassId
WHERE fp.Remarks LIKE '%Additional Charges:%'
AND fp.IsDeleted = 0
GROUP BY c.ClassName
ORDER BY TotalCharges DESC;

-- 4. Additional Charges by Date Range
SELECT
    CONVERT(DATE, fp.PaymentDate) as PaymentDate,
    COUNT(*) as Transactions,
    SUM(CAST(SUBSTRING(fp.Remarks, CHARINDEX('₹', fp.Remarks) + 1, LEN(fp.Remarks)) AS DECIMAL(10, 2))) as DailyCharges
FROM FeePayments fp
WHERE fp.Remarks LIKE '%Additional Charges:%'
AND fp.IsDeleted = 0
GROUP BY CONVERT(DATE, fp.PaymentDate)
ORDER BY PaymentDate DESC;

-- 5. Additional Charges vs Regular Fees Comparison
SELECT
    (SELECT SUM(Amount) FROM FeePayments WHERE IsDeleted = 0) as TotalFeesCollected,
    (SELECT SUM(CAST(SUBSTRING(Remarks, CHARINDEX('₹', Remarks) + 1, LEN(Remarks)) AS DECIMAL(10, 2)))
     FROM FeePayments WHERE Remarks LIKE '%Additional Charges:%' AND IsDeleted = 0) as AdditionalChargesCollected,
    ROUND(((SELECT SUM(CAST(SUBSTRING(Remarks, CHARINDEX('₹', Remarks) + 1, LEN(Remarks)) AS DECIMAL(10, 2)))
     FROM FeePayments WHERE Remarks LIKE '%Additional Charges:%' AND IsDeleted = 0) * 100 /
     (SELECT SUM(Amount) FROM FeePayments WHERE IsDeleted = 0)), 2) as PercentageOfTotal;
