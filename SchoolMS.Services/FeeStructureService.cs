using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class FeeStructureService(FeeStructureRepo repo)
{
    public List<FeeStructure>     GetAll(int? yr, int? cls, int? sec) => repo.GetAll(yr, cls, sec);
    public int                    Save(FeeStructure fs, int by)       => repo.Save(fs, by);
    public void                   Delete(int id)                      => repo.Delete(id);
    public List<FeeStructureSummary> GetSummary(int? yr)             => repo.GetSummary(yr);
    public void                   ApplyToStudent(int sid)             => repo.ApplyToStudent(sid);
    public List<StudentFee>       GetStudentFees(int sid)             => repo.GetStudentFees(sid);
    public (List<StudentFee> data, int total) GetFeesDue(int? yr, int? cls, int? sec, int? ft, string? status, int pg, int ps)
        => repo.GetFeesDue(yr, cls, sec, ft, status, pg, ps);
    public int CollectFee(int sfId, decimal paid, decimal disc, decimal fine, string mode, string? tx, string? rem, int by)
        => repo.CollectFee(sfId, paid, disc, fine, mode, tx, rem, by);
    public int GenerateMonthlyFees(string month, int yearId) => repo.GenerateMonthlyFees(month, yearId);
}
