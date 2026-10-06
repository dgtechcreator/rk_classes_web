using SchoolMS.Domain;
using SchoolMS.Repository;
using System.Linq;

namespace SchoolMS.Services;

public class TeacherPaymentService(TeacherPaymentRepo repo, StudentService studentSvc)
{
    public int Save(int facultyId, string paymentType, decimal rate, decimal? quantity, int paymentMonth, int paymentYear, string? remarks, int createdBy)
    {
        // Calculate total amount
        decimal totalAmount = paymentType switch
        {
            "Hourly" => (quantity ?? 0) * rate,
            "Topic" => (quantity ?? 0) * rate,
            "Fixed" => rate,
            _ => 0
        };

        return repo.Save(facultyId, paymentType, rate, quantity, totalAmount, paymentMonth, paymentYear, remarks, createdBy);
    }

    public List<TeacherPayment> GetUnpaid(int? facultyId = null) => repo.GetUnpaid(facultyId);

    public List<TeacherPayment> GetByMonth(int month, int year) => repo.GetByMonth(month, year);

    public List<TeacherPayment> Search(int? year, int? month, int? facultyId, string? status) => repo.Search(year, month, facultyId, status);

    /// Totals + a per-teacher roll-up for a filtered list (used by the admin summary, web and app).
    public TeacherPaymentSummary Summarize(List<TeacherPayment> list)
    {
        var s = new TeacherPaymentSummary {
            Count = list.Count,
            TotalAmount = list.Sum(p => p.TotalAmount),
            PaidAmount = list.Where(p => p.IsPaid).Sum(p => p.TotalAmount),
            PendingAmount = list.Where(p => !p.IsPaid).Sum(p => p.TotalAmount),
            PaidCount = list.Count(p => p.IsPaid),
            PendingCount = list.Count(p => !p.IsPaid),
        };
        s.Teachers = list.GroupBy(p => (p.FacultyId, Name: (p.FacultyName ?? "").Trim()))
            .Select(g => new TeacherPaymentTeacherRow {
                FacultyId = g.Key.FacultyId, FacultyName = g.Key.Name, Count = g.Count(),
                PaidAmount = g.Where(p => p.IsPaid).Sum(p => p.TotalAmount),
                PendingAmount = g.Where(p => !p.IsPaid).Sum(p => p.TotalAmount),
                LastPaidOn = g.Where(p => p.IsPaid && p.PaymentDate.HasValue).Select(p => p.PaymentDate).Max(),
            })
            .OrderByDescending(t => t.PaidAmount + t.PendingAmount).ThenBy(t => t.FacultyName).ToList();
        return s;
    }

    public TeacherPayment? GetById(int paymentId) => repo.GetById(paymentId);

    public void MarkAsPaid(int paymentId, string? paymentMode, string? transactionRef, int paidBy) => repo.MarkAsPaid(paymentId, paymentMode, transactionRef, paidBy);

    public void Delete(int paymentId, int deletedBy) => repo.Delete(paymentId, deletedBy);
}
