using SchoolMS.Domain;
using SchoolMS.Repository;

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

    public TeacherPayment? GetById(int paymentId) => repo.GetById(paymentId);

    public void MarkAsPaid(int paymentId, string? paymentMode, string? transactionRef, int paidBy) => repo.MarkAsPaid(paymentId, paymentMode, transactionRef, paidBy);

    public void Delete(int paymentId, int deletedBy) => repo.Delete(paymentId, deletedBy);
}
