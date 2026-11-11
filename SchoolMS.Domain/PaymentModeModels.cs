namespace SchoolMS.Domain;

/// <summary>A payment mode (Cash, Online Transfer, Cheque ...). One master list feeds the Expense, Fee and Teacher Payment forms, web and app.</summary>
public class PaymentMode
{
    public int    ModeId { get; set; }
    public string ModeName { get; set; } = "";
    public bool   IsActive { get; set; } = true;
    public int    UsageCount { get; set; }
}
