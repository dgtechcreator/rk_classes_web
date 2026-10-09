using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class FeeStructureRepo(CommonConnectivity db)
{
    public List<FeeStructure> GetAll(int? yearId, int? classId, int? sectionId)
        => db.Read("sp_GetFeeStructure",
            new() { {"@AcademicYearId",yearId}, {"@ClassId",classId}, {"@SectionId",sectionId} },
            r => new FeeStructure {
                StructureId    = G.G<int>(r,"StructureId"),
                AcademicYearId = G.G<int>(r,"AcademicYearId"),
                YearName       = G.G<string>(r,"YearName"),
                ClassId        = G.G<int>(r,"ClassId"),
                ClassName      = G.G<string>(r,"ClassName"),
                SectionId      = G.G<int>(r,"SectionId"),
                SectionName    = G.G<string>(r,"SectionName"),
                FeeTypeId      = G.G<int>(r,"FeeTypeId"),
                FeeTypeName    = G.G<string>(r,"FeeTypeName"),
                Amount         = G.G<decimal>(r,"Amount"),
                DueDay         = G.G<int>(r,"DueDay"),
                IsMonthly      = G.G<bool>(r,"IsMonthly"),
                Remarks        = G.G<string>(r,"Remarks"),
                CreatedByName  = G.G<string>(r,"CreatedByName"),
                CreatedAt      = G.G<DateTime>(r,"CreatedAt")
            });

    public int Save(FeeStructure fs, int by)
        => db.ExecOut("sp_SaveFeeStructure", new() {
            {"@StructureId",fs.StructureId}, {"@AcademicYearId",fs.AcademicYearId},
            {"@ClassId",fs.ClassId}, {"@SectionId",fs.SectionId},
            {"@FeeTypeId",fs.FeeTypeId}, {"@Amount",fs.Amount},
            {"@DueDay",fs.DueDay}, {"@IsMonthly",fs.IsMonthly},
            {"@Remarks",fs.Remarks}, {"@CreatedBy",by}
        }, "@NewId");

    public void Delete(int structureId)
        => db.Exec("sp_DeleteFeeStructure", new() { {"@StructureId",structureId} });

    /// <summary>
    /// Smart lookup: tries 4 levels of matching (year+class+section → year+class →
    /// name-based class+section → name-based class-only) so fee always shows even
    /// when stored IDs differ between Students and FeeStructure.
    /// </summary>
    public List<FeeStructure> GetAllForStudent(int studentId)
        => db.Read("sp_GetFeeStructureByStudent",
            new() { {"@StudentId", studentId} },
            r => new FeeStructure {
                StructureId    = G.G<int>(r,"StructureId"),
                AcademicYearId = G.G<int>(r,"AcademicYearId"),
                YearName       = G.G<string>(r,"YearName"),
                ClassId        = G.G<int>(r,"ClassId"),
                ClassName      = G.G<string>(r,"ClassName"),
                SectionId      = G.G<int>(r,"SectionId"),
                SectionName    = G.G<string>(r,"SectionName"),
                FeeTypeId      = G.G<int>(r,"FeeTypeId"),
                FeeTypeName    = G.G<string>(r,"FeeTypeName"),
                Amount         = G.G<decimal>(r,"Amount"),
                DueDay         = G.G<int>(r,"DueDay"),
                IsMonthly      = G.G<bool>(r,"IsMonthly"),
                Remarks        = G.G<string>(r,"Remarks"),
                CreatedByName  = G.G<string>(r,"CreatedByName"),
                CreatedAt      = G.G<DateTime>(r,"CreatedAt")
            });

    public List<FeeStructureSummary> GetSummary(int? yearId)
        => db.Read("sp_GetFeeStructureSummary", new() { {"@AcademicYearId",yearId} },
            r => new FeeStructureSummary {
                ClassName       = G.G<string>(r,"ClassName"),
                SectionName     = G.G<string>(r,"SectionName"),
                YearName        = G.G<string>(r,"YearName"),
                StudentCount    = G.G<int>(r,"StudentCount"),
                TotalMonthlyFee = G.G<decimal>(r,"TotalMonthlyFee"),
                FeeHeads        = G.G<int>(r,"FeeHeads"),
                CollectedAmt    = G.G<decimal>(r,"CollectedAmt"),
                PendingAmt      = G.G<decimal>(r,"PendingAmt")
            });

    public void ApplyToStudent(int studentId)
        => db.Exec("sp_ApplyFeesToStudent", new() { {"@StudentId",studentId} });

    public List<StudentFee> GetStudentFees(int studentId)
        => db.Read("sp_GetStudentFees", new() { {"@StudentId",studentId} }, MapStudentFee);

    public (List<StudentFee> data, int total) GetFeesDue(int? yearId, int? cls, int? sec, int? ft, string? status, int pg, int ps)
    {
        var op = new SqlParameter("@TotalCount", System.Data.SqlDbType.Int)
                 { Direction = System.Data.ParameterDirection.Output };
        var data = db.ReadPaged("sp_GetFeesDue", new() {
            {"@AcademicYearId",yearId}, {"@ClassId",cls}, {"@SectionId",sec},
            {"@FeeTypeId",ft}, {"@Status",status}, {"@PageNo",pg}, {"@PageSize",ps}
        }, MapStudentFee, op);
        return (data, op.Value is DBNull ? 0 : Convert.ToInt32(op.Value));
    }

    public int CollectFee(int studentFeeId, decimal paid, decimal disc, decimal fine,
                          string mode, string? txRef, string? remarks, int by)
        => db.ExecOut("sp_CollectStudentFee", new() {
            {"@StudentFeeId",studentFeeId}, {"@PaidAmount",paid},
            {"@Discount",disc}, {"@LateFine",fine},
            {"@PaymentMode",mode}, {"@TransactionRef",txRef},
            {"@Remarks",remarks}, {"@CollectedBy",by}
        }, "@NewPaymentId");

    public int GenerateMonthlyFees(string month, int yearId)
        => db.ExecOut("sp_GenerateMonthlyFees",
            new() { {"@Month",month}, {"@AcademicYearId",yearId} }, "@FeesGenerated");

    StudentFee MapStudentFee(SqlDataReader r) => new() {
        StudentFeeId   = G.G<int>(r,"StudentFeeId"),
        StudentId      = G.G<int>(r,"StudentId"),
        StudentName    = r.StudentName(db, "StudentName"),
        AdmissionNo    = G.G<string>(r,"AdmissionNo"),
        RollNo         = G.G<string>(r,"RollNo"),
        ClassName      = G.G<string>(r,"ClassName"),
        SectionName    = G.G<string>(r,"SectionName"),
        StructureId    = G.G<int>(r,"StructureId"),
        AcademicYearId = G.G<int>(r,"AcademicYearId"),
        YearName       = G.G<string>(r,"YearName"),
        FeeTypeId      = G.G<int>(r,"FeeTypeId"),
        FeeTypeName    = G.G<string>(r,"FeeTypeName"),
        Month          = G.G<string>(r,"Month"),
        DueDate        = G.G<DateTime?>(r,"DueDate"),
        Amount         = G.G<decimal>(r,"Amount"),
        PaidAmount     = G.G<decimal>(r,"PaidAmount"),
        Discount       = G.G<decimal>(r,"Discount"),
        LateFine       = G.G<decimal>(r,"LateFine"),
        BalanceAmount  = G.G<decimal>(r,"BalanceAmount"),
        Status         = G.G<string>(r,"Status") ?? "Pending",
        CreatedAt      = G.G<DateTime>(r,"CreatedAt")
    };
}
