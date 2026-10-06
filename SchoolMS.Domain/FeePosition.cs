namespace SchoolMS.Domain;

/// A student's fee position — one definition used by the staff API, parent API and web pages so they can
/// never disagree:  NetTotal = base fee + additional charges - discount ;  Balance = NetTotal - paid.
public class FeePosition
{
    public decimal BaseFee { get; set; }
    public decimal AdditionalCharges { get; set; }   // kept in payment Remarks as "Additional Charges: ₹x"
    public decimal Discount { get; set; }
    public decimal Paid { get; set; }
    public decimal NetTotal => BaseFee + AdditionalCharges - Discount;
    public decimal Balance => Math.Max(0, NetTotal - Paid);
    public DateTime? DueDate { get; set; }
    public List<FeeStructure> Structures { get; set; } = new();
    public List<FeePayment> History { get; set; } = new();
}
