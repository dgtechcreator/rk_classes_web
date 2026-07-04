using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class LookupRepo(CommonConnectivity db)
{
    public List<AcademicYear> GetYears()  => db.Sql("SELECT * FROM AcademicYears WHERE IsActive=1 ORDER BY YearId DESC",
        r => new AcademicYear { YearId=G.G<int>(r,"YearId"), YearName=G.G<string>(r,"YearName")??"", IsCurrent=G.G<bool>(r,"IsCurrent") });
    public List<Class> GetClasses() => db.Sql("SELECT * FROM Classes WHERE IsActive=1 ORDER BY OrderNo",
        r => new Class { ClassId=G.G<int>(r,"ClassId"), ClassName=G.G<string>(r,"ClassName")??"", OrderNo=G.G<int>(r,"OrderNo") });
    public List<Section> GetSections() => db.Sql("SELECT * FROM Sections WHERE IsActive=1",
        r => new Section { SectionId=G.G<int>(r,"SectionId"), SectionName=G.G<string>(r,"SectionName")??"" });
    public List<Batch> GetBatches() => db.Sql("SELECT * FROM Batches WHERE IsActive=1",
        r => new Batch { BatchId=G.G<int>(r,"BatchId"), BatchName=G.G<string>(r,"BatchName")??"" });
    public List<FeeType> GetFeeTypes() => db.Sql("SELECT * FROM FeeTypes WHERE IsActive=1",
        r => new FeeType { FeeTypeId=G.G<int>(r,"FeeTypeId"), TypeName=G.G<string>(r,"TypeName")??"" });
    public List<ExpenseCat> GetExpCats() => db.Sql("SELECT * FROM ExpenseCategories WHERE IsActive=1",
        r => new ExpenseCat { CategoryId=G.G<int>(r,"CategoryId"), CategoryName=G.G<string>(r,"CategoryName")??"" });
    public List<Subject> GetSubjects(int classId) => db.Sql($"SELECT * FROM Subjects WHERE ClassId={classId} AND IsActive=1",
        r => new Subject { SubjectId=G.G<int>(r,"SubjectId"), SubjectName=G.G<string>(r,"SubjectName")??"", SubjectCode=G.G<string>(r,"SubjectCode"), ClassId=classId, MaxMarks=G.G<int>(r,"MaxMarks"), PassMarks=G.G<int>(r,"PassMarks") });
    public List<Exam> GetExams() => db.Sql("SELECT e.*,c.ClassName FROM Exams e LEFT JOIN Classes c ON c.ClassId=e.ClassId WHERE e.IsActive=1 ORDER BY e.ExamId",
        r => new Exam { ExamId=G.G<int>(r,"ExamId"), ExamName=G.G<string>(r,"ExamName")??"", ClassId=G.G<int>(r,"ClassId"), ClassName=G.G<string>(r,"ClassName"), AcademicYearId=G.G<int>(r,"AcademicYearId") });
    public DashboardStats GetDashStats()
    {
        var l = db.Read("sp_GetDashboardStats", new(), r => new DashboardStats {
            TotalStudents=G.G<int>(r,"TotalStudents"), NewToday=G.G<int>(r,"NewToday"),
            PresentToday=G.G<int>(r,"PresentToday"), AbsentToday=G.G<int>(r,"AbsentToday"),
            FeesThisMonth=G.G<decimal>(r,"FeesThisMonth"), ExpensesThisMonth=G.G<decimal>(r,"ExpensesThisMonth"),
            Class1Count=G.G<int>(r,"Class1Count"), Class2Count=G.G<int>(r,"Class2Count"),
            Class3Count=G.G<int>(r,"Class3Count"), TotalStaff=G.G<int>(r,"TotalStaff")
        });
        var stats = l.FirstOrDefault() ?? new DashboardStats();

        // Populate dynamic class strengths
        var colors = new[] { "#6d28d9", "#db2777", "#2563eb", "#059669", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899" };
        var classStrengths = db.Sql(@"
            SELECT c.ClassId, c.ClassName, c.OrderNo, COUNT(DISTINCT s.StudentId) as StudentCount
            FROM Classes c
            LEFT JOIN Students s ON s.ClassId=c.ClassId AND ISNULL(s.Status,'Active') <> 'Deleted'
            WHERE c.IsActive=1
            GROUP BY c.ClassId, c.ClassName, c.OrderNo
            ORDER BY c.OrderNo",
            r => new ClassStrengthStat {
                ClassName = G.G<string>(r,"ClassName")??"",
                StudentCount = G.G<int>(r,"StudentCount")
            });

        for (int i = 0; i < classStrengths.Count; i++)
        {
            classStrengths[i].Color = colors[i % colors.Length];
        }

        stats.ClassStrengths = classStrengths.Where(c => c.StudentCount > 0).ToList();

        // Populate all classes
        stats.AllClasses = db.Sql(@"
            SELECT c.ClassId, c.ClassName, c.OrderNo, c.IsActive, COUNT(DISTINCT s.StudentId) as StudentCount
            FROM Classes c
            LEFT JOIN Students s ON s.ClassId=c.ClassId AND ISNULL(s.Status,'Active') <> 'Deleted'
            WHERE c.IsActive=1
            GROUP BY c.ClassId, c.ClassName, c.OrderNo, c.IsActive
            ORDER BY c.OrderNo",
            r => new ClassAllStat {
                ClassId = G.G<int>(r,"ClassId"),
                ClassName = G.G<string>(r,"ClassName")??"",
                OrderNo = G.G<int>(r,"OrderNo"),
                StudentCount = G.G<int>(r,"StudentCount"),
                IsActive = G.G<bool>(r,"IsActive")
            });

        // Calculate overall fees summary - Direct from database
        var feesSummary = db.Sql(@"
            SELECT
                ISNULL(SUM(fs.Amount), 0) as TotalFees,
                ISNULL((SELECT SUM(NetAmount) FROM FeePayments WHERE IsDeleted=0), 0) as TotalCollected,
                ISNULL((SELECT SUM(Discount) FROM FeePayments WHERE IsDeleted=0), 0) as TotalDiscount
            FROM FeeStructure fs
            WHERE fs.IsActive=1",
            r => new {
                TotalFees = G.G<decimal>(r,"TotalFees"),
                TotalCollected = G.G<decimal>(r,"TotalCollected"),
                TotalDiscount = G.G<decimal>(r,"TotalDiscount")
            }).FirstOrDefault();

        if (feesSummary != null)
        {
            stats.TotalFeesOverall = feesSummary.TotalFees;
            stats.TotalCollectedOverall = feesSummary.TotalCollected;
            stats.TotalDiscountOverall = feesSummary.TotalDiscount;
            stats.BalanceOverall = Math.Max(0, stats.TotalFeesOverall - stats.TotalCollectedOverall - stats.TotalDiscountOverall);
        }

        return stats;
    }
    public List<Student> GetStudentDropdown() => db.Sql("SELECT StudentId,FullName,AdmissionNo,ClassId FROM Students WHERE Status='Active' ORDER BY FullName",
        r => new Student { StudentId=G.G<int>(r,"StudentId"), FullName=G.G<string>(r,"FullName")??"", AdmissionNo=G.G<string>(r,"AdmissionNo")??"", ClassId=G.G<int?>(r,"ClassId") });
}
