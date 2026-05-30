using System.Runtime.InteropServices;
using System.Text;

namespace DressAndGo.Data;

public sealed class SqliteConnection : IDisposable {
    private IntPtr db_handle;
    private bool _disposed;

    const string Lib = "e_sqlite3";

    [DllImport(Lib, EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_open(byte[] filename, out IntPtr db);

    [DllImport(Lib, EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_close(IntPtr db);

    [DllImport(Lib, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int nBytes, out IntPtr stmt, out IntPtr tail);

    [DllImport(Lib, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_step(IntPtr stmt);

    [DllImport(Lib, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_finalize(IntPtr stmt);

    [DllImport(Lib, EntryPoint = "sqlite3_column_count", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_column_count(IntPtr stmt);

    [DllImport(Lib, EntryPoint = "sqlite3_column_name", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_column_name(IntPtr stmt, int col);

    [DllImport(Lib, EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_column_type(IntPtr stmt, int col);

    [DllImport(Lib, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);

    [DllImport(Lib, EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl)]
    static extern long sqlite3_column_int64(IntPtr stmt, int col);

    [DllImport(Lib, EntryPoint = "sqlite3_column_double", CallingConvention = CallingConvention.Cdecl)]
    static extern double sqlite3_column_double(IntPtr stmt, int col);

    [DllImport(Lib, EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_text(IntPtr stmt, int col, byte[] val, int n, IntPtr destructor);

    [DllImport(Lib, EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_int64(IntPtr stmt, int col, long val);

    [DllImport(Lib, EntryPoint = "sqlite3_bind_double", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_double(IntPtr stmt, int col, double val);

    [DllImport(Lib, EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_null(IntPtr stmt, int col);

    [DllImport(Lib, EntryPoint = "sqlite3_last_insert_rowid", CallingConvention = CallingConvention.Cdecl)]
    static extern long sqlite3_last_insert_rowid(IntPtr db);

    [DllImport(Lib, EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_errmsg(IntPtr db);

    const int SQLITE_ROW  = 100;
    const int SQLITE_DONE = 101;
    const int SQLITE_OK   = 0;

    public SqliteConnection(string path) {
        var pathBytes = Encoding.UTF8.GetBytes(path + "\0");
        if (sqlite3_open(pathBytes, out db_handle) != SQLITE_OK)
            throw new Exception("Não foi possível abrir o banco: " + path);

        Execute("PRAGMA journal_mode=WAL;");
        Execute("PRAGMA foreign_keys=ON;");
    }

    string GetLastError() => Marshal.PtrToStringUTF8(sqlite3_errmsg(db_handle)) ?? "erro desconhecido";

    public void Execute(string sql, Dictionary<string, object?>? p = null) {
        var stmt = Prepare(sql);
        try { Bind(stmt, p); sqlite3_step(stmt); }
        finally { sqlite3_finalize(stmt); }
    }

    public long ExecuteInsert(string sql, Dictionary<string, object?>? p = null) {
        Execute(sql, p);
        return sqlite3_last_insert_rowid(db_handle);
    }

    public List<Dictionary<string, object?>> Query(string sql, Dictionary<string, object?>? p = null) {
        var rows = new List<Dictionary<string, object?>>();
        var stmt = Prepare(sql);
        try {
            Bind(stmt, p);
            int cols = sqlite3_column_count(stmt);

            var nomes = new string[cols];
            for (int i = 0; i < cols; i++)
                nomes[i] = Marshal.PtrToStringUTF8(sqlite3_column_name(stmt, i)) ?? $"col{i}";

            while (sqlite3_step(stmt) == SQLITE_ROW) {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cols; i++) {
                    row[nomes[i]] = sqlite3_column_type(stmt, i) switch {
                        1 => sqlite3_column_int64(stmt, i),
                        2 => sqlite3_column_double(stmt, i),
                        3 => Marshal.PtrToStringUTF8(sqlite3_column_text(stmt, i)),
                        _ => null
                    };
                }
                rows.Add(row);
            }
        }
        finally { sqlite3_finalize(stmt); }

        return rows;
    }

    public Dictionary<string, object?>? QueryOne(string sql, Dictionary<string, object?>? p = null) {
        var r = Query(sql, p);
        return r.Count > 0 ? r[0] : null;
    }

    public T? Scalar<T>(string sql, Dictionary<string, object?>? p = null) {
        var row = QueryOne(sql, p);
        if (row == null || row.Count == 0) return default;
        var v = row.Values.First();
        if (v == null) return default;
        return (T)Convert.ChangeType(v, typeof(T));
    }

    IntPtr Prepare(string sql) {
        var bytes = Encoding.UTF8.GetBytes(sql + "\0");
        if (sqlite3_prepare_v2(db_handle, bytes, -1, out IntPtr stmt, out _) != SQLITE_OK)
            throw new Exception($"SQL inválido: {GetLastError()}\n{sql}");
        return stmt;
    }

    static void Bind(IntPtr stmt, Dictionary<string, object?>? p) {
        if (p == null) return;

        int idx = 1;
        foreach (var (_, v) in p) {
            if (v == null)
                sqlite3_bind_null(stmt, idx);
            else if (v is long l)
                sqlite3_bind_int64(stmt, idx, l);
            else if (v is int i)
                sqlite3_bind_int64(stmt, idx, i);
            else if (v is double d)
                sqlite3_bind_double(stmt, idx, d);
            else if (v is decimal dec)
                sqlite3_bind_double(stmt, idx, (double)dec);
            else if (v is bool b)
                sqlite3_bind_int64(stmt, idx, b ? 1 : 0);
            else {
                var bytes = Encoding.UTF8.GetBytes(v.ToString()! + "\0");
                sqlite3_bind_text(stmt, idx, bytes, bytes.Length - 1, new IntPtr(-1));
            }
            idx++;
        }
    }

    public void Dispose() {
        if (_disposed) return;
        if (db_handle != IntPtr.Zero) sqlite3_close(db_handle);
        _disposed = true;
    }
}
