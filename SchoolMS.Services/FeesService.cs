using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class FeesService(FeesRepo repo)
{
    public (List<FeePayment> data, int total) GetAll(int pg, int ps, string? s, string? month, int? ft)
        => repo.GetAll(pg, ps, s, month, ft);
    public int Collect(int sid, int ftId, decimal amt, decimal disc, decimal fine, DateTime date, string mode, string? txRef, int? yrId, string? month, string? remarks, int by)
        => repo.Save(sid, ftId, amt, disc, fine, date, mode, txRef, yrId, month, remarks, by);
    public FeePayment? GetPaymentById(int id) => repo.GetById(id);
    public List<FeePayment> GetStudentHistory(int sid) => repo.GetStudentHistory(sid);
}
