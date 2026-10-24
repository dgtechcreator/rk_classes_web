using SchoolMS.DB;

namespace SchoolMS.Repository;

/// <summary>
/// Tiny key/value store for switches the admin flips from the website and the mobile app obeys (maintenance mode).
/// The AppSettings table is created on first save (also in SQL/66_AppSettings.sql); until it exists every read just
/// returns nothing, so a missing table can never break the app or the website.
/// </summary>
public class AppSettingsRepo(CommonConnectivity db)
{
    const string EnsureTable = @"
        IF OBJECT_ID('dbo.AppSettings','U') IS NULL
            CREATE TABLE dbo.AppSettings (
                SettingKey   NVARCHAR(100)  NOT NULL PRIMARY KEY,
                SettingValue NVARCHAR(1000) NULL,
                UpdatedAt    DATETIME       NOT NULL DEFAULT GETDATE(),
                UpdatedBy    INT            NULL
            );";

    public Dictionary<string, string> GetAll()
    {
        try
        {
            var rows = db.SqlP("SELECT SettingKey, SettingValue FROM AppSettings", new(),
                r => (Key: CommonConnectivity.G<string>(r, "SettingKey") ?? "", Value: CommonConnectivity.G<string>(r, "SettingValue") ?? ""));
            return rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }

    /// <summary>Saves one value. Throws if the table cannot be created / written (caller shows a message).</summary>
    public void Set(string key, string value, int by)
    {
        db.ExecSql(EnsureTable, new());
        db.ExecSql(@"
            UPDATE AppSettings SET SettingValue=@V, UpdatedAt=GETDATE(), UpdatedBy=@By WHERE SettingKey=@K;
            IF @@ROWCOUNT = 0 INSERT INTO AppSettings (SettingKey, SettingValue, UpdatedBy) VALUES (@K, @V, @By);",
            new() { { "@K", key }, { "@V", value }, { "@By", by } });
    }
}
