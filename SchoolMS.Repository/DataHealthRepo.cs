using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// Read-only. Finds every active student (current year) whose fee or details are missing, inconsistent or suspicious.
/// It never changes data and never fills in a guess - a missing value is reported as missing.
public class DataHealthRepo(CommonConnectivity db)
{
    const string Sql = @"
DECLARE @Yr INT = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC);

;WITH st AS (
    SELECT s.StudentId, s.AdmissionNo, s.FullName, s.AcademicYearId, s.ClassId, s.SectionId, s.BatchId,
           s.Phone, s.FatherPhone, s.MotherPhone, ISNULL(s.AgreedDiscount, 0) AS AgreedDiscount,
           LTRIM(RTRIM(ISNULL(c.ClassName,'')))   AS ClassName,
           LTRIM(RTRIM(ISNULL(sec.SectionName,''))) AS SectionName,
           LTRIM(RTRIM(ISNULL(b.BatchName,'')))   AS BatchName
    FROM Students s
    LEFT JOIN Classes  c   ON c.ClassId = s.ClassId
    LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
    LEFT JOIN Batches  b   ON b.BatchId = s.BatchId
    WHERE s.Status = 'Active' AND s.AcademicYearId = @Yr
)
-- result 1: students with their numbers
SELECT st.StudentId, st.AdmissionNo, st.FullName, st.ClassName, st.SectionName, st.BatchName,
       ISNULL((SELECT SUM(sf.Amount) FROM StudentFees sf WHERE sf.StudentId = st.StudentId AND sf.IsActive = 1), 0) AS Fee,
       ISNULL((SELECT SUM(fp.NetAmount) FROM FeePayments fp WHERE fp.StudentId = st.StudentId AND fp.IsDeleted = 0), 0) AS Paid,
       ISNULL((SELECT SUM(fp.Discount)  FROM FeePayments fp WHERE fp.StudentId = st.StudentId AND fp.IsDeleted = 0), 0) + ISNULL(st.AgreedDiscount, 0) AS Discount
FROM st ORDER BY st.FullName;

-- result 2: payment remarks that carry additional charges
SELECT fp.StudentId, fp.Remarks
FROM FeePayments fp
JOIN Students s ON s.StudentId = fp.StudentId AND s.Status = 'Active' AND s.AcademicYearId = @Yr
WHERE fp.IsDeleted = 0 AND fp.Remarks LIKE '%Additional Charges:%';

