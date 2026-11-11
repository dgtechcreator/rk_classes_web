using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>Payment Modes master. The table is created and seeded on first use (Cash, Online Transfer, Cheque, Demand Draft, Card).</summary>
public class PaymentModeRepo(CommonConnectivity db)
{
    const string SchemaSql = @"
IF OBJECT_ID('dbo.PaymentModes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentModes (
        ModeId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentModes PRIMARY KEY,
        ModeName  NVARCHAR(50) NOT NULL,
        IsActive  BIT NOT NULL CONSTRAINT DF_PaymentModes_Active DEFAULT 1,
        OrderNo   INT NOT NULL CONSTRAINT DF_PaymentModes_Order DEFAULT 0
    );
    INSERT INTO PaymentModes (ModeName, OrderNo) VALUES (N'Cash', 1), (N'Online Transfer', 2), (N'Cheque', 3), (N'Demand Draft', 4), (N'Card', 5);
END;";

    static volatile bool _ready;
    static readonly object _lock = new();

    public void EnsureSchema()
    {
        if (_ready) return;
        lock (_lock)
        {
            if (_ready) return;
            db.ExecSql(SchemaSql, new());
            _ready = true;
        }
    }

    public List<PaymentMode> GetAll()
    {
        EnsureSchema();
        // UsageCount = how many expenses / fee payments / teacher payments use the name (missing tables count as 0)
        var modes = db.SqlP("SELECT ModeId, ModeName, IsActive FROM PaymentModes ORDER BY OrderNo, ModeName", new(),
            r => new PaymentMode { ModeId = G.G<int>(r, "ModeId"), ModeName = G.G<string>(r, "ModeName") ?? "", IsActive = G.G<bool>(r, "IsActive") });
        foreach (var m in modes)
        {
            int n = 0;
            foreach (var table in new[] { "Expenses", "FeePayments", "TeacherPayments" })
            {
                try { n += db.SqlP($"SELECT COUNT(*) AS C FROM {table} WHERE PaymentMode = @N", new() { { "@N", m.ModeName } }, r => G.G<int>(r, "C")).FirstOrDefault(); }
                catch { /* table not present */ }
            }
            m.UsageCount = n;
        }
        return modes;
    }

    public int Save(PaymentMode m)
    {
        EnsureSchema();
        if (m.ModeId > 0)
        {
            db.ExecSql("UPDATE PaymentModes SET ModeName=@N, IsActive=@A WHERE ModeId=@Id", new() { { "@N", m.ModeName }, { "@A", m.IsActive }, { "@Id", m.ModeId } });
            return m.ModeId;
        }
        return db.SqlP("INSERT INTO PaymentModes (ModeName, IsActive, OrderNo) OUTPUT INSERTED.ModeId VALUES (@N, @A, (SELECT ISNULL(MAX(OrderNo),0)+1 FROM PaymentModes))",
            new() { { "@N", m.ModeName }, { "@A", m.IsActive } }, r => G.G<int>(r, "ModeId")).First();
    }

    public void Delete(int id)
    {
        EnsureSchema();
        db.ExecSql("DELETE FROM PaymentModes WHERE ModeId=@Id", new() { { "@Id", id } });   // old records keep the name they were saved with
    }
}
