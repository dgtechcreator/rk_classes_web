namespace SchoolMS.Domain;

public class AcademicYear
{
    public int    YearId{get;set;}
    public string YearName{get;set;}="";
    public bool   IsCurrent{get;set;}
    public bool   IsActive{get;set;}=true;
    public int    StudentCount{get;set;}
}

public class Class
{
    public int    ClassId{get;set;}
    public string ClassName{get;set;}="";
    public int    OrderNo{get;set;}
    public bool   IsActive{get;set;}=true;
    public int    StudentCount{get;set;}
}

public class Section
{
    public int    SectionId{get;set;}
    public string SectionName{get;set;}="";
    public bool   IsActive{get;set;}=true;
    public int    StudentCount{get;set;}
}

public class Batch
{
    public int    BatchId{get;set;}
    public string BatchName{get;set;}="";
    public bool   IsActive{get;set;}=true;
    public int    StudentCount{get;set;}
}

public class FeeType
{
    public int     FeeTypeId{get;set;}
    public string  TypeName{get;set;}="";
    public string? Description{get;set;}
    public bool    IsActive{get;set;}=true;
    public int     StructureCount{get;set;}
    public int     PaymentCount{get;set;}
}

public class ExpenseCat { public int CategoryId{get;set;} public string CategoryName{get;set;}=""; }

public class Subject
{
    public int     SubjectId{get;set;}
    public string  SubjectName{get;set;}="";
    public string? SubjectCode{get;set;}
    public int     ClassId{get;set;}
    public string? ClassName{get;set;}
    public int     MaxMarks{get;set;}=100;
    public int     PassMarks{get;set;}=35;
    public bool    IsActive{get;set;}=true;
    public int     UsageCount{get;set;}
}

public class Exam
{
    public int      ExamId{get;set;}
    public string   ExamName{get;set;}="";
    public int      ClassId{get;set;}
    public string?  ClassName{get;set;}
    public int      AcademicYearId{get;set;}
    public string?  YearName{get;set;}
    public DateTime? TestDate{get;set;}
    public int      EntryCount{get;set;}
}
