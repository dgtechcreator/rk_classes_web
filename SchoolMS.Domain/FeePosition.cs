namespace SchoolMS.Domain;

/// A student's fee position — one definition used by the staff API, parent API and web pages so they can
/// never disagree:  NetTotal = base fee + additional charges - discount ;  Balance = NetTotal - paid.
public class FeePosition
{
    public decimal BaseFee { get; set; }
    public decimal AdditionalCharges { get; set; }   // kept in payment Remarks as "Additional Charges: ₹x"
    public decimal Discount { get; set; }           // payment discounts + AgreedDiscount
    public decimal AgreedDiscount { get; set; }     // the part agreed at admission (already inside Discount)
    public decimal Paid { get; set; }
    /// Fees of EARLIER academic years (9th / 10th / 11th for a student now in 12th). Already inside BaseFee.
    public decimal PreviousYearsFee { get; set; }
    /// How much of the Balance is still owed from earlier years: money received is counted against the oldest fees first.
    public decimal PreviousYearsDue => Math.Min(Balance, Math.Max(0, PreviousYearsFee - Paid - Discount));
    public decimal NetTotal => BaseFee + AdditionalCharges - Discount;
    public decimal Balance => Math.Max(0, NetTotal - Paid);
    public DateTime? DueDate { get; set; }
    /// Next installment date shown to parents: the Due Date entered on the latest payment, else the fee-structure due day. Null when nothing is set.
    public DateTime? InstallmentDate { get; set; }
    public List<FeeStructure> Structures { get; set; } = new();
    public List<FeePayment> History { get; set; } = new();
}
