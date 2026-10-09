using Microsoft.Data.SqlClient;
using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>
/// StudentId -> "Surname StudentName FatherName MotherName". Most stored procedures only return FullName, so repositories
/// swap in the display name here by StudentId and every screen (web, mobile, PDFs) shows the same format.
/// Cached briefly; a failed lookup falls back to the stored name rather than breaking the page.
/// </summary>
public static class StudentNameDirectory
{
    static readonly object Gate = new();
    static Dictionary<int, string> _names = new();
    static DateTime _loadedAt = DateTime.MinValue;
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    /// <summary>Call after a student is saved / deleted so the next read sees the new name immediately.</summary>
    public static void Invalidate() { lock (Gate) _loadedAt = DateTime.MinValue; }

    public static string? Display(CommonConnectivity db, int studentId, string? stored)
    {
        if (studentId <= 0) return stored;
        return Snapshot(db).TryGetValue(studentId, out var n) && n.Length > 0 ? n : stored;
    }

    /// <summary>The row's student name (column <paramref name="col"/>) as a display name, keyed by the row's StudentId column.</summary>
    public static string? StudentName(this SqlDataReader r, CommonConnectivity db, string col = "FullName")
        => Display(db, G.G<int>(r, "StudentId"), G.G<string>(r, col));

    static Dictionary<int, string> Snapshot(CommonConnectivity db)
    {
        lock (Gate)
        {
            if (DateTime.UtcNow - _loadedAt < Ttl) return _names;
            try
            {
                var map = new Dictionary<int, string>();
                foreach (var (id, name) in db.Sql("SELECT StudentId, FullName, FatherName, MotherName FROM Students",
                    r => (G.G<int>(r, "StudentId"),
                          StudentNameFormatter.Format(G.G<string>(r, "FullName"), G.G<string>(r, "FatherName"), G.G<string>(r, "MotherName")))))
                    map[id] = name;
                _names = map;
                _loadedAt = DateTime.UtcNow;
            }
            catch { /* keep serving the previous snapshot (or stored names) */ _loadedAt = DateTime.UtcNow - Ttl + TimeSpan.FromSeconds(15); }
            return _names;
        }
    }
}
