using SchoolMS.DB;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>
/// Who did it and when, for the actions that hide a record: deleting a student / faculty member / user and switching a user
/// off. Deleting a student or faculty member never stored the person - only the status changed - so the Deleted Records
/// pages had nothing to show. The columns are created on first use (nullable, nothing existing changes); records deleted
/// before this change keep an empty "by / on".
/// </summary>
public static class ActionAudit
{
    static volatile bool _ready;
    static readonly object _lock = new();

    const string SchemaSql = @"
IF COL_LENGTH('dbo.Students', 'DeletedBy') IS NULL ALTER TABLE dbo.Students ADD DeletedBy INT NULL;
IF COL_LENGTH('dbo.Students', 'DeletedAt') IS NULL ALTER TABLE dbo.Students ADD DeletedAt DATETIME NULL;
IF COL_LENGTH('dbo.Faculty',  'DeletedBy') IS NULL ALTER TABLE dbo.Faculty  ADD DeletedBy INT NULL;
IF COL_LENGTH('dbo.Faculty',  'DeletedAt') IS NULL ALTER TABLE dbo.Faculty  ADD DeletedAt DATETIME NULL;
IF COL_LENGTH('dbo.Users',    'DeletedBy') IS NULL ALTER TABLE dbo.Users    ADD DeletedBy INT NULL;
IF COL_LENGTH('dbo.Users',    'DeletedAt') IS NULL ALTER TABLE dbo.Users    ADD DeletedAt DATETIME NULL;
IF COL_LENGTH('dbo.Users',    'DeactivatedBy') IS NULL ALTER TABLE dbo.Users ADD DeactivatedBy INT NULL;
IF COL_LENGTH('dbo.Users',    'DeactivatedAt') IS NULL ALTER TABLE dbo.Users ADD DeactivatedAt DATETIME NULL;";

    public static void Ensure(CommonConnectivity db)
    {
        if (_ready) return;
        lock (_lock)
        {
            if (_ready) return;
            db.ExecSql(SchemaSql, new());
            _ready = true;
        }
    }

    // table / key column come from this fixed list only (never from a request)
    static (string table, string key) Target(string kind) => kind switch
    {
        "student" => ("Students", "StudentId"),
        "faculty" => ("Faculty", "FacultyId"),
        "user" => ("Users", "UserId"),
        _ => throw new ArgumentException("unknown kind"),
    };

    /// <summary>Remembers who deleted the record, now. Never throws: a missing column must not break the delete itself.</summary>
    public static void StampDeleted(CommonConnectivity db, string kind, int id, int by)
    {
        if (by <= 0) return;
        try
        {
            Ensure(db);
            var (t, k) = Target(kind);
            db.ExecSql($"UPDATE {t} SET DeletedBy = @By, DeletedAt = GETDATE() WHERE {k} = @Id", new() { { "@By", by }, { "@Id", id } });
        }
        catch { /* audit only */ }
    }

    public static void ClearDeleted(CommonConnectivity db, string kind, int id)
    {
        try
        {
            Ensure(db);
            var (t, k) = Target(kind);
            db.ExecSql($"UPDATE {t} SET DeletedBy = NULL, DeletedAt = NULL WHERE {k} = @Id", new() { { "@Id", id } });
        }
        catch { }
    }

    /// <summary>id -> (when, who) for every record of that kind that has a deleted stamp.</summary>
    public static Dictionary<int, (DateTime? at, string? by)> DeletedInfo(CommonConnectivity db, string kind)
    {
        try
        {
            Ensure(db);
            var (t, k) = Target(kind);
            return db.Sql($@"SELECT x.{k} AS Id, x.DeletedAt, u.FullName AS ByName
                             FROM {t} x LEFT JOIN Users u ON u.UserId = x.DeletedBy
                             WHERE x.DeletedBy IS NOT NULL OR x.DeletedAt IS NOT NULL",
                r => (Id: G.G<int>(r, "Id"), At: G.G<DateTime?>(r, "DeletedAt"), By: G.G<string>(r, "ByName")))
                .ToDictionary(x => x.Id, x => (x.At, x.By));
        }
        catch { return new(); }
    }
}
