namespace SchoolMS.Domain;

public class ParentAccount
{
    public int       ParentId  { get; set; }
    public string    Phone     { get; set; } = "";
    public string    Password  { get; set; } = "";
    public string?   FullName  { get; set; }
    public bool      IsActive  { get; set; } = true;
    public DateTime  CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}
