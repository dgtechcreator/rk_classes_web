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

    public int Save(int sid, int ftId, decimal amt, decimal disc, decimal fine, DateTime date, string mode, string? txRef, int? yrId, string? month, string? remarks, int by)
        => db.ExecOut("sp_SaveFeePayment", new() {
            {"@StudentId",sid}, {"@FeeTypeId",ftId}, {"@Amount",amt},
            {"@Discount",disc}, {"@LateFine",fine}, {"@PaymentDate",date},
            {"@PaymentMode",mode}, {"@TransactionRef",txRef}, {"@AcademicYearId",yrId},
            {"@Month",month}, {"@Remarks",remarks}, {"@CollectedBy",by}
        }, "@NewPaymentId");

    public FeePayment? GetById(int paymentId)
    {
        var l = db.Sql($@"SELECT fp.*,s.FullName AS StudentName,s.AdmissionNo,s.RollNo,
            c.ClassName,sec.SectionName,ft.TypeName AS FeeTypeName,u.FullName AS CollectorName
            FROM FeePayments fp
            LEFT JOIN Students s   ON s.StudentId  = fp.StudentId
            LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId= s.SectionId
            LEFT JOIN FeeTypes ft  ON ft.FeeTypeId = fp.FeeTypeId
            LEFT JOIN Users    u   ON u.UserId     = fp.CollectedBy
            WHERE fp.PaymentId={paymentId}", MapFee);
        return l.FirstOrDefault();
    }

    public List<FeePayment> GetStudentHistory(int studentId)
        => db.Sql($@"SELECT fp.*,s.FullName AS StudentName,s.AdmissionNo,s.RollNo,
            c.ClassName,sec.SectionName,ft.TypeName AS FeeTypeName,u.FullName AS CollectorName
            FROM FeePayments fp
            LEFT JOIN Students s   ON s.StudentId  = fp.StudentId
            LEFT JOIN Classes  c   ON c.ClassId    = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId= s.SectionId
            LEFT JOIN FeeTypes ft  ON ft.FeeTypeId = fp.FeeTypeId
            LEFT JOIN Users    u   ON u.UserId     = fp.CollectedBy
            WHERE fp.StudentId={studentId}
            ORDER BY fp.CreatedAt DESC", MapFee);

    static FeePayment MapFee(SqlDataReader r) => new() {
        PaymentId      = G.G<int>(r,"PaymentId"),
        ReceiptNo      = G.G<string>(r,"ReceiptNo")??"",
        StudentId      = G.G<int>(r,"StudentId"),
        StudentName    = G.G<string>(r,"StudentName"),
        AdmissionNo    = G.G<string>(r,"AdmissionNo"),
        ClassName      = G.G<string>(r,"ClassName"),
        SectionName    = G.G<string>(r,"SectionName"),
        FeeTypeId      = G.G<int?>(r,"FeeTypeId"),
        FeeTypeName    = G.G<string>(r,"FeeTypeName"),
        Amount         = G.G<decimal>(r,"Amount"),
        Discount       = G.G<decimal>(r,"Discount"),
        LateFine       = G.G<decimal>(r,"LateFine"),
        NetAmount      = G.G<decimal>(r,"NetAmount"),
        PaymentDate    = G.G<DateTime>(r,"PaymentDate"),
        PaymentMode    = G.G<string>(r,"PaymentMode")??"Cash",
        TransactionRef = G.G<string>(r,"TransactionRef"),
        Month          = G.G<string>(r,"Month"),
        Remarks        = G.G<string>(r,"Remarks"),
        CollectorName  = G.G<string>(r,"CollectorName"),
        CreatedAt      = G.G<DateTime>(r,"CreatedAt")
    };
}
