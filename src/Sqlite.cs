using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LunarHitFix {
    // Windows supplies winsqlite3.dll. No downloaded native library or NuGet package.
    public sealed class Sqlite : IDisposable {
        IntPtr handle;
        const string Dll = "winsqlite3.dll";
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_busy_timeout(IntPtr db, int milliseconds);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg16(IntPtr db);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Unicode)] static extern int sqlite3_prepare16_v2(IntPtr db, string sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr statement);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr statement);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_name16(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_type(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern long sqlite3_column_int64(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern double sqlite3_column_double(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text16(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_blob(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_null(IntPtr statement, int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_double(IntPtr statement, int index, double value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Unicode)] static extern int sqlite3_bind_text16(IntPtr statement, int index, string value, int bytes, IntPtr destructor);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_blob(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_backup_init(IntPtr dest, byte[] destName, IntPtr source, byte[] sourceName);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_backup_step(IntPtr backup, int pages);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_backup_finish(IntPtr backup);

        public Sqlite(string path, bool create) {
            int code = sqlite3_open_v2(Utf8(path), out handle, create ? 6 : 2, IntPtr.Zero);
            if (code != 0) {
                string message = handle == IntPtr.Zero ? "Cannot open SQLite database" : Marshal.PtrToStringUni(sqlite3_errmsg16(handle));
                Dispose();
                throw new InvalidOperationException("SQLite " + code + ": " + message);
            }
            Check(sqlite3_busy_timeout(handle, 2000));
        }
        static byte[] Utf8(string text) { return Encoding.UTF8.GetBytes(text + "\0"); }
        void Check(int code) {
            if (code != 0) throw new InvalidOperationException("SQLite " + code + ": " + Marshal.PtrToStringUni(sqlite3_errmsg16(handle)));
        }
        public List<Dictionary<string, object>> Query(string sql, params object[] values) {
            IntPtr statement;
            Check(sqlite3_prepare16_v2(handle, sql, -1, out statement, IntPtr.Zero));
            try {
                for (int i = 0; i < values.Length; i++) {
                    object value = values[i];
                    int code;
                    if (value == null) code = sqlite3_bind_null(statement, i + 1);
                    else if (value is byte[]) code = sqlite3_bind_blob(statement, i + 1, (byte[])value, ((byte[])value).Length, new IntPtr(-1));
                    else if (value is double || value is float) code = sqlite3_bind_double(statement, i + 1, Convert.ToDouble(value));
                    else if (value is long || value is int || value is bool) code = sqlite3_bind_int64(statement, i + 1, Convert.ToInt64(value));
                    else code = sqlite3_bind_text16(statement, i + 1, Convert.ToString(value), -1, new IntPtr(-1));
                    Check(code);
                }
                var rows = new List<Dictionary<string, object>>();
                int result;
                while ((result = sqlite3_step(statement)) == 100) {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < sqlite3_column_count(statement); i++) {
                        object value;
                        switch (sqlite3_column_type(statement, i)) {
                            case 1: value = sqlite3_column_int64(statement, i); break;
                            case 2: value = sqlite3_column_double(statement, i); break;
                            case 3: value = Marshal.PtrToStringUni(sqlite3_column_text16(statement, i)); break;
                            case 4:
                                byte[] bytes = new byte[sqlite3_column_bytes(statement, i)];
                                if (bytes.Length > 0) Marshal.Copy(sqlite3_column_blob(statement, i), bytes, 0, bytes.Length);
                                value = bytes; break;
                            default: value = null; break;
                        }
                        row[Marshal.PtrToStringUni(sqlite3_column_name16(statement, i))] = value;
                    }
                    rows.Add(row);
                }
                if (result != 101) Check(result);
                return rows;
            } finally { sqlite3_finalize(statement); }
        }
        public void Backup(string path) {
            using (var destination = new Sqlite(path, true)) {
                IntPtr backup = sqlite3_backup_init(destination.handle, Utf8("main"), handle, Utf8("main"));
                if (backup == IntPtr.Zero) { destination.Check(1); return; }
                int step = sqlite3_backup_step(backup, -1);
                int finish = sqlite3_backup_finish(backup);
                if (step != 101) destination.Check(step);
                destination.Check(finish);
            }
        }
        public void Dispose() { if (handle != IntPtr.Zero) { sqlite3_close(handle); handle = IntPtr.Zero; } }
    }
}
