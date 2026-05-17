namespace SchoolMS.Domain;

public class Expense
{
    public int      ExpenseId{get;set;}
    public string   ExpenseNo{get;set;}="";
    public int?     CategoryId{get;set;}
    public string?  CategoryName{get;set;}
    public string   Title{get;set;}="";
    public string?  Description{get;set;}
    public decimal  Amount{get;set;}
    public DateTime ExpenseDate{get;set;}=DateTime.Today;
    public string   PaymentMode{get;set;}="Cash";
    public string?  BillNo{get;set;}
    public string?  VendorName{get;set;}
    public string?  EnteredByName{get;set;}
    public DateTime CreatedAt{get;set;}
}
