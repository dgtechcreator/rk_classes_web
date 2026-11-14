using SchoolMS.DB;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class WebAuthnCredential
{
    public string  CredentialId { get; set; } = "";   // base64url
    public string  AccountType  { get; set; } = "staff";   // staff | parent
    public int     AccountId    { get; set; }
    public string  PublicKey    { get; set; } = "";   // base64
    public long    SignCount    { get; set; }
    public string? DeviceName   { get; set; }
    public DateTime CreatedAt   { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// Fingerprint / passkey logins of the website (WebAuthn): the public key of each device's credential. The private key never
/// leaves the person's device. Table created on first use (same DDL as SQL/81).
/// </summary>
public class WebAuthnRepo(CommonConnectivity db)
{
    static volatile bool _ready;
    static readonly object _lock = new();

    const string SchemaSql = @"
IF OBJECT_ID('dbo.WebAuthnCredentials', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebAuthnCredentials (
        CredentialId NVARCHAR(512)  NOT NULL CONSTRAINT PK_WebAuthnCredentials PRIMARY KEY,
        AccountType  NVARCHAR(10)   NOT NULL,
        AccountId    INT            NOT NULL,
        PublicKey    NVARCHAR(MAX)  NOT NULL,
        SignCount    BIGINT         NOT NULL CONSTRAINT DF_WebAuthn_Sign DEFAULT 0,
        DeviceName   NVARCHAR(200)  NULL,
        CreatedAt    DATETIME       NOT NULL CONSTRAINT DF_WebAuthn_Created DEFAULT GETDATE(),
        LastUsedAt   DATETIME       NULL
    );
    CREATE INDEX IX_WebAuthn_Account ON dbo.WebAuthnCredentials (AccountType, AccountId);
END";

    void Ensure() { if (_ready) return; lock (_lock) { if (_ready) return; db.ExecSql(SchemaSql, new()); _ready = true; } }
    public void EnsureSchema() => Ensure();

    static WebAuthnCredential Map(Microsoft.Data.SqlClient.SqlDataReader r) => new() {
        CredentialId = G.G<string>(r, "CredentialId") ?? "", AccountType = G.G<string>(r, "AccountType") ?? "staff", AccountId = G.G<int>(r, "AccountId"),
        PublicKey = G.G<string>(r, "PublicKey") ?? "", SignCount = G.G<long>(r, "SignCount"), DeviceName = G.G<string>(r, "DeviceName"),
        CreatedAt = G.G<DateTime>(r, "CreatedAt"), LastUsedAt = G.G<DateTime?>(r, "LastUsedAt"),
    };

    public void Add(WebAuthnCredential c)
    {
        Ensure();
        db.ExecSql("INSERT INTO WebAuthnCredentials (CredentialId, AccountType, AccountId, PublicKey, SignCount, DeviceName) VALUES (@Id, @T, @A, @Key, @Cnt, @Dev)",
            new() { { "@Id", c.CredentialId }, { "@T", c.AccountType }, { "@A", c.AccountId }, { "@Key", c.PublicKey }, { "@Cnt", c.SignCount }, { "@Dev", c.DeviceName } });
    }

    public WebAuthnCredential? Get(string credentialId)
    {
        Ensure();
        return db.SqlP("SELECT * FROM WebAuthnCredentials WHERE CredentialId = @Id", new() { { "@Id", credentialId } }, Map).FirstOrDefault();
    }

    public List<WebAuthnCredential> ForAccount(string type, int id)
    {
        Ensure();
        return db.SqlP("SELECT * FROM WebAuthnCredentials WHERE AccountType = @T AND AccountId = @A ORDER BY CreatedAt DESC", new() { { "@T", type }, { "@A", id } }, Map);
    }

    public void Touch(string credentialId, long signCount)
    {
        Ensure();
        db.ExecSql("UPDATE WebAuthnCredentials SET SignCount = @C, LastUsedAt = GETDATE() WHERE CredentialId = @Id", new() { { "@Id", credentialId }, { "@C", signCount } });
    }

    public void Remove(string type, int accountId, string credentialId)
    {
        Ensure();
        db.ExecSql("DELETE FROM WebAuthnCredentials WHERE CredentialId = @Id AND AccountType = @T AND AccountId = @A", new() { { "@Id", credentialId }, { "@T", type }, { "@A", accountId } });
    }
}