-- result 3: issues found in SQL
;WITH st AS (
    SELECT s.StudentId, s.AdmissionNo, s.FullName, s.AcademicYearId, s.ClassId, s.SectionId, s.BatchId,
           s.Phone, s.FatherPhone, s.MotherPhone,
           LTRIM(RTRIM(ISNULL(c.ClassName,'')))   AS ClassName,
           LTRIM(RTRIM(ISNULL(sec.SectionName,''))) AS SectionName,
           LTRIM(RTRIM(ISNULL(b.BatchName,'')))   AS BatchName
    FROM Students s
    LEFT JOIN Classes  c   ON c.ClassId = s.ClassId
    LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
    LEFT JOIN Batches  b   ON b.BatchId = s.BatchId
    WHERE s.Status = 'Active' AND s.AcademicYearId = @Yr
), act AS (   -- active fee rows with the head they belong to
    SELECT sf.StudentId, sf.StudentFeeId, sf.StructureId, sf.Amount, sf.IsCustomAmount,
           fs.Amount AS HeadAmount, fs.IsActive AS HeadActive, fs.AcademicYearId AS HeadYear, fs.ClassId AS HeadClass, fs.SectionId AS HeadSection,
           LTRIM(RTRIM(ISNULL(hc.ClassName,''))) AS HeadClassName, LTRIM(RTRIM(ISNULL(hs.SectionName,''))) AS HeadSectionName,
           ISNULL(ft.TypeName,'Fee') AS FeeType
    FROM StudentFees sf
    JOIN FeeStructure fs ON fs.StructureId = sf.StructureId
    LEFT JOIN Classes hc ON hc.ClassId = fs.ClassId
    LEFT JOIN Sections hs ON hs.SectionId = fs.SectionId
    LEFT JOIN FeeTypes ft ON ft.FeeTypeId = fs.FeeTypeId
    WHERE sf.IsActive = 1
), pay AS (
    SELECT fp.StudentId, SUM(fp.NetAmount) AS Paid, SUM(fp.Discount) + MAX(ISNULL(s2.AgreedDiscount, 0)) AS Disc
    FROM FeePayments fp JOIN Students s2 ON s2.StudentId = fp.StudentId
    WHERE fp.IsDeleted = 0 GROUP BY fp.StudentId
)
SELECT StudentId, AdmissionNo, FullName, ClassName, SectionName, BatchName, Code, [Group], Severity, Title, Detail, [Action], CanApplyFee
FROM (
    -- student details
    SELECT st.*, 'NO_CLASS' AS Code, 'Student details' AS [Group], 'High' AS Severity, 'Class not set' AS Title,
           'This student has no class.' AS Detail, 'Open the student and choose the class.' AS [Action], 0 AS CanApplyFee
    FROM st WHERE st.ClassId IS NULL
    UNION ALL
    SELECT st.*, 'NO_SECTION', 'Student details', 'High', 'Medium (section) not set',
           'This student has no medium (English / Hindi ...), so the right fee cannot be found.', 'Open the student and choose the medium.', 0
    FROM st WHERE st.SectionId IS NULL
    UNION ALL
    SELECT st.*, 'NO_BATCH', 'Student details', 'Medium', 'Batch not set',
           'This student is in no batch, so he is missing from batch-wise attendance and reports.', 'Open the student and choose the batch.', 0
    FROM st WHERE st.BatchId IS NULL
    UNION ALL
    SELECT st.*, 'NO_PHONE', 'Student details', 'Low', 'No contact number',
           'Student, father and mother numbers are all missing or shorter than 10 digits.', 'Open the student and add a number (needed for calls / WhatsApp).', 0
    FROM st
    WHERE LEN(REPLACE(REPLACE(ISNULL(st.Phone,''),' ',''),'+','')) < 10
      AND LEN(REPLACE(REPLACE(ISNULL(st.FatherPhone,''),' ',''),'+','')) < 10
      AND LEN(REPLACE(REPLACE(ISNULL(st.MotherPhone,''),' ',''),'+','')) < 10

    -- fees: nothing applied
    UNION ALL
    SELECT st.*, 'NO_FEE_HEAD', 'Fees', 'High', 'Fee not set - no fee head for this class / medium',
           'There is no active fee head for ' + st.ClassName + ' / ' + st.SectionName + ', so this student has no fee. '
           + CASE WHEN ISNULL(p.Paid,0) > 0 OR ISNULL(p.Disc,0) > 0
                  THEN 'Already collected Rs ' + CONVERT(VARCHAR(20), CAST(ISNULL(p.Paid,0) AS DECIMAL(12,0))) + ', discount Rs ' + CONVERT(VARCHAR(20), CAST(ISNULL(p.Disc,0) AS DECIMAL(12,0))) + '.'
                  ELSE 'Nothing collected yet.' END,
           'Fee Setup > add the fee head for ' + st.ClassName + ' / ' + st.SectionName + ' - it is applied to these students automatically.', 0
    FROM st LEFT JOIN pay p ON p.StudentId = st.StudentId
    WHERE st.ClassId IS NOT NULL AND st.SectionId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM act a WHERE a.StudentId = st.StudentId)
      AND NOT EXISTS (SELECT 1 FROM FeeStructure f WHERE f.IsActive = 1 AND f.AcademicYearId = st.AcademicYearId AND f.ClassId = st.ClassId AND f.SectionId = st.SectionId)
    UNION ALL
    SELECT st.*, 'FEE_NOT_APPLIED', 'Fees', 'High', 'Fee head exists but is not applied to this student',
           'Fee head(s) exist for ' + st.ClassName + ' / ' + st.SectionName + ' but this student has no fee row, so his fee shows as not set.',
           'Press Apply fee (adds the fee heads of his own class / medium).', 1
    FROM st
    WHERE st.ClassId IS NOT NULL AND st.SectionId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM act a WHERE a.StudentId = st.StudentId)
      AND EXISTS (SELECT 1 FROM FeeStructure f WHERE f.IsActive = 1 AND f.AcademicYearId = st.AcademicYearId AND f.ClassId = st.ClassId AND f.SectionId = st.SectionId)

    -- fees: row does not belong to the student's class / medium
    UNION ALL
    SELECT st.*, 'FEE_WRONG_CLASS', 'Fees', 'High', 'Fee belongs to another class / medium',
           'Fee row ' + a.FeeType + ' Rs ' + CONVERT(VARCHAR(20), CAST(a.Amount AS DECIMAL(12,0))) + ' is from ' + a.HeadClassName + ' / ' + a.HeadSectionName
           + ', but the student is in ' + st.ClassName + ' / ' + st.SectionName + '.',
           'Confirm the correct class / medium of the student (or the fee) and tell the developer - this needs a human decision.', 0
    FROM st JOIN act a ON a.StudentId = st.StudentId
    WHERE a.HeadClass <> st.ClassId OR a.HeadSection <> st.SectionId OR a.HeadYear <> st.AcademicYearId

    UNION ALL
    SELECT st.*, 'FEE_HEAD_DELETED', 'Fees', 'High', 'Fee row of a deleted fee head',
           'The fee head ' + a.FeeType + ' (' + a.HeadClassName + ' / ' + a.HeadSectionName + ') was deleted but this student still has an active Rs ' + CONVERT(VARCHAR(20), CAST(a.Amount AS DECIMAL(12,0))) + ' row.',
           'Re-add the fee head, or ask the developer to switch the row off.', 0
    FROM st JOIN act a ON a.StudentId = st.StudentId WHERE a.HeadActive = 0

    UNION ALL
    SELECT st.*, 'FEE_DUPLICATE', 'Fees', 'High', 'Same fee applied twice',
           'The fee head ' + a.FeeType + ' appears ' + CAST(COUNT(*) AS VARCHAR(5)) + ' times for this student, so his fee is counted more than once.',
           'Tell the developer - one of the rows has to be switched off.', 0
    FROM st JOIN act a ON a.StudentId = st.StudentId
    GROUP BY st.StudentId, st.AdmissionNo, st.FullName, st.AcademicYearId, st.ClassId, st.SectionId, st.BatchId, st.Phone, st.FatherPhone, st.MotherPhone, st.ClassName, st.SectionName, st.BatchName, a.StructureId, a.FeeType
    HAVING COUNT(*) > 1

    -- fees: amount differs from the head
    UNION ALL
    SELECT st.*, 'FEE_DIFFERS', 'Fees', 'Medium', 'Student fee differs from the fee head',
           a.FeeType + ': student has Rs ' + CONVERT(VARCHAR(20), CAST(a.Amount AS DECIMAL(12,0))) + ' but the fee head says Rs ' + CONVERT(VARCHAR(20), CAST(a.HeadAmount AS DECIMAL(12,0))) + '.',
           'Confirm the right amount: fix the fee head, or tell the developer to mark this student''s amount as agreed.', 0
    FROM st JOIN act a ON a.StudentId = st.StudentId
    WHERE a.HeadClass = st.ClassId AND a.HeadSection = st.SectionId AND a.HeadYear = st.AcademicYearId
      AND a.HeadActive = 1 AND a.IsCustomAmount = 0 AND a.Amount <> a.HeadAmount

    -- information: agreed custom amounts (so they are visible, not a problem)
    UNION ALL
    SELECT st.*, 'CUSTOM_FEE', 'Fees', 'Info', 'Agreed custom fee',
           a.FeeType + ': Rs ' + CONVERT(VARCHAR(20), CAST(a.Amount AS DECIMAL(12,0))) + ' for this student (fee head is Rs ' + CONVERT(VARCHAR(20), CAST(a.HeadAmount AS DECIMAL(12,0))) + ').',
           'Nothing to do - listed so it is not a surprise.', 0
    FROM st JOIN act a ON a.StudentId = st.StudentId WHERE a.IsCustomAmount = 1
) x
ORDER BY CASE Severity WHEN 'High' THEN 1 WHEN 'Medium' THEN 2 WHEN 'Low' THEN 3 ELSE 4 END, ClassName, SectionName, FullName;";

    public DataHealthReport Build()
    {
        var students = new List<StudentFeeNumbers>();
        var charges = new Dictionary<int, decimal>();
        var issues = new List<DataIssue>();

        using var conn = db.Open();
        using var cmd = new SqlCommand(Sql, conn) { CommandTimeout = 90 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
            students.Add(new StudentFeeNumbers {
                StudentId = G.G<int>(r, "StudentId"), AdmissionNo = G.G<string>(r, "AdmissionNo") ?? "",
                FullName = r.StudentName(db) ?? "", ClassName = G.G<string>(r, "ClassName") ?? "",
                SectionName = G.G<string>(r, "SectionName") ?? "", BatchName = G.G<string>(r, "BatchName") ?? "",
                Fee = G.G<decimal>(r, "Fee"), Paid = G.G<decimal>(r, "Paid"), Discount = G.G<decimal>(r, "Discount"),
            });
        r.NextResult();
        while (r.Read())
        {
            var id = G.G<int>(r, "StudentId");
            charges[id] = charges.GetValueOrDefault(id) + AdditionalCharges.Parse(G.G<string>(r, "Remarks"));
        }
        r.NextResult();
        while (r.Read())
            issues.Add(new DataIssue {
                StudentId = G.G<int>(r, "StudentId"), AdmissionNo = G.G<string>(r, "AdmissionNo") ?? "",
                FullName = r.StudentName(db) ?? "", ClassName = G.G<string>(r, "ClassName") ?? "",
                SectionName = G.G<string>(r, "SectionName") ?? "", BatchName = G.G<string>(r, "BatchName") ?? "",
                Code = G.G<string>(r, "Code") ?? "", Group = G.G<string>(r, "Group") ?? "", Severity = G.G<string>(r, "Severity") ?? "",
                Title = G.G<string>(r, "Title") ?? "", Detail = G.G<string>(r, "Detail") ?? "", Action = G.G<string>(r, "Action") ?? "",
                CanApplyFee = G.G<int>(r, "CanApplyFee") == 1,
            });

        // Checks that need the additional charges (kept in payment remarks)
        foreach (var s in students)
        {
            s.Charges = charges.GetValueOrDefault(s.StudentId);
            string Rs(decimal v) => "Rs " + v.ToString("N0");

            if (s.Fee > 0)
            {
                var balance = s.Fee + s.Charges - s.Discount - s.Paid;
                if (balance < -0.5m)
                    issues.Add(Make(s, "OVERPAID", "Fees", "Medium", "Collected more than the fee",
                        $"Fee {Rs(s.Fee)} + charges {Rs(s.Charges)} = {Rs(s.Fee + s.Charges)}; paid {Rs(s.Paid)} + discount {Rs(s.Discount)} = {Rs(s.Paid + s.Discount)}. Extra: {Rs(-balance)}.",
                        "Check the payments and the additional charges on the Pay page; correct the wrong entry or adjust / refund."));
            }
            if (s.Charges > 0 && (s.Charges >= 5000 || (s.Fee > 0 && s.Charges >= s.Fee * 0.4m)))
                issues.Add(Make(s, "CHARGES_CHECK", "Fees", "Medium", "Additional charges look too big",
                    $"Additional charges {Rs(s.Charges)} are saved in payment remarks" + (s.Fee > 0 ? $" (fee is {Rs(s.Fee)})." : "."),
                    "Open Pay page > Additional Charges: enter the correct total, or 0, and press Update Charges Only."));
        }

        int Rank(string sev) => sev switch { "High" => 1, "Medium" => 2, "Low" => 3, _ => 4 };
        issues = issues.OrderBy(i => Rank(i.Severity)).ThenBy(i => i.ClassName).ThenBy(i => i.SectionName).ThenBy(i => i.FullName).ToList();
        return new DataHealthReport { StudentsChecked = students.Count, Issues = issues };
    }

    static DataIssue Make(StudentFeeNumbers s, string code, string group, string sev, string title, string detail, string action) => new() {
        StudentId = s.StudentId, AdmissionNo = s.AdmissionNo, FullName = s.FullName, ClassName = s.ClassName,
        SectionName = s.SectionName, BatchName = s.BatchName, Code = code, Group = group, Severity = sev,
        Title = title, Detail = detail, Action = action,
    };
}
