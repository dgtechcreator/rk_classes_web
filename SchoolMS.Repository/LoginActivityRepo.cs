using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>
/// Sign-in history (privacy / security). One row per attempt. Fully parameterised. The table is created on first use
/// (same DDL as SQL/78), so a deploy that forgot to run the script still works.
/// </summary>
public class LoginActivityRepo(CommonConnectivity db)
{
    static volatile bool _schemaReady;
    static readonly object _lock = new();

    const string SchemaSql = @"
IF OBJECT_ID('dbo.LoginActivity', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginActivity (
        LoginId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoginActivity PRIMARY KEY,
        AccountType NVARCHAR(10)  NOT NULL,
        AccountId   INT           NULL,
        LoginName   NVARCHAR(100) NOT NULL CONSTRAINT DF_LoginActivity_LoginName DEFAULT '',
        DisplayName NVARCHAR(150) NULL,
        RoleName    NVARCHAR(60)  NULL,
        Success     BIT           NOT NULL,
        Platform    NVARCHAR(20)  NULL,
        Device      NVARCHAR(200) NULL,
        AppVersion  NVARCHAR(30)  NULL,
        IpAddress   NVARCHAR(60)  NULL,
        LoggedAt    DATETIME      NOT NULL CONSTRAINT DF_LoginActivity_LoggedAt DEFAULT GETDATE()
    );
    CREATE INDEX IX_LoginActivity_Account ON dbo.LoginActivity (AccountType, AccountId, LoggedAt DESC);
    CREATE INDEX IX_LoginActivity_Time    ON dbo.LoginActivity (LoggedAt DESC);
END";

    public void EnsureSchema()
    {
        if (_schemaReady) return;
        lock (_lock)
        {
            if (_schemaReady) return;
            db.ExecSql(SchemaSql, new());
            _schemaReady = true;
        }
    }

    static string? Cut(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length <= max ? s.Trim() : s.Trim()[..max]);

    public void Add(LoginActivityEntry e)
    {
        EnsureSchema();
        db.ExecSql(@"INSERT INTO LoginActivity (AccountType, AccountId, LoginName, DisplayName, RoleName, Success, Platform, Device, AppVersion, IpAddress)
                     VALUES (@T, @Id, @Login, @Name, @Role, @Ok, @Plat, @Dev, @Ver, @Ip)",
            new() {
                { "@T", e.AccountType }, { "@Id", e.AccountId }, { "@Login", Cut(e.LoginName, 100) ?? "" }, { "@Name", Cut(e.DisplayName, 150) },
                { "@Role", Cut(e.RoleName, 60) }, { "@Ok", e.Success }, { "@Plat", Cut(e.Platform, 20) }, { "@Dev", Cut(e.Device, 200) },
                { "@Ver", Cut(e.AppVersion, 30) }, { "@Ip", Cut(e.IpAddress, 60) },
            });
    }

    /// <summary>Newest first. Every filter is optional.</summary>
    public List<LoginActivityEntry> Query(string? accountType, int? accountId, string? search, DateTime? from, DateTime? to, bool? success, int take = 200)
    {
        EnsureSchema();
        var q = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        return db.SqlP(@"
            SELECT TOP (@Take) LoginId, AccountType, AccountId, LoginName, DisplayName, RoleName, Success, Platform, Device, AppVersion, IpAddress, LoggedAt
            FROM LoginActivity
            WHERE (@T IS NULL OR AccountType = @T)
              AND (@Id IS NULL OR AccountId = @Id)
              AND (@Q IS NULL OR LoginName LIKE @Q OR DisplayName LIKE @Q OR RoleName LIKE @Q OR Device LIKE @Q OR IpAddress LIKE @Q)
              AND (@From IS NULL OR LoggedAt >= @From)
              AND (@To IS NULL OR LoggedAt < DATEADD(day, 1, @To))
              AND (@Ok IS NULL OR Success = @Ok)
            ORDER BY LoggedAt DESC, LoginId DESC",
            new() { { "@Take", Math.Clamp(take, 1, 1000) }, { "@T", accountType }, { "@Id", accountId }, { "@Q", q }, { "@From", from }, { "@To", to }, { "@Ok", success } },
            r => new LoginActivityEntry {
                LoginId = G.G<int>(r, "LoginId"), AccountType = G.G<string>(r, "AccountType") ?? "staff", AccountId = G.G<int?>(r, "AccountId"),
                LoginName = G.G<string>(r, "LoginName") ?? "", DisplayName = G.G<string>(r, "DisplayName"), RoleName = G.G<string>(r, "RoleName"),
                Success = G.G<bool>(r, "Success"), Platform = G.G<string>(r, "Platform"), Device = G.G<string>(r, "Device"),
                AppVersion = G.G<string>(r, "AppVersion"), IpAddress = G.G<string>(r, "IpAddress"), LoggedAt = G.G<DateTime>(r, "LoggedAt"),
            });
    }

    /// <summary>Distinct devices an account signed in from (successful logins), most recently used first.</summary>
    public List<DeviceUse> Devices(string accountType, int accountId)
    {
        EnsureSchema();
        return db.SqlP(@"
            SELECT ISNULL(Device, 'Unknown device') AS Device, MAX(Platform) AS Platform, COUNT(*) AS Logins, MAX(LoggedAt) AS LastUsed,
                   (SELECT TOP 1 x.IpAddress FROM LoginActivity x WHERE x.AccountType = @T AND x.AccountId = @Id AND x.Success = 1
                    AND ISNULL(x.Device, 'Unknown device') = ISNULL(a.Device, 'Unknown device') ORDER BY x.LoggedAt DESC) AS LastIp
            FROM LoginActivity a
            WHERE AccountType = @T AND AccountId = @Id AND Success = 1
            GROUP BY ISNULL(Device, 'Unknown device'), Device
            ORDER BY MAX(LoggedAt) DESC",
            new() { { "@T", accountType }, { "@Id", accountId } },
            r => new DeviceUse { Device = G.G<string>(r, "Device") ?? "Unknown device", Platform = G.G<string>(r, "Platform"), Logins = G.G<int>(r, "Logins"), LastUsed = G.G<DateTime>(r, "LastUsed"), LastIp = G.G<string>(r, "LastIp") });
    }
}
