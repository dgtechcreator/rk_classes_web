using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class TeacherPaymentRepo(CommonConnectivity db)
{
    public int Save(int facultyId, string paymentType, decimal rate, decimal? quantity, decimal totalAmount,
        int paymentMonth, int paymentYear, string? remarks, int createdBy)
    {
        var query = $@"
            INSERT INTO TeacherPayments (FacultyId, PaymentType, Rate, Quantity, TotalAmount,
                PaymentMonth, PaymentYear, Remarks, CreatedBy, CreatedAt, IsDeleted)
            OUTPUT INSERTED.TeacherPaymentId
            VALUES ({facultyId}, '{paymentType}', {rate}, {(quantity.HasValue ? quantity.Value : "NULL")},
                {totalAmount}, {paymentMonth}, {paymentYear}, {(string.IsNullOrEmpty(remarks) ? "NULL" : $"'{remarks.Replace("'", "''")}'")},
                {createdBy}, GETDATE(), 0)
        ";

        var result = db.Sql(query, r => G.G<int>(r, "TeacherPaymentId")).FirstOrDefault();
        return result;
    }

    public List<TeacherPayment> GetUnpaid(int? facultyId = null)
    {
        var query = $@"
            SELECT tp.TeacherPaymentId, tp.FacultyId, f.FullName AS FacultyName,
                tp.PaymentType, tp.Rate, tp.Quantity, tp.TotalAmount,
                tp.PaymentMonth, tp.PaymentYear, tp.IsPaid, tp.PaymentDate,
                tp.PaymentMode, tp.TransactionRef, tp.ReceiptNo, tp.Remarks,
                tp.CreatedAt, tp.CreatedBy, tp.UpdatedAt, tp.UpdatedBy, tp.IsDeleted
            FROM TeacherPayments tp
            LEFT JOIN Faculty f ON tp.FacultyId = f.FacultyId
            WHERE tp.IsDeleted = 0 AND tp.IsPaid = 0
            {(facultyId.HasValue ? $"AND tp.FacultyId = {facultyId}" : "")}
            ORDER BY tp.PaymentYear DESC, tp.PaymentMonth DESC, f.FullName
        ";

        return db.Sql(query, MapTeacherPayment);
    }

    public List<TeacherPayment> GetByMonth(int month, int year)
    {
        var query = $@"
            SELECT tp.TeacherPaymentId, tp.FacultyId, f.FullName AS FacultyName,
                tp.PaymentType, tp.Rate, tp.Quantity, tp.TotalAmount,
                tp.PaymentMonth, tp.PaymentYear, tp.IsPaid, tp.PaymentDate,
                tp.PaymentMode, tp.TransactionRef, tp.ReceiptNo, tp.Remarks,
                tp.CreatedAt, tp.CreatedBy, tp.UpdatedAt, tp.UpdatedBy, tp.IsDeleted
            FROM TeacherPayments tp
            LEFT JOIN Faculty f ON tp.FacultyId = f.FacultyId
            WHERE tp.IsDeleted = 0 AND tp.PaymentMonth = {month} AND tp.PaymentYear = {year}
            ORDER BY f.FullName
        ";

        return db.Sql(query, MapTeacherPayment);
    }

    public TeacherPayment? GetById(int paymentId)
    {
        var query = $@"
            SELECT tp.TeacherPaymentId, tp.FacultyId, f.FullName AS FacultyName,
                tp.PaymentType, tp.Rate, tp.Quantity, tp.TotalAmount,
                tp.PaymentMonth, tp.PaymentYear, tp.IsPaid, tp.PaymentDate,
                tp.PaymentMode, tp.TransactionRef, tp.ReceiptNo, tp.Remarks,
                tp.CreatedAt, tp.CreatedBy, tp.UpdatedAt, tp.UpdatedBy, tp.IsDeleted
            FROM TeacherPayments tp
            LEFT JOIN Faculty f ON tp.FacultyId = f.FacultyId
            WHERE tp.TeacherPaymentId = {paymentId} AND tp.IsDeleted = 0
        ";

        return db.Sql(query, MapTeacherPayment).FirstOrDefault();
    }

    public void MarkAsPaid(int paymentId, string? paymentMode, string? transactionRef, int paidBy)
    {
        var receiptNo = GenerateReceiptNo();
        var query = $@"
            UPDATE TeacherPayments
            SET IsPaid = 1, PaymentDate = GETDATE(), PaymentMode = {(string.IsNullOrEmpty(paymentMode) ? "NULL" : $"'{paymentMode}'")},
                TransactionRef = {(string.IsNullOrEmpty(transactionRef) ? "NULL" : $"'{transactionRef.Replace("'", "''")}'")},
                ReceiptNo = '{receiptNo}', UpdatedBy = {paidBy}, UpdatedAt = GETDATE()
            WHERE TeacherPaymentId = {paymentId}
        ";

        db.Exec(query);
    }

    public void Delete(int paymentId, int deletedBy)
    {
        var query = $@"
            UPDATE TeacherPayments
            SET IsDeleted = 1, DeletedBy = {deletedBy}, DeletedAt = GETDATE()
            WHERE TeacherPaymentId = {paymentId}
        ";

        db.Exec(query);
    }

    private string GenerateReceiptNo()
    {
        var query = @"
            SELECT 'TPS-' + FORMAT(YEAR(GETDATE()), '0000') + '-' +
                   FORMAT(MONTH(GETDATE()), '00') + '-' +
                   FORMAT(ISNULL(MAX(CAST(RIGHT(ReceiptNo, 5) AS INT)), 0) + 1, '00005') AS ReceiptNo
            FROM TeacherPayments
            WHERE YEAR(PaymentDate) = YEAR(GETDATE()) AND MONTH(PaymentDate) = MONTH(GETDATE())
        ";

        return db.Sql(query, r => G.G<string>(r, "ReceiptNo")).FirstOrDefault() ?? "TPS-" + DateTime.Now.Year + "-" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "-00001";
    }

    static TeacherPayment MapTeacherPayment(SqlDataReader r) => new()
    {
        TeacherPaymentId = G.G<int>(r, "TeacherPaymentId"),
        FacultyId = G.G<int>(r, "FacultyId"),
        FacultyName = G.G<string>(r, "FacultyName") ?? "",
        PaymentType = G.G<string>(r, "PaymentType") ?? "",
        Rate = G.G<decimal>(r, "Rate"),
        Quantity = G.G<decimal?>(r, "Quantity"),
        TotalAmount = G.G<decimal>(r, "TotalAmount"),
        PaymentMonth = G.G<int>(r, "PaymentMonth"),
        PaymentYear = G.G<int>(r, "PaymentYear"),
        IsPaid = G.G<bool>(r, "IsPaid"),
        PaymentDate = G.G<DateTime?>(r, "PaymentDate"),
        PaymentMode = G.G<string>(r, "PaymentMode"),
        TransactionRef = G.G<string>(r, "TransactionRef"),
        ReceiptNo = G.G<string>(r, "ReceiptNo"),
        Remarks = G.G<string>(r, "Remarks"),
        CreatedAt = G.G<DateTime>(r, "CreatedAt"),
        CreatedBy = G.G<int?>(r, "CreatedBy"),
        UpdatedAt = G.G<DateTime?>(r, "UpdatedAt"),
        UpdatedBy = G.G<int?>(r, "UpdatedBy"),
        IsDeleted = G.G<bool>(r, "IsDeleted")
    };
}
