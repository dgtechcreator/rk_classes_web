using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class UserMgmtRepo(CommonConnectivity db)
{
    public List<AppUser> GetAll()
        => db.Read("sp_GetUsers", new(), r => new AppUser {
            UserId    = G.G<int>(r,"UserId"),
            FullName  = G.G<string>(r,"FullName")??"",
            Username  = G.G<string>(r,"Username")??"",
            RoleId    = G.G<int>(r,"RoleId"),
            RoleName  = G.G<string>(r,"RoleName")??"",
            Email     = G.G<string>(r,"Email"),
            Phone     = G.G<string>(r,"Phone"),
            IsActive  = G.G<bool>(r,"IsActive"),
            PermCount = G.G<int>(r,"PermCount")
        });

    public AppUser? GetById(int id)
    {
        var users = db.Read("sp_GetUserById", new(){{"@UserId",id}},
            r => new AppUser {
                UserId       = G.G<int>(r,"UserId"),
                FullName     = G.G<string>(r,"FullName")??"",
                Username     = G.G<string>(r,"Username")??"",
                PasswordHash = G.G<string>(r,"PasswordHash")??"",
                RoleId       = G.G<int>(r,"RoleId"),
                RoleName     = G.G<string>(r,"RoleName")??"",
                Email        = G.G<string>(r,"Email"),
                Phone        = G.G<string>(r,"Phone"),
                IsActive     = G.G<bool>(r,"IsActive")
            });
        var user = users.FirstOrDefault();
        if (user == null) return null;

        var mods = db.Sql($@"
            SELECT m.*, ISNULL(up.CanView,0) AS CanView, ISNULL(up.CanEdit,0) AS CanEdit
            FROM Modules m
            LEFT JOIN UserPermissions up ON up.ModuleId=m.ModuleId AND up.UserId={id}
            WHERE m.IsActive=1
            ORDER BY m.OrderNo",
            r => new Module {
                ModuleId  = G.G<int>(r,"ModuleId"),
                ModuleName= G.G<string>(r,"ModuleName")??"",
                ModuleKey = G.G<string>(r,"ModuleKey")??"",
                Icon      = G.G<string>(r,"Icon"),
                GroupName = G.G<string>(r,"GroupName"),
                OrderNo   = G.G<int>(r,"OrderNo"),
                CanView   = G.G<bool>(r,"CanView"),
                CanEdit   = G.G<bool>(r,"CanEdit")
            });

        user.Permissions = mods;
        return user;
    }

    public int Save(AppUser u, int by)
        => db.ExecOut("sp_SaveUser", new() {
            {"@UserId",u.UserId}, {"@FullName",u.FullName},
            {"@Username",u.Username}, {"@Password",u.PasswordHash},
            {"@RoleId",u.RoleId}, {"@Email",u.Email},
            {"@Phone",u.Phone}, {"@IsActive",u.IsActive}, {"@CreatedBy",by}
        }, "@NewId");

    public void SavePermission(int userId, int moduleId, bool canView, bool canEdit)
        => db.Exec("sp_SaveUserPermissions", new() {
            {"@UserId",userId}, {"@ModuleId",moduleId},
            {"@CanView",canView}, {"@CanEdit",canEdit}
        });

    public Dictionary<string,bool> GetPermissions(int userId)
    {
        var d = new Dictionary<string,bool>();
        var rows = db.Read("sp_GetUserPermissions", new(){{"@UserId",userId}},
            r => new { key=G.G<string>(r,"ModuleKey")??"", view=G.G<bool>(r,"CanView"), edit=G.G<bool>(r,"CanEdit") });
        foreach (var row in rows) {
            d[row.key] = row.view;
            d[row.key+"_edit"] = row.edit;
        }
        return d;
    }

    public (bool ok, string msg) ChangePassword(int userId, string oldPwd, string newPwd)
    {
        var op = new SqlParameter("@Result", System.Data.SqlDbType.NVarChar, 50)
                 { Direction = System.Data.ParameterDirection.Output };
        db.ExecOut("sp_ChangePassword", new(){{"@UserId",userId},{"@OldPassword",oldPwd},{"@NewPassword",newPwd}},"@Result");
        var res = op.Value?.ToString() ?? "";
        return (res=="OK", res=="OK"?"Password changed.":"Current password is incorrect.");
    }

    public List<Role> GetRoles()
        => db.Sql("SELECT * FROM Roles ORDER BY RoleId",
            r => new Role { RoleId=G.G<int>(r,"RoleId"), RoleName=G.G<string>(r,"RoleName")??"" });

    public List<Module> GetAllModules()
        => db.Sql("SELECT * FROM Modules WHERE IsActive=1 ORDER BY OrderNo",
            r => new Module {
                ModuleId  = G.G<int>(r,"ModuleId"),    ModuleName=G.G<string>(r,"ModuleName")??"",
                ModuleKey = G.G<string>(r,"ModuleKey")??"", Icon=G.G<string>(r,"Icon"),
                GroupName = G.G<string>(r,"GroupName"), OrderNo=G.G<int>(r,"OrderNo")
            });
}
