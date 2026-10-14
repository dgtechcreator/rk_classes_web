using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// Class Representative (CR / Monitor) management. A CR is an ordinary staff login (role "Class Representative")
/// tied to one student; the web + API guards limit that login to the student's own class.
/// </summary>
public class ClassRepService(ClassRepRepo repo, UserMgmtService users, StudentService students)
{
    /// <summary>The only modules a CR login may ever hold (everything else is blocked server-side as well).</summary>
    public static readonly string[] AllowedModuleKeys = { "attendance_entry", "teacher_attendance", "marks_entry" };

    public List<ClassRep> GetAll()
    {
        var list = repo.GetAll();
        var rights = repo.GetRights();
        foreach (var cr in list) cr.Rights = OrderedRights(rights.TryGetValue(cr.UserId, out var k) ? k : new());
        return list;
    }

    public ClassRep? GetById(int crId)
    {
        var cr = repo.GetById(crId);
        if (cr != null) cr.Rights = OrderedRights(repo.GetRights().TryGetValue(cr.UserId, out var k) ? k : new());
        return cr;
    }

    static List<string> OrderedRights(IEnumerable<string> keys) => AllowedModuleKeys.Where(keys.Contains).ToList();

    /// <summary>null = every CR right; otherwise only the allowed keys that were asked for.</summary>
    static List<string> NormalizeRights(IEnumerable<string>? rights)
        => rights == null ? AllowedModuleKeys.ToList() : AllowedModuleKeys.Where(rights.Contains).ToList();
    public ClassRep? GetByUserId(int userId) => repo.GetByUserId(userId);
    public int? RoleId() => repo.RoleId();

    /// <summary>Scope for a working CR login; null for everyone else.</summary>
    public CrScope? GetScope(int userId)
    {
        var cr = repo.GetByUserId(userId);
        return cr != null && cr.IsWorking ? ToScope(cr) : null;
    }

    public static CrScope? ToScope(ClassRep cr)
        => cr.ClassId == null ? null : new CrScope {
            CrId = cr.CrId, UserId = cr.UserId, StudentId = cr.StudentId, StudentName = cr.StudentName,
            ClassId = cr.ClassId.Value, ClassName = cr.ClassName,
            SectionId = cr.SectionId, SectionName = cr.SectionName,
            BatchId = cr.BatchId, BatchName = cr.BatchName, LimitToBatch = cr.LimitToBatch,
        };

    /// <summary>Makes the student a CR: creates the login, gives the two CR modules, links both.</summary>
    public (bool ok, string message, int crId) Assign(int studentId, string username, string password, bool limitToBatch, int by, IEnumerable<string>? rights = null)
    {
        var keys = NormalizeRights(rights);
        if (keys.Count == 0) return (false, "Give the Class Representative at least one right.", 0);
        username = (username ?? "").Trim().ToLowerInvariant();
        password = (password ?? "").Trim();
        if (username.Length < 3 || username.Any(char.IsWhiteSpace)) return (false, "Username must be at least 3 characters, without spaces.", 0);
        if (password.Length < 4) return (false, "Password must be at least 4 characters.", 0);

        var student = students.GetById(studentId);
        if (student == null) return (false, "Student not found.", 0);
        if (!string.Equals(student.Status, "Active", StringComparison.OrdinalIgnoreCase)) return (false, "Only an Active student can be made CR.", 0);
        if (student.ClassId == null) return (false, "This student has no class - set the class first.", 0);
        if (repo.GetByStudentId(studentId) != null) return (false, "This student is already a CR. Edit the existing CR instead.", 0);

        var roleId = repo.RoleId();
        if (roleId == null) return (false, "Class Representative role is missing. Run SQL/61_ClassRepresentative.sql first.", 0);

        var userId = users.Save(new AppUser {
            FullName = student.DisplayName, Username = username, PasswordHash = password,
            RoleId = roleId.Value, Phone = student.Phone, IsActive = true,
        }, by);
        if (userId <= 0) return (false, $"Username '{username}' is already used. Choose another one.", 0);

        try
        {
            GrantCrModules(userId, keys);
            var crId = repo.Insert(userId, studentId, limitToBatch && student.BatchId != null, by);
            return (true, $"{student.DisplayName} is now Class Representative.", crId);
        }
        catch
        {
            users.RemoveNewUser(userId);   // no half-created login left behind
            throw;
        }
    }

    /// <summary>Change batch limit / on-off and optionally reset the login password.</summary>
    public (bool ok, string message) Update(int crId, bool limitToBatch, bool isActive, string? newPassword, int by, IEnumerable<string>? rights = null)
    {
        var keys = rights == null ? null : NormalizeRights(rights);
        if (keys is { Count: 0 }) return (false, "Give the Class Representative at least one right.");
        var cr = repo.GetById(crId);
        if (cr == null) return (false, "Class Representative not found.");
        var pwd = (newPassword ?? "").Trim();
        if (pwd.Length > 0 && pwd.Length < 4) return (false, "Password must be at least 4 characters.");

        repo.Update(crId, limitToBatch && cr.BatchId != null, isActive);
        if (pwd.Length > 0)
        {
            var u = users.GetById(cr.UserId);
            if (u != null) { u.PasswordHash = pwd; u.IsActive = isActive; users.Save(u, by); }
        }
        users.SetActive(cr.UserId, isActive);
        if (keys != null) GrantCrModules(cr.UserId, keys);   // rights only change when the admin sent them
        return (true, isActive ? "Class Representative updated." : "Class Representative switched off.");
    }

    void GrantCrModules(int userId, List<string> keys)
    {
        users.ClearPermissions(userId);
        foreach (var m in users.GetAllModules().Where(m => keys.Contains(m.ModuleKey)))
            users.SavePermission(userId, m.ModuleId, true, true);
    }
}
