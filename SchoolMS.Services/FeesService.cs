using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class FeesService(FeesRepo repo, ActivityNotifier? notify = null)
{
    public (List<FeePayment> data, int total) GetAll(int pg, int ps, string? s, string? month, int? ft)
        => repo.GetAll(pg, ps, s, month, ft);
    public int Collect(int sid, int? ftId, decimal amt, decimal disc, decimal fine, DateTime date, string mode, string? txRef, int? yrId, string? month, string? remarks, int by, DateTime? dueDate=null)
    {
        var paymentId = repo.Save(sid, ftId, amt, disc, fine, date, mode, txRef, yrId, month, remarks, by, dueDate);
        notify?.FeeReceived(sid, amt, date, paymentId);
        return paymentId;
    }
    public FeePayment? GetPaymentById(int id) => repo.GetById(id);
    /// Dues of earlier years typed in by staff (the amount is part of every fee total).
    public PreviousDuesEntry GetPreviousDues(int sid) => repo.GetPreviousDues(sid);
    public string? SetPreviousDues(int sid, decimal amount, string? note, int by)
    {
        if (amount < 0) return "Previous dues cannot be negative.";
        if (amount > 10000000) return "That amount looks wrong.";
        return repo.SetPreviousDues(sid, amount, note, by);
    }
    public List<FeePayment> GetStudentHistory(int sid) => repo.GetStudentHistory(sid);
    /// Discount agreed at admission; every fee total = fee + charges - (payment discounts + this) - paid.
    public decimal GetAgreedDiscount(int sid) => repo.GetAgreedDiscount(sid);
    public List<FeePayment> GetPaymentsThisMonth() => repo.GetPaymentsThisMonth();
    public List<FinanceStudentRow> GetFinanceStudents(string? className, string? batchName, string? sectionName = null, bool anyBatch = false)
        => repo.GetFinanceStudents(className, batchName, sectionName, anyBatch);
    /// Sets the student's total additional charges to [target] by rewriting the "Additional Charges" part of the
    /// remarks on their payments (a wrong entry can be lowered or removed). Returns an error text, or null on success.
    public string? SetAdditionalCharges(int studentId, decimal target)
    {
        if (target < 0) return "Additional charges cannot be negative.";
        var history = repo.GetStudentHistory(studentId);
        var changes = AdditionalCharges.Plan(history, target);
        if (changes.Count == 0)
            return history.Count == 0 && target > 0
                ? "Additional charges are saved with a payment — collect a payment first."
                : null;
        foreach (var (id, remarks) in changes) repo.UpdateRemarks(id, remarks);
        return null;
    }
    public void DeletePayment(int paymentId, int deletedBy) => repo.DeletePayment(paymentId, deletedBy);
    public void RestorePayment(int paymentId) => repo.RestorePayment(paymentId);
    public List<FeePayment> GetDeletedPayments() => repo.GetDeletedPayments();

    public List<dynamic> GetClassFeesSummary(int classId, int? sectionId)
        => repo.GetClassFeesSummary(classId, sectionId);

    public OverallFeesSummary GetOverallFeesSummary()
        => repo.GetOverallFeesSummary();

    public decimal GetTotalAdditionalCharges()
        => repo.GetTotalAdditionalCharges();

    public (int TotalStudents, decimal TotalFees, decimal TotalCollected, decimal TotalDiscount, decimal TotalBalance, decimal CollectionPercentage) GetFinanceDashboardSummary(int? yearId = null)
        => repo.GetFinanceDashboardSummary(yearId);

    public List<FinanceAcademicBreakdown> GetFinanceDashboardAcademic(int? yearId = null)
        => repo.GetFinanceDashboardAcademic(yearId);

    public (decimal TotalFees, decimal TotalCollected, decimal TotalDiscount) GetStudentFeeDetails(int studentId, int yearId)
        => repo.GetStudentFeeDetails(studentId, yearId);

    public List<StudentFeeDetail> GetClassStudentFeeDetails(string className, string batchName)
        => repo.GetClassStudentFeeDetails(className, batchName);
}
