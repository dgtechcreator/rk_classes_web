-- 59: the "22k" (Raunak 331) and "15k" (Rudra 197) the admin gave match the fee AFTER the discount already saved on
-- their payments (25000 - 3000, 17000 - 2000). Entering them as custom fees counted the discount twice.
-- Put both back to the normal fee-head amount; the discount stays on the payments.
UPDATE sf
SET Amount = fs.Amount, IsCustomAmount = 0
FROM StudentFees sf
JOIN FeeStructure fs ON fs.StructureId = sf.StructureId
WHERE sf.IsActive = 1 AND sf.IsCustomAmount = 1 AND sf.StudentId IN (331, 197);
PRINT 'rows reset: ' + CAST(@@ROWCOUNT AS VARCHAR);
