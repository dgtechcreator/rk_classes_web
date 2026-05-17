using Microsoft.Data.SqlClient;
using System.Data;

namespace SchoolMS.DB;

public class CommonConnectivity
{
    private readonly string _cs;
    public CommonConnectivity(string cs) => _cs = cs;
    public SqlConnection Open() { var c = new SqlConnection(_cs); c.Open(); return c; }

    // Read list
    public List<T> Read<T>(string sp, Dictionary<string,object?> p, Func<SqlDataReader,T> map)
    {
        var list = new List<T>();
        using var c = Open(); using var cmd = Cmd(c, sp, p);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    // Read with output param (paged)
    public List<T> ReadPaged<T>(string sp, Dictionary<string,object?> p, Func<SqlDataReader,T> map, SqlParameter outParam)
    {
        var list = new List<T>();
        using var c = Open(); using var cmd = Cmd(c, sp, p);
        cmd.Parameters.Add(outParam);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    // Execute non-query
    public int Exec(string sp, Dictionary<string,object?> p)
    {
        using var c = Open(); using var cmd = Cmd(c, sp, p);
        return cmd.ExecuteNonQuery();
    }

    // Execute with output int param
    public int ExecOut(string sp, Dictionary<string,object?> p, string outName)
    {
        using var c = Open(); using var cmd = Cmd(c, sp, p);
        var op = new SqlParameter(outName, SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(op);
        cmd.ExecuteNonQuery();
        return op.Value is DBNull ? 0 : Convert.ToInt32(op.Value);
    }

    // Raw SQL helper (for simple lookups)
    public List<T> Sql<T>(string sql, Func<SqlDataReader,T> map)
    {
        var list = new List<T>();
        using var c = Open(); using var cmd = new SqlCommand(sql, c);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    private static SqlCommand Cmd(SqlConnection c, string sp, Dictionary<string,object?> p)
    {
        var cmd = new SqlCommand(sp, c) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
        foreach (var kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
        return cmd;
    }

    public static T? G<T>(SqlDataReader r, string col)
    {
        try { int i = r.GetOrdinal(col); if (r.IsDBNull(i)) return default; return (T)Convert.ChangeType(r.GetValue(i), typeof(T)); }
        catch { return default; }
    }
}
