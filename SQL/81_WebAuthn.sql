-- 81: fingerprint / passkey login on the website (WebAuthn): one public key per device.
-- Safe to run more than once. The website also creates this table by itself the first time it needs it (WebAuthnRepo).
IF OBJECT_ID('dbo.WebAuthnCredentials', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebAuthnCredentials (
        CredentialId NVARCHAR(512)  NOT NULL CONSTRAINT PK_WebAuthnCredentials PRIMARY KEY,
        AccountType  NVARCHAR(10)   NOT NULL,          -- 'staff' or 'parent'
        AccountId    INT            NOT NULL,
        PublicKey    NVARCHAR(MAX)  NOT NULL,
        SignCount    BIGINT         NOT NULL CONSTRAINT DF_WebAuthn_Sign DEFAULT 0,
        DeviceName   NVARCHAR(200)  NULL,
        CreatedAt    DATETIME       NOT NULL CONSTRAINT DF_WebAuthn_Created DEFAULT GETDATE(),
        LastUsedAt   DATETIME       NULL
    );
    CREATE INDEX IX_WebAuthn_Account ON dbo.WebAuthnCredentials (AccountType, AccountId);
END
GO
PRINT 'WebAuthnCredentials ready.';
