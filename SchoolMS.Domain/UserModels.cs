namespace SchoolMS.Domain;

public class Role { public int RoleId{get;set;} public string RoleName{get;set;}=""; }

public class Module
{
    public int    ModuleId{get;set;}
    public string ModuleName{get;set;}="";
    public string ModuleKey{get;set;}="";
    public string? Icon{get;set;}
    public string? GroupName{get;set;}
    public int    OrderNo{get;set;}
    public bool   CanView{get;set;}
    public bool   CanEdit{get;set;}
}

public class AppUser
{
    public int     UserId{get;set;}
    public string  FullName{get;set;}="";
    public string  Username{get;set;}="";
    public string  PasswordHash{get;set;}="";
    public int     RoleId{get;set;}
    public string  RoleName{get;set;}="";
    public string? Email{get;set;}
    public string? Phone{get;set;}
    public bool    IsActive{get;set;}=true;
    public int     PermCount{get;set;}
    public string? Gender{get;set;}
    public string? ProfilePicPath{get;set;}
    public int?    FacultyId{get;set;}   // explicit link to the teacher's Faculty profile (null = match automatically by phone / name)
    public bool    IsDeleted{get;set;}
    public DateTime? DeletedAt{get;set;}
    public List<Module> Permissions{get;set;}=new();
}

public class User
{
    public int    UserId{get;set;}
    public string FullName{get;set;}="";
    public string Username{get;set;}="";
    public string PasswordHash{get;set;}="";
    public int    RoleId{get;set;}
    public string RoleName{get;set;}="";
    public string? Email{get;set;}
    public string? Phone{get;set;}
    public bool   IsActive{get;set;}=true;
}
