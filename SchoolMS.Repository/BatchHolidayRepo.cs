using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>"No class" days of an attendance batch. The table is created on first use, so a deploy without a script still works.</summary>
public class BatchHolidayRepo(CommonConnectivity db)
{
    const string SchemaSql = @"
IF OBJECT_ID('dbo.AttendanceBatchHolidays', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AttendanceBatchHolidays (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AttendanceBatchHolidays PRIMARY KEY,
        BatchId       INT            NOT NULL,
        HolidayDate   DATE           NOT NULL,
        Reason        NVARCHAR(300)  NULL,
        CreatedBy     INT            NULL,
        CreatedByName NVARCHAR(150)  NULL,
        CreatedAt     DATETIME       NOT NULL CONSTRAINT DF_AttendanceBatchHolidays_At DEFAULT GETDATE(),
        CONSTRAINT UQ_AttendanceBatchHolidays UNIQUE (BatchId, HolidayDate)
    );
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

    public List<BatchHoliday> GetForDate(DateTime date)
    {
        EnsureSchema();
        return db.SqlP("SELECT * FROM AttendanceBatchHolidays WHERE HolidayDate = @D", new() { { "@D", date.Date } },
            r => new BatchHoliday {
                Id = G.G<int>(r, "Id"), BatchId = G.G<int>(r, "BatchId"), HolidayDate = G.G<DateTime>(r, "HolidayDate"),
                Reason = G.G<string>(r, "Reason") ?? "", CreatedByName = G.G<string>(r, "CreatedByName") ?? "", CreatedAt = G.G<DateTime>(r, "CreatedAt"),
            });
    }

    public void Add(int batchId, DateTime date, string? reason, int by, string byName)
    {
        EnsureSchema();
        db.ExecSql(@"IF NOT EXISTS (SELECT 1 FROM AttendanceBatchHolidays WHERE BatchId=@B AND HolidayDate=@D)
                        INSERT INTO AttendanceBatchHolidays (BatchId, HolidayDate, Reason, CreatedBy, CreatedByName) VALUES (@B, @D, @R, @By, @N)",
            new() { { "@B", batchId }, { "@D", date.Date }, { "@R", string.IsNullOrWhiteSpace(reason) ? null : reason.Trim() }, { "@By", by > 0 ? by : null }, { "@N", byName } });
    }

    public void Remove(int batchId, DateTime date)
    {
        EnsureSchema();
        db.ExecSql("DELETE FROM AttendanceBatchHolidays WHERE BatchId=@B AND HolidayDate=@D", new() { { "@B", batchId }, { "@D", date.Date } });
    }
}
