namespace SchoolMS.Domain;

/// One thing that is wrong or missing for a student (current academic year, active students only).
/// The Data Health page lists these live, so anything that is fixed disappears and anything new shows up.
public class DataIssue
{
    public int     StudentId{get;set;}
    public string  AdmissionNo{get;set;} = "";
    public string  FullName{get;set;} = "";
    public string  ClassName{get;set;} = "";
    public string  SectionName{get;set;} = "";
    public string  BatchName{get;set;} = "";
    public string  Code{get;set;} = "";        // stable key, e.g. NO_FEE_HEAD
    public string  Group{get;set;} = "";       // Fees | Student details
    public string  Severity{get;set;} = "";    // High | Medium | Low | Info
    public string  Title{get;set;} = "";
    public string  Detail{get;set;} = "";
    public string  Action{get;set;} = "";
    public bool    CanApplyFee{get;set;}       // "Apply fee" button (fee head exists, row just missing)
}

/// Raw per-student numbers used to derive the over-paid / additional-charges checks.
public class StudentFeeNumbers
{
    public int     StudentId{get;set;}
    public string  AdmissionNo{get;set;} = "";
    public string  FullName{get;set;} = "";
    public string  ClassName{get;set;} = "";
    public string  SectionName{get;set;} = "";
    public string  BatchName{get;set;} = "";
    public decimal Fee{get;set;}
    public decimal Paid{get;set;}
    public decimal Discount{get;set;}
    public decimal Charges{get;set;}
}

public class DataHealthReport
{
    public int StudentsChecked{get;set;}
    public List<DataIssue> Issues{get;set;} = new();
}
