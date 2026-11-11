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
    /// Edits a receipt in place. The "Additional Charges" part of its remarks is kept as it is (charges are corrected with
    /// SetAdditionalCharges), and the discount stays once-per-student: when another receipt already carries it, this one's
    /// discount cannot change. Returns an error text, or null on success.
    public string? UpdatePayment(int paymentId, decimal paid, decimal discount, DateTime? date, DateTime? dueDate,
        string? mode, string? txRef, string? remarks)
    {
        var p = repo.GetById(paymentId);
        if (p == null || p.IsDeleted) return "This receipt was not found (it may have been deleted).";
        if (paid <= 0) return "Amount received must be more than 0.";
        if (paid > 10000000) return "That amount looks wrong.";
        if (discount < 0) return "Discount cannot be negative.";
        var day = (date ?? p.PaymentDate).Date;
        if (day > DateTime.Today) return "Payment date cannot be in the future.";
        if (string.IsNullOrWhiteSpace(mode)) return "Choose a payment mode.";

        var othersDiscount = repo.GetStudentHistory(p.StudentId).Where(x => x.PaymentId != paymentId).Sum(x => x.Discount);
        if (othersDiscount > 0 && discount != p.Discount)
            return "Discount was already given on another receipt of this student, so it cannot be changed here.";

        var final = AdditionalCharges.WithAmount(remarks, AdditionalCharges.Parse(p.Remarks));
        repo.UpdatePayment(paymentId, paid, discount, day, dueDate ?? p.DueDate, mode.Trim(), txRef, final);
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

    /// <summary>
    /// The Finance class-wise rows split by class + batch + medium, built from the same student-wise list the rows open
    /// (so a row always equals what its "Students" link shows). Ordered 5th..12th, Morning before Evening, then medium.
    /// TotalFees here is the fee BEFORE discount; the caller subtracts EstimatedDiscount for "what will be received".
    /// </summary>
    public List<FinanceAcademicBreakdown> GetFinanceGroups()
    {
        var rows = repo.GetFinanceStudents(null, null, null, true);
        static int ClassNo(string c) { var d = new string((c ?? "").Where(char.IsDigit).ToArray()); return int.TryParse(d, out var n) ? n : 99; }
        static int BatchNo(string b) { var l = (b ?? "").ToLowerInvariant(); return l.Contains("morning") ? 0 : l.Contains("evening") ? 1 : 2; }
        return rows.GroupBy(r => (C: r.ClassName.Trim(), B: r.BatchName.Trim(), S: r.SectionName.Trim()))
            .Select(g => new FinanceAcademicBreakdown {
                ClassName = g.Key.C, BatchName = g.Key.B, SectionName = g.Key.S, StudentCount = g.Count(),
                TotalFees = g.Sum(r => r.TotalFees), EstimatedCollected = g.Sum(r => r.Collected), EstimatedDiscount = g.Sum(r => r.Discount),
            })
            .OrderBy(x => ClassNo(x.ClassName)).ThenBy(x => x.ClassName).ThenBy(x => BatchNo(x.BatchName)).ThenBy(x => x.BatchName).ThenBy(x => x.SectionName)
            .ToList();
    }

    public List<FinanceAcademicBreakdown> GetFinanceDashboardAcademic(int? yearId = null)
        => repo.GetFinanceDashboardAcademic(yearId);

    public (decimal TotalFees, decimal TotalCollected, decimal TotalDiscount) GetStudentFeeDetails(int studentId, int yearId)
        => repo.GetStudentFeeDetails(studentId, yearId);

    public List<StudentFeeDetail> GetClassStudentFeeDetails(string className, string batchName)
        => repo.GetClassStudentFeeDetails(className, batchName);
}
