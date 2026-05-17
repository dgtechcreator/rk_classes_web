using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class AuthRepo(CommonConnectivity db)
{
    public User? Validate(string username)
    {
        var l = db.Read("sp_ValidateUser", new() { { "@Username", username } }, r => new User {
            UserId=G.G<int>(r,"UserId"), FullName=G.G<string>(r,"FullName")??"", Username=G.G<string>(r,"Username")??"",
            PasswordHash=G.G<string>(r,"PasswordHash")??"", RoleId=G.G<int>(r,"RoleId"), RoleName=G.G<string>(r,"RoleName")??"",
            Email=G.G<string>(r,"Email"), Phone=G.G<string>(r,"Phone"), IsActive=G.G<bool>(r,"IsActive")
        });
        return l.FirstOrDefault();
    }
    public void UpdateLogin(int uid) => db.Exec("sp_UpdateLastLogin", new() { { "@UserId", uid } });
}
