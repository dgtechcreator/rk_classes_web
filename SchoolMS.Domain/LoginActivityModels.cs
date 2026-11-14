namespace SchoolMS.Domain;

/// <summary>One sign-in attempt (success or failure) of a staff login or a student / parent login, from the website or the app.</summary>
public class LoginActivityEntry
{
    public int       LoginId     { get; set; }
    /// <summary>"staff" (Users table: admin, teacher, accountant...) or "parent" (ParentAccounts: student / parent login).</summary>
    public string    AccountType { get; set; } = "staff";
    public int?      AccountId   { get; set; }
    /// <summary>What was typed as the login id (username / phone). Never the password.</summary>
    public string    LoginName   { get; set; } = "";
    public string?   DisplayName { get; set; }
    public string?   RoleName    { get; set; }
    public bool      Success     { get; set; }
    /// <summary>"app" or "web".</summary>
    public string?   Platform    { get; set; }
    /// <summary>"Samsung SM-A515F · Android 13" (app) / "Chrome 126 · Windows 10" (website).</summary>
    public string?   Device      { get; set; }
    public string?   AppVersion  { get; set; }
    public string?   IpAddress   { get; set; }
    public DateTime  LoggedAt    { get; set; }
}

/// <summary>A device / browser an account has signed in from, with how often and when it was last used.</summary>
public class DeviceUse
{
    public string    Device   { get; set; } = "";
    public string?   Platform { get; set; }
    public int       Logins   { get; set; }
    public DateTime  LastUsed { get; set; }
    public string?   LastIp   { get; set; }
}
