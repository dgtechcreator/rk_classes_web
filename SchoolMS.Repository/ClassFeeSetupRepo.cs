using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class ClassFeeSetupRepo(CommonConnectivity db)
{
    public List<ClassFeeSetup> GetAll()
        => db.Read("sp_GetClassFeeSetup", new(), r => new ClassFeeSetup {
            SetupId    = G.G<int>(r,"SetupId"),
            BatchId    = G.G<int>(r,"BatchId"),
            BatchName  = G.G<string>(r,"BatchName"),
            ClassId    = G.G<int>(r,"ClassId"),
            ClassName  = G.G<string>(r,"ClassName"),
            SectionId  = G.G<int>(r,"SectionId"),
            SectionName= G.G<string>(r,"SectionName"),
            Amount     = G.G<decimal>(r,"Amount")
        });

    public int Save(ClassFeeSetup fs)
        => db.ExecOut("sp_SaveClassFeeSetup", new() {
            {"@SetupId",fs.SetupId}, {"@BatchId",fs.BatchId},
            {"@ClassId",fs.ClassId}, {"@SectionId",fs.SectionId},
            {"@Amount",fs.Amount}
        }, "@NewId");

    public void Delete(int setupId)
        => db.Exec("sp_DeleteClassFeeSetup", new() { {"@SetupId",setupId} });

    public decimal? GetAmount(int batchId, int classId, int sectionId)
    {
        var rows = db.Read("sp_GetFeeAmountBySelection",
            new() { {"@BatchId",batchId}, {"@ClassId",classId}, {"@SectionId",sectionId} },
            r => G.G<decimal>(r,"Amount"));
        return rows.Count > 0 ? rows[0] : (decimal?)null;
    }
}
