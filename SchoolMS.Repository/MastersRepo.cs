using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class MastersRepo(CommonConnectivity db)
{
    // ── Academic Year ─────────────────────────────────────────
    public List<AcademicYear> GetYears() => db.Read("sp_GetAcademicYears", new(),
        r => new AcademicYear {
            YearId=G.G<int>(r,"YearId"), YearName=G.G<string>(r,"YearName")??"",
            IsCurrent=G.G<bool>(r,"IsCurrent"), IsActive=G.G<bool>(r,"IsActive"),
            StudentCount=G.G<int>(r,"StudentCount")
        });

    public int SaveYear(AcademicYear m) => db.ExecOut("sp_SaveAcademicYear", new() {
        {"@YearId",m.YearId}, {"@YearName",m.YearName},
        {"@IsCurrent",m.IsCurrent}, {"@IsActive",m.IsActive}
    }, "@NewId");

    public void DeleteYear(int id) => db.Exec("sp_DeleteAcademicYear", new(){{"@YearId",id}});

    // ── Class ─────────────────────────────────────────────────
    public List<Class> GetClasses() => db.Read("sp_GetClasses", new(),
        r => new Class {
            ClassId=G.G<int>(r,"ClassId"), ClassName=G.G<string>(r,"ClassName")??"",
            OrderNo=G.G<int>(r,"OrderNo"), IsActive=G.G<bool>(r,"IsActive"),
            StudentCount=G.G<int>(r,"StudentCount")
        });

    public int SaveClass(Class m) => db.ExecOut("sp_SaveClass", new() {
        {"@ClassId",m.ClassId}, {"@ClassName",m.ClassName},
        {"@OrderNo",m.OrderNo}, {"@IsActive",m.IsActive}
    }, "@NewId");

    public void DeleteClass(int id) => db.Exec("sp_DeleteClass", new(){{"@ClassId",id}});

    // ── Section ───────────────────────────────────────────────
    public List<Section> GetSections() => db.Read("sp_GetSections", new(),
        r => new Section {
            SectionId=G.G<int>(r,"SectionId"), SectionName=G.G<string>(r,"SectionName")??"",
            IsActive=G.G<bool>(r,"IsActive"), StudentCount=G.G<int>(r,"StudentCount")
        });

    public int SaveSection(Section m) => db.ExecOut("sp_SaveSection", new() {
        {"@SectionId",m.SectionId}, {"@SectionName",m.SectionName}, {"@IsActive",m.IsActive}
    }, "@NewId");

    public void DeleteSection(int id) => db.Exec("sp_DeleteSection", new(){{"@SectionId",id}});

    // ── Batch ─────────────────────────────────────────────────
    public List<Batch> GetBatches() => db.Read("sp_GetBatches", new(),
        r => new Batch {
            BatchId=G.G<int>(r,"BatchId"), BatchName=G.G<string>(r,"BatchName")??"",
            IsActive=G.G<bool>(r,"IsActive"), StudentCount=G.G<int>(r,"StudentCount")
        });

    public int SaveBatch(Batch m) => db.ExecOut("sp_SaveBatch", new() {
        {"@BatchId",m.BatchId}, {"@BatchName",m.BatchName}, {"@IsActive",m.IsActive}
    }, "@NewId");

    public void DeleteBatch(int id) => db.Exec("sp_DeleteBatch", new(){{"@BatchId",id}});

    // ── Fee Type ──────────────────────────────────────────────
    public List<FeeType> GetFeeTypes() => db.Read("sp_GetFeeTypes", new(),
        r => new FeeType {
            FeeTypeId      = G.G<int>(r,"FeeTypeId"),
            TypeName       = G.G<string>(r,"TypeName")??"",
            Description    = G.G<string>(r,"Description"),
            IsActive       = G.G<bool>(r,"IsActive"),
            StructureCount = G.G<int>(r,"StructureCount"),
            PaymentCount   = G.G<int>(r,"PaymentCount")
        });

    public int SaveFeeType(FeeType m) => db.ExecOut("sp_SaveFeeType", new() {
        {"@FeeTypeId",m.FeeTypeId}, {"@TypeName",m.TypeName},
        {"@Description",m.Description}, {"@IsActive",m.IsActive}
    }, "@NewId");

    public void DeleteFeeType(int id) => db.Exec("sp_DeleteFeeType", new(){{"@FeeTypeId",id}});

    // ── Subject ───────────────────────────────────────────────
    public List<Subject> GetSubjects() => db.Read("sp_GetSubjects", new(),
        r => new Subject {
            SubjectId   = G.G<int>(r,"SubjectId"),
            SubjectName = G.G<string>(r,"SubjectName")??"",
            SubjectCode = G.G<string>(r,"SubjectCode"),
            ClassId     = G.G<int>(r,"ClassId"),
            ClassName   = G.G<string>(r,"ClassName"),
            MaxMarks    = G.G<int>(r,"MaxMarks"),
            PassMarks   = G.G<int>(r,"PassMarks"),
            IsActive    = G.G<bool>(r,"IsActive"),
            UsageCount  = G.G<int>(r,"UsageCount")
        });

    public int SaveSubject(Subject m) => db.ExecOut("sp_SaveSubject", new() {
        {"@SubjectId",m.SubjectId}, {"@SubjectName",m.SubjectName}, {"@SubjectCode",m.SubjectCode},
        {"@ClassId",m.ClassId}, {"@MaxMarks",m.MaxMarks}, {"@PassMarks",m.PassMarks}, {"@IsActive",m.IsActive}
    }, "@NewId");

    public void DeleteSubject(int id) => db.Exec("sp_DeleteSubject", new(){{"@SubjectId",id}});
}
