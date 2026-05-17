namespace SchoolMS.Domain;

public class TestMark
{
    public int      MarkId{get;set;}
    public int      StudentId{get;set;}
    public string   FullName{get;set;}="";
    public string   AdmissionNo{get;set;}="";
    public string?  RollNo{get;set;}
    public int      ExamId{get;set;}
    public string?  ExamName{get;set;}
    public DateTime? TestDate{get;set;}
    public int      SubjectId{get;set;}
    public string?  SubjectName{get;set;}
    public string?  SubjectCode{get;set;}
    public int      SubjectMaxMarks{get;set;}=100;
    public int      PassMarks{get;set;}=35;
    public decimal? MarksObtained{get;set;}
    public int      MaxMarks{get;set;}=100;
    public string?  Grade{get;set;}
    public string?  ClassName{get;set;}
    public string?  SectionName{get;set;}
    public string?  BatchName{get;set;}
    public string?  YearName{get;set;}
}

public class StudentMarkRow
{
    public int      StudentId{get;set;}
    public string   FullName{get;set;}="";
    public string   AdmissionNo{get;set;}="";
    public string?  RollNo{get;set;}
    public string?  ClassName{get;set;}
    public string?  SectionName{get;set;}
    public string?  BatchName{get;set;}
    public decimal? MarksObtained{get;set;}
    public int      MaxMarks{get;set;}=30;
    public string?  Grade{get;set;}
    public bool     IsAbsent{get;set;}=false;
    public DateTime? TestDate{get;set;}
}
