using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class UserMgmtService(UserMgmtRepo repo)
{
    public List<AppUser>           GetAll()                                          => repo.GetAll().Where(u => !u.IsDeleted).ToList();
    public List<AppUser>           GetDeleted()                                      => repo.GetAll().Where(u => u.IsDeleted).OrderByDescending(u => u.DeletedAt).ToList();
    public void                    Delete(int id, int by)                            => repo.SoftDelete(id, by);
    public void                    Restore(int id)                                   => repo.Restore(id);
    public void                    SetGender(int id, string? gender)                 => repo.SetGender(id, gender);
    public void                    SetActive(int id, bool active)                    => repo.SetActive(id, active);
    public void                    ClearPermissions(int userId)                      => repo.ClearPermissions(userId);
    public void                    RemoveNewUser(int userId)                         => repo.RemoveNewUser(userId);
    public AppUser?                GetById(int id)                                   => repo.GetById(id);
    public int                     Save(AppUser u, int by)                           => repo.Save(u, by);
    public void                    SavePermission(int uid, int mid, bool v, bool e)  => repo.SavePermission(uid, mid, v, e);
    public Dictionary<string,bool> GetPermissions(int userId)                        => repo.GetPermissions(userId);
    public (bool ok, string msg)   ChangePassword(int uid, string o, string n)       => repo.ChangePassword(uid, o, n);
    public List<Role>              GetRoles()                                         => repo.GetRoles();
    public List<Module>            GetAllModules()                                    => repo.GetAllModules();
}
