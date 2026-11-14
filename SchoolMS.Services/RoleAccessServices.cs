using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public record PortalSection(string Key, string Label, string Icon, string Description);

/// <summary>
/// What the Student / Parent login (the portal on the website and the parent mobile app) may show. The admin switches
/// sections on or off on the website (Users &amp; Permissions -> Roles &amp; Access); the web portal, the parent API and the
/// app all obey it. Stored as one comma separated list of HIDDEN section keys, so a section nobody touched is visible
/// and a missing AppSettings table simply means "show everything". Singleton, cached a few seconds.
/// </summary>
public class PortalAccessService(AppSettingsRepo repo)
{
    public const string Key = "portal.hidden";

    /// <summary>Home / child switcher are always shown. Test Marks includes the class toppers.</summary>
    public static readonly PortalSection[] Sections =
    {
        new("attendance",    "Attendance",       "fas fa-calendar-check",    "Daily attendance and absent days"),
        new("lectures",      "Lectures",         "fas fa-chalkboard-teacher", "Day-wise lecture timetable"),
        new("marks",         "Test Marks",       "fas fa-chart-bar",         "Marks of every test + class toppers"),
        // "Fees" is two switches so the admin decides separately what a parent sees:
        //   fees_summary  = the Paid / Balance numbers, due alerts, next installment and the fee pie in the report
        //   fees_receipts = the list of payments and the receipt itself (view only - sharing / printing it is switched off in code)
        // The old single "fees" key is no longer stored: it is derived in Hidden() (= both switched off), so every older
        // "is Fees hidden" check - web menu, API, app builds already installed - keeps working.
        new("fees_summary",  "Fees: Paid & Balance", "fas fa-rupee-sign",    "Paid amount, balance due and installment date"),
        new("fees_receipts", "Fees: Receipts",   "fas fa-receipt",           "Payment list and receipts (view only)"),
        new("notes",         "Notes & Papers",   "fas fa-file-lines",        "Class notes and practice papers"),
        new("notifications", "Notifications",    "fas fa-bell",              "Absent / marks / fee / lecture alerts"),
        new("contact",       "Contact Us",       "fas fa-envelope",          "Message to the institute"),
    };

    /// <summary>Notification kinds that belong to the hidden portal sections - they are not listed or counted for the parent either.</summary>
    public static IReadOnlyCollection<string>? KindsOf(IEnumerable<string>? hiddenSections)
    {
        if (hiddenSections == null) return null;
        var kinds = new List<string>();
        foreach (var s in hiddenSections)
            switch ((s ?? "").ToLowerInvariant())
            {
                case "attendance": kinds.AddRange(new[] { "absent", "present", "late", "holiday" }); break;
                case "lectures":   kinds.AddRange(new[] { "lecture", "lecture_update", "lecture_cancel", "lecture_done" }); break;
                case "marks":      kinds.Add("marks"); break;
                case "fees":
                case "fees_receipts": kinds.Add("fee"); break;   // "Receipt RC-... received" alerts belong to the receipts switch
                case "notes":      kinds.AddRange(new[] { "notes", "practice" }); break;
            }
        return kinds.Count == 0 ? null : kinds;
    }

