using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class FeesRepo(CommonConnectivity db)
{
    public (List<FeePayment> data, int total) GetAll(int pg, int ps, string? search, string? month, int? ft)
    {
        var op = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var data = db.ReadPaged("sp_GetFeePayments", new() {
            {"@PageNo",pg}, {"@PageSize",ps}, {"@Search",search}, {"@Month",month}, {"@FeeTypeId",ft}
        }, MapFee, op);
        return (data, op.Value is DBNull ? 0 : Convert.ToInt32(op.Value));
    }

    public int Save(int sid, int? ftId, decimal amt, decimal disc, decimal fine, DateTime date, string mode, string? txRef, int? yrId, string? month, string? remarks, int by, DateTime? dueDate=null)
        => db.ExecOut("sp_SaveFeePayment", new() {
            {"@StudentId",sid}, {"@FeeTypeId",(object?)ftId ?? DBNull.Value}, {"@Amount",amt},
            {"@Discount",disc}, {"@LateFine",fine}, {"@PaymentDate",date},
            {"@PaymentMode",mode}, {"@TransactionRef",txRef}, {"@AcademicYearId",yrId},
            {"@Month",month}, {"@Remarks",remarks}, {"@CollectedBy",by}, {"@DueDate",(object?)dueDate ?? DBNull.Value}
        }, "@NewPaymentId");

    public FeePayment? GetById(int paymentId)
    {
        var l = db.Sql($@"SELECT fp.*,s.FullName AS StudentName,s.AdmissionNo,s.RollNo,
            s.Phone AS StudentPhone,s.FatherPhone,s.Email AS StudentEmail,s.Address AS StudentAddress,
            c.ClassName,sec.SectionName,b.BatchName,ft.TypeName AS FeeTypeName,u.FullName AS CollectorName
            FROM FeePayments fp
            LEFT JOIN Students s   ON s.StudentId  = fp.StudentId
            LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId= s.SectionId
            LEFT JOIN Batches  b   ON b.BatchId    = s.BatchId
            LEFT JOIN FeeTypes ft  ON ft.FeeTypeId = fp.FeeTypeId
            LEFT JOIN Users    u   ON u.UserId     = fp.CollectedBy
            WHERE fp.PaymentId={paymentId}", MapFee);
        return l.FirstOrDefault();
    }

    public void DeletePayment(int paymentId, int deletedBy)
        => db.Exec("sp_DeleteFeePayment", new() { {"@PaymentId",paymentId}, {"@DeletedBy",deletedBy} });

    public void RestorePayment(int paymentId)
        => db.Exec("sp_RestoreFeePayment", new() { {"@PaymentId",paymentId} });

    public List<FeePayment> GetDeletedPayments()
        => db.Read("sp_GetDeletedFeePayments", new(), MapFee);

    // Same rule as sp_GetDashboardStats.FeesThisMonth: PaymentDate in the current month, not deleted.
    public List<FeePayment> GetPaymentsThisMonth()
        => db.Sql(@"SELECT fp.*,s.FullName AS StudentName,s.AdmissionNo,s.RollNo,
            c.ClassName,sec.SectionName,ft.TypeName AS FeeTypeName,u.FullName AS CollectorName
            FROM FeePayments fp
            LEFT JOIN Students s   ON s.StudentId  = fp.StudentId
            LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId= s.SectionId
            LEFT JOIN FeeTypes ft  ON ft.FeeTypeId = fp.FeeTypeId
            LEFT JOIN Users    u   ON u.UserId     = fp.CollectedBy
            WHERE fp.IsDeleted=0
              AND MONTH(fp.PaymentDate)=MONTH(GETDATE()) AND YEAR(fp.PaymentDate)=YEAR(GETDATE())
            ORDER BY fp.PaymentDate DESC, fp.CreatedAt DESC", MapFee);

    public List<FeePayment> GetStudentHistory(int studentId)
        => db.Sql($@"SELECT fp.*,s.FullName AS StudentName,s.AdmissionNo,s.RollNo,
            c.ClassName,sec.SectionName,ft.TypeName AS FeeTypeName,u.FullName AS CollectorName
            FROM FeePayments fp
            LEFT JOIN Students s   ON s.StudentId  = fp.StudentId
            LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId= s.SectionId
            LEFT JOIN FeeTypes ft  ON ft.FeeTypeId = fp.FeeTypeId
            LEFT JOIN Users    u   ON u.UserId     = fp.CollectedBy
            WHERE fp.StudentId={studentId} AND fp.IsDeleted=0
            ORDER BY fp.CreatedAt DESC", MapFee);

    public List<dynamic> GetClassFeesSummary(int classId, int? sectionId)
    {
        var query = $@"
            SELECT
                s.StudentId,
                s.FullName AS StudentName,
                s.AdmissionNo,
                c.ClassName,
                sec.SectionName,
                ISNULL(SUM(CASE WHEN fp.IsDeleted=0 THEN fs.Amount ELSE 0 END), 0) AS TotalFeesOwed,
                ISNULL(SUM(CASE WHEN fp.IsDeleted=0 THEN fp.NetAmount ELSE 0 END), 0) AS TotalCollected,
                ISNULL(SUM(CASE WHEN fp.IsDeleted=0 THEN fp.Discount ELSE 0 END), 0) AS TotalDiscount,
                ISNULL(SUM(CASE WHEN fp.IsDeleted=0 THEN fs.Amount ELSE 0 END), 0) - ISNULL(SUM(CASE WHEN fp.IsDeleted=0 THEN fp.NetAmount ELSE 0 END), 0) AS Balance
            FROM Students s
            LEFT JOIN Classes c ON c.ClassId = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
            LEFT JOIN FeeStructure fs ON fs.ClassId = s.ClassId AND (fs.SectionId IS NULL OR fs.SectionId = s.SectionId)
            LEFT JOIN FeePayments fp ON fp.StudentId = s.StudentId AND YEAR(fp.PaymentDate) = YEAR(GETDATE())
            WHERE s.ClassId = {classId} {(sectionId.HasValue ? $"AND s.SectionId = {sectionId}" : "")} AND s.Status = 'Active'
            GROUP BY s.StudentId, s.FullName, s.AdmissionNo, c.ClassName, sec.SectionName
            ORDER BY s.FullName
        ";

        return db.Sql(query, r => new {
            StudentId = G.G<int>(r, "StudentId"),
            StudentName = G.G<string>(r, "StudentName") ?? "",
            AdmissionNo = G.G<string>(r, "AdmissionNo") ?? "",
            ClassName = G.G<string>(r, "ClassName") ?? "",
            SectionName = G.G<string>(r, "SectionName") ?? "",
            TotalFeesOwed = G.G<decimal>(r, "TotalFeesOwed"),
            TotalCollected = G.G<decimal>(r, "TotalCollected"),
            TotalDiscount = G.G<decimal>(r, "TotalDiscount"),
            Balance = G.G<decimal>(r, "Balance")
        }).Cast<dynamic>().ToList();
    }

    // Same numbers as the Finance dashboard (sp_GetFinanceDashboardSummary: active students of the current
    // academic year, balance = fees - collected - discount) so the Home card and Finance page always agree.
    public OverallFeesSummary GetOverallFeesSummary()
    {
        var result = db.Read("sp_GetFinanceDashboardSummary", new(), r => new {
            TotalFees = G.G<decimal>(r, "TotalFees"),
            TotalCollected = G.G<decimal>(r, "TotalCollected"),
            TotalDiscount = G.G<decimal>(r, "TotalDiscount"),
            TotalBalance = G.G<decimal>(r, "TotalBalance")
        }).FirstOrDefault();

        if (result == null)
            return new OverallFeesSummary { TotalFeesOwed = 0, TotalCollected = 0, TotalDiscount = 0, Balance = 0 };

        return new OverallFeesSummary {
            TotalFeesOwed = result.TotalFees,
            TotalCollected = result.TotalCollected,
            TotalDiscount = result.TotalDiscount,
            Balance = Math.Max(0, result.TotalBalance)
        };
    }

    public decimal GetTotalAdditionalCharges()
    {
        var query = @"
            SELECT ISNULL(SUM(
                CAST(TRY_CONVERT(DECIMAL(10, 2),
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                TRIM(
                                    SUBSTRING(
                                        Remarks,
                                        CHARINDEX(':', Remarks) + 1,
                                        LEN(Remarks)
                                    )
                                ),
                                '₹', ''
                            ),
                            ',', ''
                        ),
                        ' ', ''
                    )
                ) AS DECIMAL(10, 2))
            ), 0) as TotalCharges
            FROM FeePayments
            WHERE Remarks IS NOT NULL
                AND Remarks LIKE '%Additional Charges:%'
                AND IsDeleted = 0
        ";

        var result = db.Sql(query, r => G.G<decimal>(r, "TotalCharges")).FirstOrDefault();
        return result;
    }

    static FeePayment MapFee(SqlDataReader r) => new() {
        PaymentId      = G.G<int>(r,"PaymentId"),
        ReceiptNo      = G.G<string>(r,"ReceiptNo")??"",
        StudentId      = G.G<int>(r,"StudentId"),
        StudentName    = G.G<string>(r,"StudentName"),
        AdmissionNo    = G.G<string>(r,"AdmissionNo"),
        ClassName      = G.G<string>(r,"ClassName"),
        SectionName    = G.G<string>(r,"SectionName"),
        BatchName      = G.G<string>(r,"BatchName"),
        FeeTypeId      = G.G<int?>(r,"FeeTypeId"),
        FeeTypeName    = G.G<string>(r,"FeeTypeName"),
        Amount         = G.G<decimal>(r,"Amount"),
        Discount       = G.G<decimal>(r,"Discount"),
        LateFine       = G.G<decimal>(r,"LateFine"),
        NetAmount      = G.G<decimal>(r,"NetAmount"),
        PaymentDate    = G.G<DateTime>(r,"PaymentDate"),
        DueDate        = G.G<DateTime?>(r,"DueDate"),
        PaymentMode    = G.G<string>(r,"PaymentMode")??"Cash",
        TransactionRef = G.G<string>(r,"TransactionRef"),
        Month          = G.G<string>(r,"Month"),
        Remarks        = G.G<string>(r,"Remarks"),
        CollectorName  = G.G<string>(r,"CollectorName"),
        StudentPhone   = G.G<string>(r,"StudentPhone"),
        FatherPhone    = G.G<string>(r,"FatherPhone"),
        StudentEmail   = G.G<string>(r,"StudentEmail"),
        StudentAddress = G.G<string>(r,"StudentAddress"),
        CreatedAt      = G.G<DateTime>(r,"CreatedAt"),
        IsDeleted      = G.G<bool>(r,"IsDeleted"),
        DeletedAt      = G.G<DateTime?>(r,"DeletedAt"),
        DeletedByName  = G.G<string>(r,"DeletedByName")
    };

    public (int TotalStudents, decimal TotalFees, decimal TotalCollected, decimal TotalDiscount, decimal TotalBalance, decimal CollectionPercentage) GetFinanceDashboardSummary(int? yearId = null)
    {
        var result = db.Read(
            "sp_GetFinanceDashboardSummary",
            new() { { "@AcademicYearId", (object?)yearId ?? DBNull.Value } },
            r => new
            {
                TotalStudents = G.G<int>(r, "TotalStudents"),
                TotalFees = G.G<decimal>(r, "TotalFees"),
                TotalCollected = G.G<decimal>(r, "TotalCollected"),
                TotalDiscount = G.G<decimal>(r, "TotalDiscount"),
                TotalBalance = G.G<decimal>(r, "TotalBalance"),
                CollectionPercentage = G.G<decimal>(r, "CollectionPercentage")
            }
        ).FirstOrDefault();

        if (result == null)
            return (0, 0, 0, 0, 0, 0);

        return (result.TotalStudents, result.TotalFees, result.TotalCollected, result.TotalDiscount, result.TotalBalance, result.CollectionPercentage);
    }

    public List<FinanceAcademicBreakdown> GetFinanceDashboardAcademic(int? yearId = null)
    {
        return db.Read(
            "sp_GetFinanceDashboardAcademic",
            new() { { "@AcademicYearId", (object?)yearId ?? DBNull.Value } },
            MapAcademicData
        );
    }

    public (decimal TotalFees, decimal TotalCollected, decimal TotalDiscount) GetStudentFeeDetails(int studentId, int yearId)
    {
        var query = $@"
            SELECT
                COALESCE(SUM(fs.Amount), 0) AS TotalFees,
                COALESCE(SUM(fs.PaidAmount), 0) AS TotalCollected,
                COALESCE(SUM(fs.Discount), 0) AS TotalDiscount
            FROM StudentFees fs
            WHERE fs.StudentId = {studentId} AND fs.AcademicYearId = {yearId}
        ";

        var result = db.Sql(query, r => new
        {
            TotalFees = G.G<decimal>(r, "TotalFees"),
            TotalCollected = G.G<decimal>(r, "TotalCollected"),
            TotalDiscount = G.G<decimal>(r, "TotalDiscount")
        }).FirstOrDefault();

        if (result == null)
            return (0, 0, 0);

        return (result.TotalFees, result.TotalCollected, result.TotalDiscount);
    }

    public List<StudentFeeDetail> GetClassStudentFeeDetails(string className, string batchName)
    {
        return db.Read("sp_GetStudentsByClassAndBatch", new() {
            { "@ClassName", className },
            { "@BatchName", batchName }
        }, r => new StudentFeeDetail
        {
            StudentName = G.G<string>(r, "FullName") ?? "",
            TotalFees = G.G<decimal>(r, "TotalFees"),
            Discount = G.G<decimal>(r, "Discount"),
            Collected = G.G<decimal>(r, "Collected"),
            Balance = G.G<decimal>(r, "Balance")
        });
    }

    /// Active students of the current academic year with their fee position. When [className] is given the
    /// list is limited to that class + batch (an empty [batchName] means "no batch"); otherwise everyone.
    public List<FinanceStudentRow> GetFinanceStudents(string? className, string? batchName)
    {
        const string sql = @"
            DECLARE @Yr INT = (SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC);
            SELECT s.StudentId, s.FullName, s.AdmissionNo,
                   ISNULL(s.Phone,'') AS Phone, ISNULL(s.FatherPhone,'') AS FatherPhone, ISNULL(s.MotherPhone,'') AS MotherPhone,
                   LTRIM(RTRIM(ISNULL(c.ClassName,''))) AS ClassName,
                   LTRIM(RTRIM(ISNULL(sec.SectionName,''))) AS SectionName,
                   LTRIM(RTRIM(ISNULL(b.BatchName,''))) AS BatchName,
                   ISNULL((SELECT SUM(sf.Amount) FROM StudentFees sf WHERE sf.StudentId = s.StudentId), 0) AS TotalFees,
                   ISNULL((SELECT SUM(fp.Amount) FROM FeePayments fp WHERE fp.StudentId = s.StudentId AND fp.IsDeleted = 0), 0) AS Collected,
                   ISNULL((SELECT SUM(fp.Discount) FROM FeePayments fp WHERE fp.StudentId = s.StudentId AND fp.IsDeleted = 0), 0) AS Discount
            FROM Students s
            LEFT JOIN Classes  c   ON c.ClassId     = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
            LEFT JOIN Batches  b   ON b.BatchId     = s.BatchId
            WHERE s.Status = 'Active' AND s.AcademicYearId = @Yr
              AND (@ClassName IS NULL OR LTRIM(RTRIM(ISNULL(c.ClassName,''))) = @ClassName)
              AND (@ClassName IS NULL OR LTRIM(RTRIM(ISNULL(b.BatchName,''))) = @BatchName)
            ORDER BY s.FullName";

        var list = new List<FinanceStudentRow>();
        using var conn = db.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@ClassName", string.IsNullOrWhiteSpace(className) ? DBNull.Value : className.Trim());
        cmd.Parameters.AddWithValue("@BatchName", (batchName ?? "").Trim());
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new FinanceStudentRow {
                StudentId = G.G<int>(r, "StudentId"), FullName = G.G<string>(r, "FullName") ?? "",
                AdmissionNo = G.G<string>(r, "AdmissionNo") ?? "",
                ClassName = G.G<string>(r, "ClassName") ?? "", SectionName = G.G<string>(r, "SectionName") ?? "",
                BatchName = G.G<string>(r, "BatchName") ?? "",
                Phone = G.G<string>(r, "Phone") ?? "", FatherPhone = G.G<string>(r, "FatherPhone") ?? "",
                MotherPhone = G.G<string>(r, "MotherPhone") ?? "",
                TotalFees = G.G<decimal>(r, "TotalFees"), Collected = G.G<decimal>(r, "Collected"),
                Discount = G.G<decimal>(r, "Discount"),
            });
        return list;
    }

    static FinanceAcademicBreakdown MapAcademicData(SqlDataReader r) => new()
    {
        ClassName = G.G<string>(r, "ClassName") ?? "",
        BatchName = G.G<string>(r, "BatchName") ?? "",
        StudentCount = G.G<int>(r, "StudentCount"),
        TotalFees = G.G<decimal>(r, "TotalFees"),
        EstimatedCollected = G.G<decimal>(r, "EstimatedCollected"),
        EstimatedDiscount = G.G<decimal>(r, "EstimatedDiscount")
    };
}
