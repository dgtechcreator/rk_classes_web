using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class UserMgmtService(UserMgmtRepo repo)
{
    public List<AppUser>           GetAll()                                          => repo.GetAll();
    public AppUser?                GetById(int id)                                   => repo.GetById(id);
    public int                     Save(AppUser u, int by)                           => repo.Save(u, by);
    public void                    SavePermission(int uid, int mid, bool v, bool e)  => repo.SavePermission(uid, mid, v, e);
    public Dictionary<string,bool> GetPermissions(int userId)                        => repo.GetPermissions(userId);
    public (bool ok, string msg)   ChangePassword(int uid, string o, string n)       => repo.ChangePassword(uid, o, n);
    public List<Role>              GetRoles()                                         => repo.GetRoles();
    public List<Module>            GetAllModules()                                    => repo.GetAllModules();
}
