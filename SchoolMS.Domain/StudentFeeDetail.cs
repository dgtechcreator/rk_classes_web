namespace SchoolMS.Domain;

public class StudentFeeDetail
{
    public string StudentName { get; set; } = "";
    public decimal TotalFees { get; set; }
    public decimal Discount { get; set; }
    public decimal Collected { get; set; }
    public decimal Balance { get; set; }
}
