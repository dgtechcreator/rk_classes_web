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
            PermCount = G.G<int>(r,"PermCount"),
            Gender    = G.G<string>(r,"Gender"),
            IsDeleted = G.G<bool>(r,"IsDeleted"),
            DeletedAt = G.G<DateTime?>(r,"DeletedAt")
        });

    // Soft delete: the row stays, IsActive=0 also blocks login (sp_ValidateUser filters IsActive=1).
    public void SoftDelete(int id, int by)
        => db.ExecSql("UPDATE Users SET IsDeleted=1, IsActive=0, DeletedAt=GETDATE(), DeletedBy=@By WHERE UserId=@Id",
            new() {{"@Id",id},{"@By",by}});

    /// <summary>Male / Female / null. Column comes from SQL/63; a missing column must never break saving a user.</summary>
    public void SetGender(int id, string? gender)
    {
        var g = string.IsNullOrWhiteSpace(gender) ? null : (gender.Trim() is "Male" or "Female" ? gender.Trim() : null);
        try { db.ExecSql("UPDATE Users SET Gender=@G WHERE UserId=@Id", new() {{"@Id",id},{"@G",g}}); }
        catch (Microsoft.Data.SqlClient.SqlException) { /* SQL/63 not run yet */ }
    }

    public void SetActive(int id, bool active)
        => db.ExecSql("UPDATE Users SET IsActive=@A WHERE UserId=@Id", new() {{"@Id",id},{"@A",active}});

    public void ClearPermissions(int userId)
        => db.ExecSql("DELETE FROM UserPermissions WHERE UserId=@Id", new() {{"@Id",userId}});

    /// <summary>Hard delete of a user row that was just created and whose follow-up step failed (rollback only).</summary>
    public void RemoveNewUser(int userId)
    {
        db.ExecSql("DELETE FROM UserPermissions WHERE UserId=@Id", new() {{"@Id",userId}});
        db.ExecSql("DELETE FROM Users WHERE UserId=@Id", new() {{"@Id",userId}});
    }

    public void Restore(int id)
        => db.ExecSql("UPDATE Users SET IsDeleted=0, IsActive=1, DeletedAt=NULL, DeletedBy=NULL WHERE UserId=@Id",
            new() {{"@Id",id}});

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
                IsActive     = G.G<bool>(r,"IsActive"),
                Gender       = G.G<string>(r,"Gender")
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
        // A module that IS an edit right ("Add/Edit Faculty" = faculty_edit) is granted when View OR Edit is ticked.
        // The web menu asks for faculty_edit_edit, the mobile API for faculty_edit - make both agree.
        foreach (var row in rows.Where(r => r.key.EndsWith("_edit"))) {
            var granted = row.view || row.edit;
            d[row.key] = granted;
            d[row.key+"_edit"] = granted;
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