    static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);
    HashSet<string> _hidden = new(StringComparer.OrdinalIgnoreCase);
    DateTime _at = DateTime.MinValue;
    readonly object _lock = new();

    /// <summary>Keys of the hidden sections (only keys that exist in <see cref="Sections"/>).</summary>
    public HashSet<string> Hidden()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _at < Ttl) return _hidden;
            try
            {
                var all = repo.GetAll();
                var raw = all.TryGetValue(Key, out var v) ? v : "";
                if (!all.ContainsKey(SplitMarker)) raw = MigrateFeesSplit(raw);
                _hidden = Derive(Parse(raw));
            }
            catch { /* keep the last known list */ }
            _at = DateTime.UtcNow;
            return _hidden;
        }
    }

    public bool IsVisible(string section) => !Hidden().Contains(section);

    static HashSet<string> Parse(string? csv)
    {
        var valid = Sections.Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keys = (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        // a list saved before the Fees switch was split: "fees" off = both new switches off
        if (keys.Any(k => k.Equals("fees", StringComparison.OrdinalIgnoreCase))) { keys.Add("fees_summary"); keys.Add("fees_receipts"); }
        return keys.Where(valid.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>"fees" (the whole Fees tab) counts as hidden when both of its switches are off. Never stored.</summary>
    static HashSet<string> Derive(HashSet<string> set)
    {
        if (set.Contains("fees_summary") && set.Contains("fees_receipts")) set.Add("fees");
        return set;
    }

    const string SplitMarker = "portal.fees_split";

    /// <summary>
    /// One time, the first time the split switches are read: an old "fees" switch is carried over, and Paid / Balance
    /// starts switched OFF (parents see receipts only) - the admin turns it on in Roles &amp; Access when wanted.
    /// </summary>
    string MigrateFeesSplit(string raw)
    {
        var keys = (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => !k.Equals("fees", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if ((raw ?? "").Split(',', StringSplitOptions.TrimEntries).Any(k => k.Equals("fees", StringComparison.OrdinalIgnoreCase))) { keys.Add("fees_receipts"); }
        keys.Add("fees_summary");
        var joined = string.Join(',', keys);
        try { repo.Set(Key, joined, 0); repo.Set(SplitMarker, "1", 0); } catch { /* applied for now, retried on the next read */ }
        return joined;
    }

    public (bool ok, string? error) SetHidden(IEnumerable<string> hidden, int by)
    {
        var set = Parse(string.Join(',', hidden ?? Array.Empty<string>()));
        try { repo.Set(Key, string.Join(',', set), by); repo.Set(SplitMarker, "1", by); }
        catch (Exception ex) { return (false, "Could not save (" + ex.Message + ")."); }
        lock (_lock) { _hidden = Derive(set); _at = DateTime.UtcNow; }
        return (true, null);
    }
}

/// <summary>
/// Default rights per staff role (Teacher, Accounts ...). The admin ticks the modules once for a role, then either
/// saves it as the role's template (every NEW user of that role starts with it) or also applies it to all existing users of
/// the role. Individual rights can still be changed per user afterwards (Users -> Permissions). Stored in AppSettings as
/// "moduleId=1" (view) / "moduleId=2" (view + edit), comma separated.
/// </summary>
public class RoleTemplateService(AppSettingsRepo repo, UserMgmtService users)
{
    static string KeyOf(int roleId) => $"role.perms.{roleId}";

    /// <summary>moduleId -> (canView, canEdit); null when the role has no template yet.</summary>
    public Dictionary<int, (bool view, bool edit)>? Get(int roleId)
    {
        var all = repo.GetAll();
        if (!all.TryGetValue(KeyOf(roleId), out var v)) return null;
        var map = new Dictionary<int, (bool, bool)>();
        foreach (var part in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=');
            if (kv.Length == 2 && int.TryParse(kv[0], out var id) && int.TryParse(kv[1], out var f) && f > 0)
                map[id] = (true, f >= 2);
        }
        return map;
    }

    public (bool ok, string? error) Save(int roleId, IEnumerable<(int moduleId, bool view, bool edit)> entries, int by)
    {
        var valid = users.GetAllModules().Select(m => m.ModuleId).ToHashSet();
        var text = string.Join(',', entries.Where(e => valid.Contains(e.moduleId) && (e.view || e.edit))
            .Select(e => $"{e.moduleId}={(e.edit ? 2 : 1)}"));
        if (text.Length > 950) return (false, "Too many modules selected for one template.");
        try { repo.Set(KeyOf(roleId), text, by); return (true, null); }
        catch (Exception ex) { return (false, "Could not save (" + ex.Message + ")."); }
    }

    /// <summary>Writes the role's template onto one user (every module: granted or cleared). Returns false when the role has no template.</summary>
    public bool ApplyTo(int userId, int roleId)
    {
        var t = Get(roleId);
        if (t == null) return false;
        foreach (var m in users.GetAllModules())
        {
            var (view, edit) = t.TryGetValue(m.ModuleId, out var x) ? x : (false, false);
            users.SavePermission(userId, m.ModuleId, view || edit, edit);
        }
        return true;
    }

    /// <summary>Applies the template to every existing, non-deleted user of the role (never the Super Admin). Returns the user ids touched.</summary>
    public List<int> ApplyToRole(int roleId, Func<AppUser, bool>? skip = null)
    {
        var ids = new List<int>();
        foreach (var u in users.GetAll().Where(u => u.RoleId == roleId && u.UserId != 1 && (skip == null || !skip(u))))
            if (ApplyTo(u.UserId, roleId)) ids.Add(u.UserId);
        return ids;
    }
}
